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
