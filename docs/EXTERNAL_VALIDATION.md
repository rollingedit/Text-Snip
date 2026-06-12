# External Release Validation

Some OCR Snip ship gates require hardware, OS, display, account, or reboot states that cannot be proven by normal unit tests. Use this checklist to fill `artifacts/reports/external-validation.json`, then run:

```powershell
powershell -ExecutionPolicy Bypass -File tools\verify-ship-readiness.ps1
```

Generate a blank evidence file with:

```powershell
powershell -ExecutionPolicy Bypass -File tools\verify-ship-readiness.ps1 -CreateTemplate
```

Each entry must set `passed` to `true` and include a short `evidence` note with the machine/run used. Keep the generated JSON out of commits.

The profile runner is the preferred entry point for validation machines:

```powershell
powershell -ExecutionPolicy Bypass -File tools\invoke-external-validation-profile.ps1 -Profile CurrentMachineFull
powershell -ExecutionPolicy Bypass -File tools\invoke-external-validation-profile.ps1 -Profile Windows10Amd
powershell -ExecutionPolicy Bypass -File tools\invoke-external-validation-profile.ps1 -Profile Dpi125
powershell -ExecutionPolicy Bypass -File tools\invoke-external-validation-profile.ps1 -Profile Dpi150
powershell -ExecutionPolicy Bypass -File tools\invoke-external-validation-profile.ps1 -Profile MixedNegativeMultiMonitor
powershell -ExecutionPolicy Bypass -File tools\invoke-external-validation-profile.ps1 -Profile Admin
powershell -ExecutionPolicy Bypass -File tools\invoke-external-validation-profile.ps1 -Profile Status
powershell -ExecutionPolicy Bypass -File tools\invoke-external-validation-profile.ps1 -Profile Export
powershell -ExecutionPolicy Bypass -File tools\invoke-external-validation-profile.ps1 -Profile Tooling
```

Run the `Tooling` profile after changing validation scripts. It uses temporary ignored evidence files and verifies parser checks, manifest integrity, readiness validation, import rejection, import success, and status generation.

The same profiles can be run from GitHub Actions with the `External Validation` workflow. Use self-hosted Windows runners for hardware, admin, reboot, and display-layout evidence; the workflow uploads `external-validation-export.zip` when evidence is available.

If the target machine does not have the repo or .NET SDK, create a portable validation kit on the development machine:

```powershell
powershell -ExecutionPolicy Bypass -File tools\create-external-validation-kit.ps1
```

Copy `artifacts\publish\OcrSnip-ExternalValidationKit.zip` to the target Windows machine, extract it, and run the kit runner from the extracted folder:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run-external-validation-kit.ps1 -ExpectedWindows Windows10 -IncludeDesktopHotkey -IncludeHotkeyConflict
```

The kit writes `artifacts\reports\external-validation-export.zip`, which can be imported directly back in this repo.
For DPI gates, include `-IncludeDesktopHotkey`; the kit records DPI evidence only when a real desktop hotkey snip passes at the detected scale.

To merge evidence collected on another machine:

```powershell
powershell -ExecutionPolicy Bypass -File tools\import-external-validation.ps1 -SourcePath path\to\external-validation.json
```

The importer also accepts the ZIP produced by the `Export` profile or uploaded by the `External Validation` GitHub Actions workflow:

```powershell
powershell -ExecutionPolicy Bypass -File tools\import-external-validation.ps1 -SourcePath path\to\external-validation-export.zip
```

To create a small transfer ZIP containing only external validation evidence and status:

```powershell
powershell -ExecutionPolicy Bypass -File tools\export-external-validation.ps1
```

To run the full local stack and record any gates satisfied by the current machine:

```powershell
powershell -ExecutionPolicy Bypass -File tools\record-external-validation.ps1 -RunChecks
```

For checks that require human confirmation after changing the environment, add the relevant flag:

```powershell
powershell -ExecutionPolicy Bypass -File tools\record-external-validation.ps1 -RunChecks -PostRebootHotkeyPassed -MultiMonitorCapturePassed
```

For post-reboot hotkey validation, register the one-time logon task, reboot, sign in, and let the task update the evidence file:

```powershell
powershell -ExecutionPolicy Bypass -File tools\prepare-post-reboot-validation.ps1
```

For DPI, mixed-DPI, negative-coordinate, and multi-monitor runs, use the display validator after setting up the target display layout:

```powershell
powershell -ExecutionPolicy Bypass -File tools\validate-display-environment.ps1 -ExpectedDpiScale 125
powershell -ExecutionPolicy Bypass -File tools\validate-display-environment.ps1 -ExpectedDpiScale 150
powershell -ExecutionPolicy Bypass -File tools\validate-display-environment.ps1 -ExpectedDpiScale 100 -RequireMixedDpi -RequireNegativeVirtualMonitor -MultiMonitorCapturePassed
```

To test capture on every currently connected monitor without recording multi-monitor evidence:

```powershell
powershell -ExecutionPolicy Bypass -File tools\verify-monitor-capture.ps1
```

For admin-account validation, run this from an elevated PowerShell session:

```powershell
powershell -ExecutionPolicy Bypass -File tools\validate-admin-environment.ps1 -RunChecks
```

For OS and CPU matrix validation, assert the expected machine profile before recording evidence:

```powershell
powershell -ExecutionPolicy Bypass -File tools\validate-machine-environment.ps1 -ExpectedWindows Windows10 -ExpectedCpuVendor AMD -RunChecks
powershell -ExecutionPolicy Bypass -File tools\validate-machine-environment.ps1 -ExpectedWindows Windows11 -ExpectedCpuVendor Intel -RunChecks
```

## Required Gates

- `windows10x64`: Run the automated stack on Windows 10 x64.
- `amdCpu`: Run the automated stack on an AMD x64 CPU.
- `dpi100`: Verify hotkey snip at 100% display scaling.
- `dpi125`: Verify hotkey snip at 125% display scaling.
- `dpi150`: Verify hotkey snip at 150% display scaling.
- `mixedDpi`: Verify hotkey snip with at least two monitors using different scaling.
- `negativeVirtualMonitor`: Verify hotkey snip with a monitor positioned left of or above the primary display.
- `lightTheme`: Verify app launch, settings, toast, and hotkey snip in Windows light theme.
- `darkTheme`: Verify app launch, settings, toast, and hotkey snip in Windows dark theme.
- `standardAccount`: Run release verification from a non-elevated standard account.
- `adminAccount`: Run release verification from an elevated admin account.
- `postRebootHotkey`: Enable launch at login, reboot, sign in, and verify `Ctrl+Shift+O` starts a snip.
- `multiMonitorCapture`: Select text on each monitor and across monitor boundaries where the layout permits; verify copied text matches the target.
- `intelModelLoad`: Run OCR self-tests or fixture OCR on an Intel x64 CPU.
- `amdModelLoad`: Run OCR self-tests or fixture OCR on an AMD x64 CPU.

## Recommended Command Set

Run this on each validation machine/profile where possible:

```powershell
powershell -ExecutionPolicy Bypass -File tools\verify-release.ps1 -IncludeDesktopHotkey -IncludeHotkeyConflict -IncludeThemeModes
powershell -ExecutionPolicy Bypass -File tools\run-ocr-fixtures.ps1
powershell -ExecutionPolicy Bypass -File tools\compare-paddle-reference.ps1
powershell -ExecutionPolicy Bypass -File tools\verify-privacy.ps1
powershell -ExecutionPolicy Bypass -File tools\verify-no-runtime-network.ps1
powershell -ExecutionPolicy Bypass -File tools\verify-installer.ps1
```
