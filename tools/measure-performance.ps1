param(
    [string]$OutputPath = "artifacts/reports/performance-summary.txt"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnet = Join-Path $repoRoot ".dotnet/dotnet.exe"
$output = Join-Path $repoRoot $OutputPath

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null

$test = & $dotnet test (Join-Path $repoRoot "src/OcrSnip.Tests/OcrSnip.Tests.csproj") `
    -c Release `
    --filter "FullyQualifiedName~OcrSmokeTests" `
    --logger "console;verbosity=minimal" 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "OCR performance smoke test failed with exit code $LASTEXITCODE"
}

$test | Set-Content $output
Write-Host "Performance smoke output written to $output"
