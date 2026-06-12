param(
    [string]$EvidencePath = "artifacts/reports/external-validation.json",
    [string]$OutputPath = "artifacts/reports/external-validation-export.zip"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$evidenceFile = Join-Path $repoRoot $EvidencePath
$output = Join-Path $repoRoot $OutputPath
$stagingRoot = Join-Path $repoRoot "artifacts/reports/external-validation-export"

if (!(Test-Path $evidenceFile)) {
    throw "Evidence file missing: $evidenceFile"
}

& (Join-Path $PSScriptRoot "write-validation-status.ps1") | Out-Null
$statusFile = Join-Path $repoRoot "artifacts/reports/validation-status.md"

Remove-Item -Recurse -Force $stagingRoot -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
Copy-Item -LiteralPath $evidenceFile -Destination (Join-Path $stagingRoot "external-validation.json")
Copy-Item -LiteralPath $statusFile -Destination (Join-Path $stagingRoot "validation-status.md")

if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Force
}

Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $output -Force
Remove-Item -Recurse -Force $stagingRoot
Write-Host "External validation export written to $output"
