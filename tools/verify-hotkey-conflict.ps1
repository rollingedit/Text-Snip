param(
    [string]$PublishRoot = "artifacts/publish/OcrSnip",
    [switch]$AllowHostInputAutomation
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishRoot = Join-Path $repoRoot $PublishRoot
$exe = Join-Path $publishRoot "OcrSnip.App.exe"
. (Join-Path $PSScriptRoot "HostInputAutomationGuard.ps1")
Assert-HostInputAutomationAllowed -AllowedBySwitch ([bool]$AllowHostInputAutomation) -Reason "verify-hotkey-conflict.ps1 reserves a real global desktop hotkey and launches OCR Snip to verify conflict behavior."

if (!(Test-Path $exe)) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class HotkeyConflictProbe
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public static string[] GetVisibleWindowTitles(int pid)
    {
        var titles = new List<string>();
        EnumWindows((hWnd, lParam) =>
        {
            uint windowPid;
            GetWindowThreadProcessId(hWnd, out windowPid);
            if (windowPid != pid || !IsWindowVisible(hWnd))
            {
                return true;
            }

            var builder = new StringBuilder(256);
            GetWindowText(hWnd, builder, builder.Capacity);
            var title = builder.ToString();
            if (!string.IsNullOrWhiteSpace(title))
            {
                titles.Add(title);
            }

            return true;
        }, IntPtr.Zero);

        return titles.ToArray();
    }
}
"@

$hotkeyId = 90210
$modWinShift = 0x0008 -bor 0x0004
$vkO = 0x4F
$registered = [HotkeyConflictProbe]::RegisterHotKey([IntPtr]::Zero, $hotkeyId, $modWinShift, $vkO)
if (!$registered) {
    throw "Could not reserve Win+Shift+O for conflict verification."
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

        $titles = [HotkeyConflictProbe]::GetVisibleWindowTitles($process.Id)
        if ($titles -contains "OCR Snip Settings") {
            Write-Host "Hotkey conflict verification passed."
            exit 0
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Settings window did not appear after hotkey conflict. Window titles: $($titles -join ', ')"
}
finally {
    if ($process -and !$process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }

    [void][HotkeyConflictProbe]::UnregisterHotKey([IntPtr]::Zero, $hotkeyId)
}
