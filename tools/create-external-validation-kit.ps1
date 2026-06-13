param(
    [string]$OutputPath = "artifacts/publish/OcrSnip-ExternalValidationKit.zip",
    [string]$StagingRoot = "artifacts/external-validation-kit"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$output = Join-Path $repoRoot $OutputPath
$staging = Join-Path $repoRoot $StagingRoot
$publishRoot = Join-Path $repoRoot "artifacts/publish/OcrSnip"
$fixtureRoot = Join-Path $repoRoot "Fixtures/generated"
$fixture = Join-Path $fixtureRoot "simple_text.png"

if (!(Test-Path (Join-Path $publishRoot "OcrSnip.App.exe"))) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

if (!(Test-Path $fixture)) {
    & (Join-Path $PSScriptRoot "generate-private-fixtures.ps1")
}

Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $staging | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $staging "Fixtures") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $staging "tools") | Out-Null

Copy-Item -Recurse -LiteralPath $publishRoot -Destination (Join-Path $staging "OcrSnip")
Copy-Item -LiteralPath $fixture -Destination (Join-Path $staging "Fixtures/simple_text.png")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "run-external-validation-kit.ps1") -Destination (Join-Path $staging "tools/run-external-validation-kit.ps1")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "validation-gates.json") -Destination (Join-Path $staging "tools/validation-gates.json")

Set-Content -LiteralPath (Join-Path $staging "autorun.inf") -Value @(
    "[AutoRun]",
    "label=OCR Snip Validation",
    "open=Run-Windows10.cmd",
    "action=Run OCR Snip Windows 10 validation"
)

Set-Content -LiteralPath (Join-Path $staging "START-HERE.txt") -Value @(
    "OCR Snip External Validation Kit",
    "",
    "Run the launcher that matches this machine:",
    "",
    "- Run-Windows10.cmd for Windows 10 validation",
    "- Run-AMD.cmd for AMD CPU validation",
    "- Run-Admin-Elevated.cmd from an elevated session for admin validation",
    "- Prepare-PostReboot.cmd to prepare the reboot persistence check",
    "- Run-DPI-125.cmd or Run-DPI-150.cmd for DPI validation",
    "- Run-MixedDPI-MultiMonitor.cmd after arranging mixed-DPI/multi-monitor validation",
    "",
    "When this kit is run from a mounted ISO, evidence is written to:",
    "Desktop\\OcrSnipExternalValidation\\artifacts\\reports\\external-validation-export.zip"
)

Set-Content -LiteralPath (Join-Path $staging "Run-Windows10.cmd") -Value @(
    "@echo off",
    "powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0tools\run-external-validation-kit.ps1"" -ExpectedWindows Windows10 -IncludeDesktopHotkey -IncludeHotkeyConflict",
    "pause"
)

Set-Content -LiteralPath (Join-Path $staging "Run-AMD.cmd") -Value @(
    "@echo off",
    "powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0tools\run-external-validation-kit.ps1"" -ExpectedCpuVendor AMD -IncludeDesktopHotkey -IncludeHotkeyConflict",
    "pause"
)

Set-Content -LiteralPath (Join-Path $staging "Run-Admin-Elevated.cmd") -Value @(
    "@echo off",
    "powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0tools\run-external-validation-kit.ps1"" -RequireAdminAccount -IncludeDesktopHotkey -IncludeHotkeyConflict",
    "pause"
)

Set-Content -LiteralPath (Join-Path $staging "Prepare-PostReboot.cmd") -Value @(
    "@echo off",
    "powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0tools\run-external-validation-kit.ps1"" -PreparePostRebootValidation",
    "pause"
)

Set-Content -LiteralPath (Join-Path $staging "Run-DPI-125.cmd") -Value @(
    "@echo off",
    "powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0tools\run-external-validation-kit.ps1"" -ExpectedDpiScale 125 -IncludeDesktopHotkey",
    "pause"
)

Set-Content -LiteralPath (Join-Path $staging "Run-DPI-150.cmd") -Value @(
    "@echo off",
    "powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0tools\run-external-validation-kit.ps1"" -ExpectedDpiScale 150 -IncludeDesktopHotkey",
    "pause"
)

Set-Content -LiteralPath (Join-Path $staging "Run-MixedDPI-MultiMonitor.cmd") -Value @(
    "@echo off",
    "powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0tools\run-external-validation-kit.ps1"" -RequireMixedDpi -RequireNegativeVirtualMonitor -RequireMultiMonitorCapture -MultiMonitorCapturePassed -IncludeDesktopHotkey",
    "pause"
)

Set-Content -LiteralPath (Join-Path $staging "README.md") -Value @(
    "# OCR Snip External Validation Kit",
    "",
    "Run this from an interactive PowerShell session on the validation machine:",
    "",
    '````powershell',
    "powershell -ExecutionPolicy Bypass -File tools\run-external-validation-kit.ps1 -ExpectedWindows Windows10 -IncludeDesktopHotkey -IncludeHotkeyConflict",
    '````',
    "",
    "For AMD validation, add:",
    "",
    '````powershell',
    "-ExpectedCpuVendor AMD",
    '````',
    "",
    "For DPI validation, run from the target DPI layout and include the desktop hotkey check:",
    "",
    '````powershell',
    "-ExpectedDpiScale 125 -IncludeDesktopHotkey",
    "-ExpectedDpiScale 150 -IncludeDesktopHotkey",
    "-RequireMixedDpi -RequireNegativeVirtualMonitor -RequireMultiMonitorCapture -MultiMonitorCapturePassed -IncludeDesktopHotkey",
    '````',
    "",
    'The runner writes `artifacts\reports\external-validation-export.zip`.',
    'When run from the ISO/CD drive, the runner writes to `Desktop\OcrSnipExternalValidation\artifacts\reports\external-validation-export.zip` because the mounted ISO is read-only.',
    "Import that ZIP back in the repo with:",
    "",
    '````powershell',
    "powershell -ExecutionPolicy Bypass -File tools\import-external-validation.ps1 -SourcePath path\to\external-validation-export.zip",
    '````',
    "",
    "For post-reboot hotkey validation, prepare the one-time task, reboot, sign in, and wait for the task to write the export ZIP:",
    "",
    '````powershell',
    "powershell -ExecutionPolicy Bypass -File tools\run-external-validation-kit.ps1 -PreparePostRebootValidation",
    '````',
    "",
    "For elevated admin validation, run PowerShell as administrator and add:",
    "",
    '````powershell',
    "-RequireAdminAccount",
    '````',
    "",
    "Convenience launchers are included for common runs:",
    "",
    '- `Run-Windows10.cmd`',
    '- `Run-AMD.cmd`',
    '- `Run-Admin-Elevated.cmd`',
    '- `Prepare-PostReboot.cmd`',
    '- `Run-DPI-125.cmd`',
    '- `Run-DPI-150.cmd`',
    '- `Run-MixedDPI-MultiMonitor.cmd`'
)

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Force
}

Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $output -Force
Write-Host "External validation kit written to $output"
