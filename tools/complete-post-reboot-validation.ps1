param(
    [string]$TaskName = "OcrSnipPostRebootValidation",
    [string]$OutputPath = "artifacts/reports/post-reboot-validation.txt"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$output = Join-Path $repoRoot $OutputPath
New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null

$lines = @("Generated: $(Get-Date -Format o)")
try {
    & (Join-Path $PSScriptRoot "publish.ps1") | Out-Null
    & (Join-Path $PSScriptRoot "verify-hotkey-snip.ps1") | Out-Null
    & (Join-Path $PSScriptRoot "record-external-validation.ps1") -PostRebootHotkeyPassed | Out-Null
    $lines += "postRebootHotkey: passed"
    Write-Host "Post-reboot hotkey validation passed."
}
catch {
    $lines += "postRebootHotkey: failed"
    $lines += $_.Exception.Message
    throw
}
finally {
    $lines | Set-Content $output
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
}
