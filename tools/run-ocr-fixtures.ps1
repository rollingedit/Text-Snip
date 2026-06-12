param(
    [string]$FixtureRoot = "Fixtures/generated",
    [string]$OutputPath = "artifacts/reports/fixture-results.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$root = Join-Path $repoRoot $FixtureRoot
$output = Join-Path $repoRoot $OutputPath
$dotnet = Join-Path $repoRoot ".dotnet/dotnet.exe"

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

if (!(Test-Path $root)) {
    & (Join-Path $PSScriptRoot "generate-private-fixtures.ps1") -FixtureRoot $FixtureRoot
}

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
Invoke-Native { & $dotnet build (Join-Path $repoRoot "src/OcrSnip.Tools.OcrCli/OcrSnip.Tools.OcrCli.csproj") -c Release }

$results = @()
foreach ($image in Get-ChildItem $root -Filter "*.png" | Sort-Object Name) {
    $expectedPath = [System.IO.Path]::ChangeExtension($image.FullName, ".expected.txt")
    $expected = (Get-Content $expectedPath -Raw).Trim()
    $jsonText = & $dotnet run --project (Join-Path $repoRoot "src/OcrSnip.Tools.OcrCli/OcrSnip.Tools.OcrCli.csproj") -c Release -- $image.FullName --json
    if ($LASTEXITCODE -ne 0) {
        throw "OCR CLI failed for $($image.Name)."
    }

    $actual = $jsonText | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($actual.text)) {
        throw "Fixture $($image.Name) produced no OCR text."
    }

    $results += [pscustomobject]@{
        image = $image.Name
        expected = $expected
        actual = $actual.text
        elapsedMs = $actual.elapsedMs
        cer = Get-Cer $expected $actual.text
        exact = ($expected -eq $actual.text)
    }
}

$results | ConvertTo-Json -Depth 4 | Set-Content $output
Write-Host "OCR fixture results written to $output"
