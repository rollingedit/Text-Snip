param(
    [string]$OutputPath = "artifacts/reports/compatibility-report.txt"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$output = Join-Path $repoRoot $OutputPath
New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class DpiProbe
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    public static int GetDpiScalePercent(int x, int y)
    {
        POINT point;
        point.X = x;
        point.Y = y;
        var monitor = MonitorFromPoint(point, 2);
        uint dpiX;
        uint dpiY;
        if (monitor == IntPtr.Zero || GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0)
        {
            return 0;
        }

        return (int)Math.Round(dpiX / 96.0 * 100.0);
    }
}
"@

$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$screens = [System.Windows.Forms.Screen]::AllScreens
$dotnetInfo = & (Join-Path $repoRoot ".dotnet/dotnet.exe") --info
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$identity
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$theme = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" -ErrorAction SilentlyContinue
$desktop = Get-ItemProperty -Path "HKCU:\Control Panel\Desktop" -ErrorAction SilentlyContinue

$lines = @()
$lines += "Generated: $(Get-Date -Format o)"
$lines += "OS: $($os.Caption) $($os.Version)"
$lines += "Architecture: $($os.OSArchitecture)"
$lines += "CPU vendor/name: $($cpu.Manufacturer) / $($cpu.Name)"
$lines += "Logical processors: $($cpu.NumberOfLogicalProcessors)"
$lines += "Account: $($identity.Name)"
$lines += "Elevated administrator: $isAdmin"
$lines += "System DPI LogPixels: $($desktop.LogPixels)"
$lines += "System DPI scaling enabled: $($desktop.Win8DpiScaling)"
$lines += "App light theme: $($theme.AppsUseLightTheme)"
$lines += "System light theme: $($theme.SystemUsesLightTheme)"
$lines += "Screens:"
$dpiScales = @()
foreach ($screen in $screens) {
    $centerX = $screen.Bounds.X + [int]($screen.Bounds.Width / 2)
    $centerY = $screen.Bounds.Y + [int]($screen.Bounds.Height / 2)
    $dpiScale = [DpiProbe]::GetDpiScalePercent($centerX, $centerY)
    if ($dpiScale -gt 0) {
        $dpiScales += $dpiScale
    }

    $lines += "  Primary=$($screen.Primary) Bounds=$($screen.Bounds) WorkingArea=$($screen.WorkingArea) DPI scale: $dpiScale%"
}
$uniqueDpiScales = @($dpiScales | Select-Object -Unique)
$lines += "Mixed DPI: $($uniqueDpiScales.Count -gt 1)"
$lines += ""
$lines += ".NET:"
$lines += $dotnetInfo

$lines | Set-Content $output
Write-Host "Compatibility report written to $output"
