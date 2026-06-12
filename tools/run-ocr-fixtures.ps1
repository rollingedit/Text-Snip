param(
    [string]$FixtureRoot = "Fixtures/generated",
    [string]$OutputPath = "artifacts/reports/fixture-results.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$root = Join-Path $repoRoot $FixtureRoot
$output = Join-Path $repoRoot $OutputPath
$dotnet = Join-Path $repoRoot ".dotnet/dotnet.exe"
if (!(Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

function Invoke-Native([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Native command failed with exit code $LASTEXITCODE"
    }
}

function Get-Cer([string]$Expected, [string]$Actual) {
    if ($Expected.Length -eq 0) {
        return $(if ($Actual.Length -eq 0) { 0 } else { 1 })
    }

    $previous = 0..$Actual.Length
    for ($i = 1; $i -le $Expected.Length; $i++) {
        $current = New-Object int[] ($Actual.Length + 1)
        $current[0] = $i
        for ($j = 1; $j -le $Actual.Length; $j++) {
            $cost = if ($Expected[$i - 1] -eq $Actual[$j - 1]) { 0 } else { 1 }
            $current[$j] = [Math]::Min(
                [Math]::Min($current[$j - 1] + 1, $previous[$j] + 1),
                $previous[$j - 1] + $cost)
        }
        $previous = $current
    }

    return [Math]::Round($previous[$Actual.Length] / [double]$Expected.Length, 4)
}

function Normalize-Text([string]$Value) {
    if ($null -eq $Value) {
        $Value = ""
    }

    return (($Value) -replace "\s+", " ").Trim()
}

function Test-ContainsToken([string]$Text, [string]$Token) {
    $normalizedText = Normalize-Text $Text
    $normalizedToken = Normalize-Text $Token
    if ([string]::IsNullOrWhiteSpace($normalizedToken)) {
        return $true
    }

    return $normalizedText.IndexOf($normalizedToken, [StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Get-WordBoundaryStats([string]$Expected, [string]$Actual) {
    $expectedWords = @((Normalize-Text $Expected).Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries))
    $actualNormalized = Normalize-Text $Actual
    $actualCompacted = $actualNormalized -replace "\s+", ""

    if ($expectedWords.Count -le 1) {
        return [pscustomobject]@{
            expectedBoundaryCount = 0
            preservedBoundaryCount = 0
            compactedMatch = $false
            score = 1.0
        }
    }

    $preserved = 0
    for ($i = 0; $i -lt ($expectedWords.Count - 1); $i++) {
        $left = [regex]::Escape($expectedWords[$i])
        $right = [regex]::Escape($expectedWords[$i + 1])
        if ($actualNormalized -match "(?i)$left\s+$right") {
            $preserved++
        }
    }

    $expectedCompacted = ((Normalize-Text $Expected) -replace "\s+", "")
    return [pscustomobject]@{
        expectedBoundaryCount = $expectedWords.Count - 1
        preservedBoundaryCount = $preserved
        compactedMatch = ($expectedCompacted.Length -gt 0 -and $expectedCompacted.Equals($actualCompacted, [StringComparison]::OrdinalIgnoreCase) -and !(Normalize-Text $Expected).Equals($actualNormalized, [StringComparison]::OrdinalIgnoreCase))
        score = [Math]::Round($preserved / [double]($expectedWords.Count - 1), 4)
    }
}

function Get-SpacingPhraseResults([string[]]$Phrases, [string]$Actual) {
    $actualNormalized = Normalize-Text $Actual
    $actualCompacted = $actualNormalized -replace "\s+", ""
    $results = @()
    foreach ($phrase in $Phrases) {
        $normalizedPhrase = Normalize-Text $phrase
        if ([string]::IsNullOrWhiteSpace($normalizedPhrase) -or $normalizedPhrase -notmatch "\S\s+\S") {
            continue
        }

        $phraseCompacted = $normalizedPhrase -replace "\s+", ""
        $results += [pscustomobject]@{
            phrase = $normalizedPhrase
            spacedFound = $actualNormalized.IndexOf($normalizedPhrase, [StringComparison]::OrdinalIgnoreCase) -ge 0
            compactedFound = $actualCompacted.IndexOf($phraseCompacted, [StringComparison]::OrdinalIgnoreCase) -ge 0
        }
    }

    return $results
}

if (!(Test-Path $root)) {
    & (Join-Path $PSScriptRoot "generate-private-fixtures.ps1") -FixtureRoot $FixtureRoot
}

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
Invoke-Native { & $dotnet build (Join-Path $repoRoot "src/OcrSnip.Tools.OcrCli/OcrSnip.Tools.OcrCli.csproj") -c Release }

$results = @()
$gateFailures = @()
foreach ($image in Get-ChildItem $root -Filter "*.png" | Sort-Object Name) {
    $expectedPath = [System.IO.Path]::ChangeExtension($image.FullName, ".expected.txt")
    $metaPath = [System.IO.Path]::ChangeExtension($image.FullName, ".meta.json")
    $expected = (Get-Content $expectedPath -Raw).Trim()
    $meta = $null
    if (Test-Path $metaPath) {
        $meta = Get-Content $metaPath -Raw | ConvertFrom-Json
    }

    $jsonText = & $dotnet run --project (Join-Path $repoRoot "src/OcrSnip.Tools.OcrCli/OcrSnip.Tools.OcrCli.csproj") -c Release -- $image.FullName --json
    $ocrExitCode = $LASTEXITCODE
    if ($ocrExitCode -ne 0 -and [string]::IsNullOrWhiteSpace(($jsonText -join ""))) {
        throw "OCR CLI failed for $($image.Name)."
    }

    $actual = $jsonText | ConvertFrom-Json

    $primaryTokens = @()
    $noiseTokens = @()
    $spacingPhrases = @()
    $requirePrimary = $true
    $category = "uncategorized"
    $risk = ""
    $shouldWarnEdge = $false
    if ($null -ne $meta) {
        $primaryTokens = @($meta.primaryTokens)
        $noiseTokens = @($meta.noiseTokens)
        $spacingPhrases = @($meta.spacingPhrases)
        $requirePrimary = [bool]$meta.requirePrimary
        $category = [string]$meta.category
        $risk = [string]$meta.risk
        $shouldWarnEdge = [bool]$meta.shouldWarnEdge
    }

    if ($primaryTokens.Count -eq 0) {
        $primaryTokens = @($expected)
    }

    if ([string]::IsNullOrWhiteSpace($actual.text) -and $requirePrimary) {
        $gateFailures += [pscustomobject]@{
            image = $image.Name
            gate = "empty"
            message = "Produced no OCR text"
            actual = ""
        }
    }

    if ($spacingPhrases.Count -eq 0 -and $expected -match "\S\s+\S" -and $category -notin @("dense", "clipped", "edge_noise")) {
        $spacingPhrases = @($expected)
    }

    $primaryHits = @()
    foreach ($token in $primaryTokens) {
        $primaryHits += [pscustomobject]@{
            token = $token
            found = Test-ContainsToken $actual.text $token
        }
    }

    $noiseHits = @()
    foreach ($token in $noiseTokens) {
        $noiseHits += [pscustomobject]@{
            token = $token
            found = Test-ContainsToken $actual.text $token
        }
    }

    $missingPrimary = @($primaryHits | Where-Object { !$_.found })
    $primaryPass = $missingPrimary.Count -eq 0
    if ($requirePrimary -and $missingPrimary.Count -gt 0) {
        $gateFailures += [pscustomobject]@{
            image = $image.Name
            gate = "primary"
            message = "Missed primary OCR token(s): $($missingPrimary.token -join ', ')"
            actual = $actual.text
        }
    }

    $spacing = Get-WordBoundaryStats $expected $actual.text
    $spacingPhraseResults = @(Get-SpacingPhraseResults $spacingPhrases $actual.text)
    $spacingPass = !($spacingPhraseResults | Where-Object { $_.compactedFound -and !$_.spacedFound })
    if (!$spacingPass -and $category -in @("baseline", "dense", "messy")) {
        $failedPhrase = @($spacingPhraseResults | Where-Object { $_.compactedFound -and !$_.spacedFound } | Select-Object -First 1).phrase
        $gateFailures += [pscustomobject]@{
            image = $image.Name
            gate = "spacing"
            message = "Failed spacing preservation for '$failedPhrase'"
            actual = $actual.text
        }
    }

    $results += [pscustomobject]@{
        image = $image.Name
        category = $category
        risk = $risk
        expected = $expected
        actual = $actual.text
        elapsedMs = $actual.elapsedMs
        cer = Get-Cer $expected $actual.text
        exact = ($expected -eq $actual.text)
        primaryHitRate = if ($primaryHits.Count -eq 0) { 1 } else { [Math]::Round(@($primaryHits | Where-Object found).Count / [double]$primaryHits.Count, 4) }
        primaryPass = $primaryPass
        noiseHitRate = if ($noiseHits.Count -eq 0) { 0 } else { [Math]::Round(@($noiseHits | Where-Object found).Count / [double]$noiseHits.Count, 4) }
        primaryHits = $primaryHits
        noiseHits = $noiseHits
        spacing = $spacing
        spacingPhrases = $spacingPhraseResults
        spacingPass = $spacingPass
        shouldWarnEdge = $shouldWarnEdge
        diagnostics = $actual.diagnostics
        lines = $actual.lines
        gateFailures = @($gateFailures | Where-Object image -eq $image.Name)
    }
}

$results | ConvertTo-Json -Depth 4 | Set-Content $output
$total = @($results).Count
$exact = @($results | Where-Object exact).Count
$spacingChecked = @($results | Where-Object { $_.spacing.expectedBoundaryCount -gt 0 }).Count
$edgeRisk = @($results | Where-Object shouldWarnEdge).Count
Write-Host "OCR fixture results written to $output"
Write-Host "Fixtures: $total total, $exact exact expected-text matches, $spacingChecked spacing-sensitive, $edgeRisk edge-risk cases"
$results | Group-Object category | Sort-Object Name | ForEach-Object {
    $group = @($_.Group)
    $avgCer = [Math]::Round((($group | Measure-Object cer -Average).Average), 4)
    $avgPrimary = [Math]::Round((($group | Measure-Object primaryHitRate -Average).Average), 4)
    Write-Host "  $($_.Name): $($group.Count) cases, avg CER $avgCer, avg primary hit $avgPrimary"
}

if ($gateFailures.Count -gt 0) {
    $failureOutput = [System.IO.Path]::ChangeExtension($output, ".failures.json")
    $gateFailures | ConvertTo-Json -Depth 4 | Set-Content $failureOutput
    Write-Host "Fixture gate failures written to $failureOutput"
    foreach ($failure in $gateFailures | Select-Object -First 10) {
        Write-Host "  FAIL $($failure.image) [$($failure.gate)]: $($failure.message)"
    }

    if ($gateFailures.Count -gt 10) {
        Write-Host "  ... plus $($gateFailures.Count - 10) more"
    }

    throw "$($gateFailures.Count) OCR fixture gate failure(s)."
}
