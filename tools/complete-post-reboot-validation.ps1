param(
    [string]$TaskName = "OcrSnipPostRebootValidation",
    [string]$OutputPath = "artifacts/reports/post-reboot-validation.txt",
    [switch]$AllowHostInputAutomation
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$output = Join-Path $repoRoot $OutputPath
. (Join-Path $PSScriptRoot "HostInputAutomationGuard.ps1")
Assert-HostInputAutomationAllowed -AllowedBySwitch ([bool]$AllowHostInputAutomation) -Reason "complete-post-reboot-validation.ps1 sends the desktop hotkey and drags a real selection after login."
New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null

$lines = @("Generated: $(Get-Date -Format o)")
try {
    & (Join-Path $PSScriptRoot "publish.ps1") | Out-Null
    & (Join-Path $PSScriptRoot "verify-hotkey-snip.ps1") -AllowHostInputAutomation:$AllowHostInputAutomation | Out-Null
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
