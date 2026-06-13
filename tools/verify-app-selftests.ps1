param(
    [string]$FixturePath = "Fixtures/generated/simple_text.png",
    [string]$ExpectedText = "OCR TEST",
    [string]$ModelRoot = "assets/models"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnet = Join-Path $repoRoot ".dotnet/dotnet.exe"
if (!(Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

$fixture = Join-Path $repoRoot $FixturePath
$modelRootPath = Join-Path $repoRoot $ModelRoot
$cliProject = Join-Path $repoRoot "src/OcrSnip.Tools.OcrCli/OcrSnip.Tools.OcrCli.csproj"

if (!(Test-Path $fixture)) {
    & (Join-Path $PSScriptRoot "generate-private-fixtures.ps1")
}

& (Join-Path $PSScriptRoot "verify-models.ps1")

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
    $output = & $dotnet run --project $cliProject -c Release -- $fixture --model-root $modelRootPath 2>&1
    $exitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}

if ($exitCode -ne 0) {
    throw "OCR CLI fixture verification failed with exit code $exitCode.`n$output"
}

if (($output -join "`n") -notmatch [regex]::Escape($ExpectedText)) {
    throw "OCR CLI fixture verification did not output expected text '$ExpectedText'.`n$output"
}

Write-Host "OCR CLI verification passed."
