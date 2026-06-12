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
    $expectedTokens = $expected -split '[\\\s:._-]+' | Where-Object { $_ }
    foreach ($token in $expectedTokens) {
        if ($actual.text -notmatch [regex]::Escape($token)) {
            throw "Fixture $($image.Name) missing expected token '$token'. Actual text: $($actual.text)"
        }
    }

    $results += [pscustomobject]@{
        image = $image.Name
        expected = $expected
        actual = $actual.text
        elapsedMs = $actual.elapsedMs
    }
}

$results | ConvertTo-Json -Depth 4 | Set-Content $output
Write-Host "OCR fixture results written to $output"
