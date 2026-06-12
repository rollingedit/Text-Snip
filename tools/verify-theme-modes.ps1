param(
    [string]$OutputPath = "artifacts/reports/theme-validation.txt"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$output = Join-Path $repoRoot $OutputPath
$themePath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class ThemeBroadcast
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);
}
"@

function Get-ThemeValue([string]$Name) {
    $item = Get-ItemProperty -Path $themePath -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $item) {
        return $null
    }

    return $item.$Name
}

function Set-ThemeValue([string]$Name, $Value) {
    if ($null -eq $Value) {
        Remove-ItemProperty -Path $themePath -Name $Name -ErrorAction SilentlyContinue
        return
    }

    New-ItemProperty -Path $themePath -Name $Name -Value ([int]$Value) -PropertyType DWord -Force | Out-Null
}

function Set-ThemeMode([int]$Value) {
    Set-ThemeValue "AppsUseLightTheme" $Value
    Set-ThemeValue "SystemUsesLightTheme" $Value
    $result = [UIntPtr]::Zero
    [void][ThemeBroadcast]::SendMessageTimeout([IntPtr]0xffff, 0x001A, [UIntPtr]::Zero, "ImmersiveColorSet", 0x0002, 5000, [ref]$result)
    Start-Sleep -Milliseconds 750
}

function Invoke-Native([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Native command failed with exit code $LASTEXITCODE"
    }
}

$originalApps = Get-ThemeValue "AppsUseLightTheme"
$originalSystem = Get-ThemeValue "SystemUsesLightTheme"
$lines = @("Generated: $(Get-Date -Format o)")

try {
    foreach ($mode in @(
        @{ Name = "light"; Value = 1 },
        @{ Name = "dark"; Value = 0 }
    )) {
        Set-ThemeMode $mode.Value
        & (Join-Path $PSScriptRoot "verify-app-selftests.ps1") | Out-Null
        & (Join-Path $PSScriptRoot "verify-hotkey-conflict.ps1") | Out-Null
        & (Join-Path $PSScriptRoot "verify-hotkey-snip.ps1") | Out-Null
        $lines += "$($mode.Name): passed"
    }
}
finally {
    Set-ThemeValue "AppsUseLightTheme" $originalApps
    Set-ThemeValue "SystemUsesLightTheme" $originalSystem
    $result = [UIntPtr]::Zero
    [void][ThemeBroadcast]::SendMessageTimeout([IntPtr]0xffff, 0x001A, [UIntPtr]::Zero, "ImmersiveColorSet", 0x0002, 5000, [ref]$result)
}

$lines | Set-Content $output
Write-Host "Theme validation written to $output"
