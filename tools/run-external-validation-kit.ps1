param(
    [ValidateSet("Windows10", "Windows11")]
    [string]$ExpectedWindows,
    [ValidateSet("Intel", "AMD")]
    [string]$ExpectedCpuVendor,
    [ValidateSet(100, 125, 150)]
    [int]$ExpectedDpiScale,
    [switch]$RequireMixedDpi,
    [switch]$RequireNegativeVirtualMonitor,
    [switch]$RequireAdminAccount,
    [switch]$RequirePostRebootHotkey,
    [switch]$RequireMultiMonitorCapture,
    [switch]$IncludeDesktopHotkey,
    [switch]$IncludeHotkeyConflict,
    [switch]$PreparePostRebootValidation,
    [switch]$CompletePostRebootValidation,
    [switch]$PostRebootHotkeyPassed,
    [switch]$MultiMonitorCapturePassed,
    [string]$TaskName = "OcrSnipKitPostRebootValidation",
    [string]$OutputRoot = "artifacts/reports"
)

$ErrorActionPreference = "Stop"
$kitRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appRoot = Join-Path $kitRoot "OcrSnip"
$exe = Join-Path $appRoot "OcrSnip.App.exe"
$fixture = Join-Path $kitRoot "Fixtures/simple_text.png"
$gateManifest = Join-Path $PSScriptRoot "validation-gates.json"

function Resolve-OutputDirectory([string]$Root) {
    if ([System.IO.Path]::IsPathRooted($Root)) {
        return $Root
    }

    $kitDrive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($kitRoot.Path))
    if ($kitDrive.DriveType -eq [System.IO.DriveType]::CDRom) {
        $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
        return Join-Path $desktop (Join-Path "OcrSnipExternalValidation" $Root)
    }

    return Join-Path $kitRoot $Root
}

$outputDirectory = Resolve-OutputDirectory $OutputRoot
$evidenceFile = Join-Path $outputDirectory "external-validation.json"
$statusFile = Join-Path $outputDirectory "validation-status.md"
$metadataFile = Join-Path $outputDirectory "validation-run-metadata.json"
$exportZip = Join-Path $outputDirectory "external-validation-export.zip"

function Assert-Exists([string]$Path) {
    if (!(Test-Path $Path)) {
        throw "Required validation kit path missing: $Path"
    }
}

function New-BlankEvidence($Gates) {
    $data = [ordered]@{}
    foreach ($gate in $Gates) {
        $data[$gate] = [ordered]@{
            passed = $false
            evidence = ""
            verifiedAt = ""
        }
    }

    return $data
}

function Convert-ToHashtable($InputObject) {
    $hash = [ordered]@{}
    foreach ($property in $InputObject.PSObject.Properties) {
        if ($property.Value -is [pscustomobject]) {
            $hash[$property.Name] = Convert-ToHashtable $property.Value
        }
        else {
            $hash[$property.Name] = $property.Value
        }
    }

    return $hash
}

function Set-Gate($Data, [string]$Gate, [string]$Evidence) {
    $Data[$Gate] = [ordered]@{
        passed = $true
        evidence = $Evidence
        verifiedAt = Get-Date -Format o
    }
}

function Assert-GatePassed($Data, [string]$Gate, [string]$Message) {
    if ($Data[$Gate].passed -ne $true) {
        throw $Message
    }
}

function Wait-ClipboardContains([string]$ExpectedText, [int]$TimeoutSeconds = 5) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $clipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
        if ($clipboard -match [regex]::Escape($ExpectedText)) {
            return $true
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    return $false
}

function Invoke-AppSelfTests {
    $startup = Start-Process -FilePath $exe -ArgumentList "--self-test-startup" -Wait -PassThru -WindowStyle Hidden
    if ($startup.ExitCode -ne 0) {
        throw "Startup self-test failed with exit code $($startup.ExitCode)."
    }

    $ocr = Start-Process -FilePath $exe -ArgumentList @("--self-test-ocr", $fixture) -Wait -PassThru -WindowStyle Hidden
    if ($ocr.ExitCode -ne 0) {
        throw "OCR self-test failed with exit code $($ocr.ExitCode)."
    }

    if (!(Wait-ClipboardContains "OCR TEST")) {
        throw "OCR self-test did not place expected text on clipboard."
    }
}

function Test-IdleNoNetwork([int]$Seconds = 5) {
    $process = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds $Seconds
    try {
        $connections = Get-NetTCPConnection -OwningProcess $process.Id -ErrorAction SilentlyContinue |
            Where-Object { $_.State -in @("Established", "SynSent", "Listen") }
        if ($connections) {
            throw "Runtime network sockets detected for OcrSnip.App process."
        }
    }
    finally {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}

function Test-DesktopHotkey([string]$ExpectedText = "OCR TEST", [int]$TimeoutSeconds = 15, [switch]$UseExistingApp) {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ExternalValidationInputNative {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
"@

    $formScript = @"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
`$form = New-Object Windows.Forms.Form
`$form.Text = 'OCR Snip Test Target'
`$form.StartPosition = 'Manual'
`$form.Location = New-Object Drawing.Point(100,100)
`$form.Size = New-Object Drawing.Size(900,260)
`$form.TopMost = `$true
`$form.BackColor = [Drawing.Color]::White
`$label = New-Object Windows.Forms.Label
`$label.Text = '$ExpectedText'
`$label.Font = New-Object Drawing.Font('Arial', 54, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
`$label.AutoSize = `$true
`$label.Location = New-Object Drawing.Point(40,70)
`$form.Controls.Add(`$label)
[Windows.Forms.Application]::Run(`$form)
"@

    $target = Start-Process powershell -ArgumentList @("-NoProfile", "-STA", "-Command", $formScript) -PassThru
    $app = $null
    try {
        Start-Sleep -Seconds 2
        Set-Clipboard -Value "__OCR_SNIP_PENDING__"
        if ($UseExistingApp) {
            $app = Get-Process -Name "OcrSnip.App" -ErrorAction SilentlyContinue | Select-Object -First 1
            if (!$app) {
                throw "OcrSnip.App was not already running after login."
            }
        }
        else {
            $app = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
            Start-Sleep -Seconds 3
        }

        [ExternalValidationInputNative]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
        [ExternalValidationInputNative]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero)
        [ExternalValidationInputNative]::keybd_event(0x4F, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 80
        [ExternalValidationInputNative]::keybd_event(0x4F, 0, 2, [UIntPtr]::Zero)
        [ExternalValidationInputNative]::keybd_event(0x10, 0, 2, [UIntPtr]::Zero)
        [ExternalValidationInputNative]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)

        Start-Sleep -Seconds 1
        [ExternalValidationInputNative]::SetCursorPos(130, 155) | Out-Null
        Start-Sleep -Milliseconds 100
        [ExternalValidationInputNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 100
        [ExternalValidationInputNative]::SetCursorPos(620, 245) | Out-Null
        Start-Sleep -Milliseconds 250
        [ExternalValidationInputNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)

        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        do {
            Start-Sleep -Milliseconds 500
            $clipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
            if ($clipboard -match [regex]::Escape($ExpectedText)) {
                return
            }
        } while ((Get-Date) -lt $deadline)

        throw "Desktop hotkey snip failed. Clipboard: $clipboard"
    }
    finally {
        if ($app -and !$UseExistingApp -and !$app.HasExited) {
            Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
        }
        if ($target -and !$target.HasExited) {
            Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

function Enable-LaunchAtLogin {
    $runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    New-Item -Path $runKey -Force | Out-Null
    Set-ItemProperty -Path $runKey -Name "OcrSnip" -Value "`"$exe`""
}

function Disable-LaunchAtLogin {
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "OcrSnip" -ErrorAction SilentlyContinue
}

function Register-PostRebootTask {
    $runner = Join-Path $PSScriptRoot "run-external-validation-kit.ps1"
    $pwsh = (Get-Command powershell.exe).Source
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$runner`"", "-CompletePostRebootValidation")
    if ($ExpectedWindows) {
        $arguments += @("-ExpectedWindows", $ExpectedWindows)
    }
    if ($ExpectedCpuVendor) {
        $arguments += @("-ExpectedCpuVendor", $ExpectedCpuVendor)
    }
    if ($ExpectedDpiScale) {
        $arguments += @("-ExpectedDpiScale", $ExpectedDpiScale)
    }

    $action = New-ScheduledTaskAction -Execute $pwsh -Argument ($arguments -join " ") -WorkingDirectory $kitRoot
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel LeastPrivilege
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 10)
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
}

function Complete-PostRebootValidation {
    Start-Sleep -Seconds 8
    Test-DesktopHotkey -UseExistingApp
    Disable-LaunchAtLogin
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
}

function Test-HotkeyConflict {
    Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class ExternalValidationHotkeyConflictProbe
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public static string[] GetVisibleWindowTitles(int pid)
    {
        var titles = new List<string>();
        EnumWindows((hWnd, lParam) =>
        {
            uint windowPid;
            GetWindowThreadProcessId(hWnd, out windowPid);
            if (windowPid != pid || !IsWindowVisible(hWnd)) return true;
            var builder = new StringBuilder(256);
            GetWindowText(hWnd, builder, builder.Capacity);
            var title = builder.ToString();
            if (!string.IsNullOrWhiteSpace(title)) titles.Add(title);
            return true;
        }, IntPtr.Zero);
        return titles.ToArray();
    }
}
"@

    $hotkeyId = 90210
    $registered = [ExternalValidationHotkeyConflictProbe]::RegisterHotKey([IntPtr]::Zero, $hotkeyId, 0x0002 -bor 0x0004, 0x4F)
    if (!$registered) {
        throw "Could not reserve Ctrl+Shift+O for conflict verification."
    }

    $process = $null
    try {
        $process = Start-Process -FilePath $exe -PassThru
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        do {
            Start-Sleep -Milliseconds 250
            if ($process.HasExited) {
                throw "App exited before showing the hotkey conflict path."
            }

            $titles = [ExternalValidationHotkeyConflictProbe]::GetVisibleWindowTitles($process.Id)
            if ($titles -contains "OCR Snip Settings") {
                return
            }
        } while ([DateTime]::UtcNow -lt $deadline)

        throw "Settings window did not appear after hotkey conflict."
    }
    finally {
        if ($process -and !$process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        [void][ExternalValidationHotkeyConflictProbe]::UnregisterHotKey([IntPtr]::Zero, $hotkeyId)
    }
}

function Get-DpiScalePercent($Screen) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class ExternalValidationDpiProbe
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    public static int GetDpiScalePercent(int x, int y)
    {
        POINT point; point.X = x; point.Y = y;
        var monitor = MonitorFromPoint(point, 2);
        uint dpiX; uint dpiY;
        if (monitor == IntPtr.Zero || GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0) return 0;
        return (int)Math.Round(dpiX / 96.0 * 100.0);
    }
}
"@ -ErrorAction SilentlyContinue

    $centerX = $Screen.Bounds.X + [int]($Screen.Bounds.Width / 2)
    $centerY = $Screen.Bounds.Y + [int]($Screen.Bounds.Height / 2)
    return [ExternalValidationDpiProbe]::GetDpiScalePercent($centerX, $centerY)
}

function Write-Status($Evidence, [string]$Path) {
    $lines = @("# External Validation Status", "")
    $lines += "| Gate | Status | Evidence | Verified At |"
    $lines += "| --- | --- | --- | --- |"
    foreach ($gate in $Evidence.Keys) {
        $entry = $Evidence[$gate]
        $status = if ($entry.passed) { "PASS" } else { "MISSING" }
        $safeEvidence = ([string]$entry.evidence) -replace "\|", "/"
        $lines += "| $gate | $status | $safeEvidence | $($entry.verifiedAt) |"
    }

    $lines | Set-Content $Path
}

function Write-RunMetadata($Metadata, [string]$Path) {
    $Metadata | ConvertTo-Json -Depth 6 | Set-Content $Path
}

Assert-Exists $exe
Assert-Exists $fixture
Assert-Exists $gateManifest
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

Add-Type -AssemblyName System.Windows.Forms
$gates = @((Get-Content $gateManifest -Raw | ConvertFrom-Json).id)
if (Test-Path $evidenceFile) {
    $evidence = Convert-ToHashtable (Get-Content $evidenceFile -Raw | ConvertFrom-Json)
}
else {
    $evidence = New-BlankEvidence $gates
}
$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$screens = [System.Windows.Forms.Screen]::AllScreens
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$identity
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

$appSelfTestsPassed = $false
$idleNoNetworkPassed = $false
$desktopHotkeyPassed = $false
$hotkeyConflictPassed = $false

if ($PreparePostRebootValidation) {
    Enable-LaunchAtLogin
    Register-PostRebootTask
    Write-Host "Prepared post-reboot validation. Reboot, sign in, and wait for the one-time validation task to run."
    return
}

if ($CompletePostRebootValidation) {
    Complete-PostRebootValidation
    $PostRebootHotkeyPassed = $true
    $desktopHotkeyPassed = $true
}

Invoke-AppSelfTests
$appSelfTestsPassed = $true
Test-IdleNoNetwork
$idleNoNetworkPassed = $true

if ($IncludeDesktopHotkey) {
    Test-DesktopHotkey
    $desktopHotkeyPassed = $true
}

if ($IncludeHotkeyConflict) {
    Test-HotkeyConflict
    $hotkeyConflictPassed = $true
}

if ($ExpectedWindows) {
    $isWindows10 = $os.Version -like "10.0.1*"
    $isWindows11 = $os.Version -like "10.0.2*"
    if ($ExpectedWindows -eq "Windows10" -and !$isWindows10) {
        throw "Expected Windows 10 x64, but detected $($os.Caption) $($os.Version)."
    }
    if ($ExpectedWindows -eq "Windows11" -and !$isWindows11) {
        throw "Expected Windows 11 x64, but detected $($os.Caption) $($os.Version)."
    }
}

if ($ExpectedCpuVendor) {
    $manufacturer = "$($cpu.Manufacturer) $($cpu.Name)"
    if ($ExpectedCpuVendor -eq "Intel" -and $manufacturer -notmatch "Intel|GenuineIntel") {
        throw "Expected Intel CPU, but detected $manufacturer."
    }
    if ($ExpectedCpuVendor -eq "AMD" -and $manufacturer -notmatch "AMD") {
        throw "Expected AMD CPU, but detected $manufacturer."
    }
}

if ($os.Version -like "10.0.1*") {
    Set-Gate $evidence "windows10x64" "$($os.Caption) $($os.Version), $($os.OSArchitecture), portable validation kit self-tests passed"
}

if ($cpu.Manufacturer -match "AMD") {
    Set-Gate $evidence "amdCpu" "$($cpu.Manufacturer) / $($cpu.Name)"
    Set-Gate $evidence "amdModelLoad" "Portable validation kit OCR self-test completed on AMD CPU: $($cpu.Name)"
}

if ($cpu.Manufacturer -match "Intel|GenuineIntel") {
    Set-Gate $evidence "intelModelLoad" "Portable validation kit OCR self-test completed on Intel CPU: $($cpu.Name)"
}

if ($isAdmin) {
    Set-Gate $evidence "adminAccount" "Portable validation kit ran from an elevated administrator token"
}
else {
    Set-Gate $evidence "standardAccount" "Portable validation kit ran from a non-elevated account"
}

$dpiScales = @()
$screenMetadata = @()
foreach ($screen in $screens) {
    $dpiScale = Get-DpiScalePercent $screen
    $screenMetadata += [ordered]@{
        deviceName = $screen.DeviceName
        primary = $screen.Primary
        bounds = [ordered]@{
            x = $screen.Bounds.X
            y = $screen.Bounds.Y
            width = $screen.Bounds.Width
            height = $screen.Bounds.Height
        }
        workingArea = [ordered]@{
            x = $screen.WorkingArea.X
            y = $screen.WorkingArea.Y
            width = $screen.WorkingArea.Width
            height = $screen.WorkingArea.Height
        }
        dpiScalePercent = $dpiScale
    }

    if ($dpiScale -gt 0) {
        $dpiScales += $dpiScale
        if ($dpiScale -in @(100, 125, 150) -and $desktopHotkeyPassed) {
            Set-Gate $evidence "dpi$dpiScale" "Portable validation kit detected DPI scale $dpiScale% and desktop hotkey snip passed on screen $($screen.Bounds)"
        }
    }

    if ($screen.Bounds.X -lt 0 -or $screen.Bounds.Y -lt 0) {
        Set-Gate $evidence "negativeVirtualMonitor" "Detected monitor with negative virtual coordinate: $($screen.Bounds)"
    }
}

if ($ExpectedDpiScale) {
    if (!$desktopHotkeyPassed) {
        throw "DPI validation requires -IncludeDesktopHotkey so the snip path is verified at the detected scale."
    }

    if ($dpiScales -notcontains $ExpectedDpiScale) {
        throw "Expected DPI scale $ExpectedDpiScale%, but detected: $($dpiScales -join ', ')"
    }
}

if ($RequireMixedDpi -and (@($dpiScales | Select-Object -Unique).Count -le 1)) {
    throw "Mixed DPI was required but not detected."
}

if (@($dpiScales | Select-Object -Unique).Count -gt 1) {
    Set-Gate $evidence "mixedDpi" "Portable validation kit detected mixed DPI scales: $($dpiScales -join ', ')"
}

if ($RequireNegativeVirtualMonitor -and $evidence["negativeVirtualMonitor"].passed -ne $true) {
    throw "Negative virtual monitor was required but not detected."
}

if ($MultiMonitorCapturePassed) {
    Set-Gate $evidence "multiMonitorCapture" "Manual multi-monitor capture validation passed on $($screens.Count) displays while running portable validation kit"
}

if ($PostRebootHotkeyPassed) {
    Set-Gate $evidence "postRebootHotkey" "Post-reboot login hotkey validation passed while running portable validation kit"
}

if ($RequireAdminAccount) {
    Assert-GatePassed $evidence "adminAccount" "Admin account validation was required but this run was not elevated."
}

if ($RequirePostRebootHotkey) {
    Assert-GatePassed $evidence "postRebootHotkey" "Post-reboot hotkey validation was required but has not passed. Run -PreparePostRebootValidation, reboot, sign in, and wait for completion."
}

if ($RequireMultiMonitorCapture) {
    Assert-GatePassed $evidence "multiMonitorCapture" "Multi-monitor capture validation was required but was not marked passed. Re-run with -MultiMonitorCapturePassed after manual multi-monitor capture verification."
}

$evidence | ConvertTo-Json -Depth 4 | Set-Content $evidenceFile
Write-Status $evidence $statusFile
$metadata = [ordered]@{
    generatedAt = Get-Date -Format o
    os = [ordered]@{
        caption = $os.Caption
        version = $os.Version
        architecture = $os.OSArchitecture
    }
    cpu = [ordered]@{
        manufacturer = $cpu.Manufacturer
        name = $cpu.Name
    }
    process = [ordered]@{
        elevatedAdministrator = $isAdmin
        is64BitProcess = [Environment]::Is64BitProcess
        is64BitOperatingSystem = [Environment]::Is64BitOperatingSystem
    }
    display = [ordered]@{
        screenCount = @($screens).Count
        dpiScales = @($dpiScales)
        mixedDpi = (@($dpiScales | Select-Object -Unique).Count -gt 1)
        hasNegativeVirtualMonitor = ($screenMetadata | Where-Object { $_.bounds.x -lt 0 -or $_.bounds.y -lt 0 }) -ne $null
        screens = $screenMetadata
    }
    requestedChecks = [ordered]@{
        expectedWindows = $ExpectedWindows
        expectedCpuVendor = $ExpectedCpuVendor
        expectedDpiScale = $ExpectedDpiScale
        requireMixedDpi = [bool]$RequireMixedDpi
        requireNegativeVirtualMonitor = [bool]$RequireNegativeVirtualMonitor
        requireAdminAccount = [bool]$RequireAdminAccount
        requirePostRebootHotkey = [bool]$RequirePostRebootHotkey
        requireMultiMonitorCapture = [bool]$RequireMultiMonitorCapture
        includeDesktopHotkey = [bool]$IncludeDesktopHotkey
        includeHotkeyConflict = [bool]$IncludeHotkeyConflict
        completePostRebootValidation = [bool]$CompletePostRebootValidation
        multiMonitorCapturePassed = [bool]$MultiMonitorCapturePassed
    }
    completedChecks = [ordered]@{
        appSelfTests = $appSelfTestsPassed
        idleNoNetwork = $idleNoNetworkPassed
        desktopHotkey = $desktopHotkeyPassed
        hotkeyConflict = $hotkeyConflictPassed
        postRebootHotkey = [bool]$PostRebootHotkeyPassed
        multiMonitorCapture = [bool]$MultiMonitorCapturePassed
    }
}
Write-RunMetadata $metadata $metadataFile
if (Test-Path $exportZip) {
    Remove-Item -LiteralPath $exportZip -Force
}
Compress-Archive -Path $evidenceFile, $statusFile, $metadataFile -DestinationPath $exportZip -Force

Write-Host "External validation evidence written to $evidenceFile"
Write-Host "External validation metadata written to $metadataFile"
Write-Host "External validation export written to $exportZip"
