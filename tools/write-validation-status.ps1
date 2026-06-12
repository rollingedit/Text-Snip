param(
    [string]$EvidencePath = "artifacts/reports/external-validation.json",
    [string]$OutputPath = "artifacts/reports/validation-status.md"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$evidenceFile = Join-Path $repoRoot $EvidencePath
$output = Join-Path $repoRoot $OutputPath

if (!(Test-Path $evidenceFile)) {
    & (Join-Path $PSScriptRoot "verify-ship-readiness.ps1") -CreateTemplate
}

$evidence = Get-Content $evidenceFile -Raw | ConvertFrom-Json
$lines = @(
    "# OCR Snip Validation Status",
    "",
    "Generated: $(Get-Date -Format o)",
    "",
    "| Gate | Status | Evidence | Verified At |",
    "| --- | --- | --- | --- |"
)

foreach ($property in $evidence.PSObject.Properties | Sort-Object Name) {
    $entry = $property.Value
    $status = if ($entry.passed -eq $true) { "PASS" } else { "MISSING" }
    $evidenceText = ([string]$entry.evidence).Replace("|", "\|")
    $verifiedAt = ([string]$entry.verifiedAt).Replace("|", "\|")
    $lines += "| $($property.Name) | $status | $evidenceText | $verifiedAt |"
}

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
$lines | Set-Content $output
Write-Host "Validation status written to $output"
