param(
    [string]$FixtureResultsPath = "artifacts/reports/fixture-results.json",
    [string]$PaddleReferenceRoot = "artifacts/reports/paddle-reference",
    [string]$OutputPath = "artifacts/reports/paddle-comparison.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$fixtureResultsFile = Join-Path $repoRoot $FixtureResultsPath
$referenceRoot = Join-Path $repoRoot $PaddleReferenceRoot
$output = Join-Path $repoRoot $OutputPath

if (!(Test-Path $fixtureResultsFile)) {
    & (Join-Path $PSScriptRoot "run-ocr-fixtures.ps1")
}

if (!(Test-Path $referenceRoot)) {
    throw "Paddle reference root not found: $referenceRoot"
}

$fixtureResults = Get-Content $fixtureResultsFile -Raw | ConvertFrom-Json
$comparisons = @()
foreach ($fixture in $fixtureResults) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($fixture.image)
    $referencePath = Join-Path $referenceRoot "$baseName.json"
    if (!(Test-Path $referencePath)) {
        throw "Missing Paddle reference JSON: $referencePath"
    }

    $reference = Get-Content $referencePath -Raw | ConvertFrom-Json
    $paddleText = ($reference[0].rec_texts -join [Environment]::NewLine).Trim()
    $csharpText = ($fixture.actual).Trim()
    $matches = $paddleText -eq $csharpText
    $comparisons += [pscustomobject]@{
        image = $fixture.image
        csharp = $csharpText
        paddle = $paddleText
        matches = $matches
    }

    if (!$matches) {
        throw "Paddle parity mismatch for $($fixture.image): C#='$csharpText' Paddle='$paddleText'"
    }
}

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
$comparisons | ConvertTo-Json -Depth 4 | Set-Content $output
$matched = @($comparisons | Where-Object matches).Count
$total = @($comparisons).Count
Write-Host "Paddle parity comparison written to $output ($matched/$total exact matches)"
