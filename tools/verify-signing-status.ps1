param(
    [string]$OutputPath = "artifacts/reports/signing-status.txt"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$output = Join-Path $repoRoot $OutputPath
$app = Join-Path $repoRoot "artifacts/publish/OcrSnip/OcrSnip.App.exe"
$installer = Join-Path $repoRoot "installer/Output/Text-Snip-Setup-x64.exe"

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
$targets = @($app, $installer) | Where-Object { Test-Path $_ }
$lines = @()
foreach ($target in $targets) {
    $signature = Get-AuthenticodeSignature $target
    $lines += "$target"
    $lines += "  Status: $($signature.Status)"
    $lines += "  Signer: $($signature.SignerCertificate.Subject)"
}

if (!$targets) {
    $lines += "No release binaries found. Run publish/build-installer first."
}

$lines | Set-Content $output
Write-Host "Signing status written to $output"
