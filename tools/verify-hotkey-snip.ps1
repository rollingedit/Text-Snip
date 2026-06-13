param(
    [string]$ExpectedText = "OCR TEST",
    [int]$TimeoutSeconds = 15,
    [switch]$AllowHostInputAutomation
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$exe = Join-Path $repoRoot "artifacts/publish/OcrSnip/OcrSnip.App.exe"
. (Join-Path $PSScriptRoot "HostInputAutomationGuard.ps1")
Assert-HostInputAutomationAllowed -AllowedBySwitch ([bool]$AllowHostInputAutomation) -Reason "verify-hotkey-snip.ps1 starts OCR Snip, sends Ctrl+Shift+O, moves the mouse, drags a desktop selection, and reads the clipboard."

if (!(Test-Path $exe)) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

function Get-ClipboardText {
    for ($attempt = 1; $attempt -le 10; $attempt++) {
        try {
            return Get-Clipboard -Raw -ErrorAction Stop
        }
        catch {
            Start-Sleep -Milliseconds 150
        }
    }

    return ""
}

function Set-ValidationSettings {
    $settingsPath = Join-Path $env:APPDATA "OcrSnip\settings.json"
    $settingsDirectory = Split-Path -Parent $settingsPath
    $previous = $null
    if (Test-Path $settingsPath) {
        $previous = Get-Content $settingsPath -Raw
    }

    New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
    $settings = @{
        Hotkey = @{
            modifiers = 6
            key = 79
        }
        MemoryMode = 1
        SmallTextBoost = 0
        CopyMode = 0
        ToastEnabled = $true
        LaunchAtLogin = $true
    }
    $settings | ConvertTo-Json -Depth 4 | Set-Content -Path $settingsPath -Encoding UTF8

    return [pscustomobject]@{
        Path = $settingsPath
        Previous = $previous
        HadPrevious = $null -ne $previous
    }
}

function Restore-ValidationSettings($Snapshot) {
    if (!$Snapshot) {
        return
    }

    if ($Snapshot.HadPrevious) {
        Set-Content -Path $Snapshot.Path -Value $Snapshot.Previous -Encoding UTF8
    }
    elseif (Test-Path $Snapshot.Path) {
        Remove-Item -Path $Snapshot.Path -Force
    }
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class InputNative {
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion u; }
  [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public HARDWAREINPUT hi; }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }
  [DllImport("user32.dll", SetLastError=true)] public static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int nIndex);
  const uint INPUT_MOUSE = 0;
  const uint INPUT_KEYBOARD = 1;
  const uint MOUSEEVENTF_MOVE = 0x0001;
  const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  const uint MOUSEEVENTF_LEFTUP = 0x0004;
  const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
  const int SM_XVIRTUALSCREEN = 76;
  const int SM_YVIRTUALSCREEN = 77;
  const int SM_CXVIRTUALSCREEN = 78;
  const int SM_CYVIRTUALSCREEN = 79;
  public static void SendCtrlShiftO() {
    INPUT[] inputs = new INPUT[6];
    inputs[0].type = INPUT_KEYBOARD; inputs[0].u.ki.wVk = 0x11;
    inputs[1].type = INPUT_KEYBOARD; inputs[1].u.ki.wVk = 0x10;
    inputs[2].type = INPUT_KEYBOARD; inputs[2].u.ki.wVk = 0x4F;
    inputs[3].type = INPUT_KEYBOARD; inputs[3].u.ki.wVk = 0x4F; inputs[3].u.ki.dwFlags = 0x0002;
    inputs[4].type = INPUT_KEYBOARD; inputs[4].u.ki.wVk = 0x10; inputs[4].u.ki.dwFlags = 0x0002;
    inputs[5].type = INPUT_KEYBOARD; inputs[5].u.ki.wVk = 0x11; inputs[5].u.ki.dwFlags = 0x0002;
    uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    if (sent != inputs.Length) {
      throw new InvalidOperationException("SendInput failed for Ctrl+Shift+O. Sent " + sent + " of " + inputs.Length + ", last error " + Marshal.GetLastWin32Error() + ".");
    }
  }
  static int NormalizeX(int x) {
    int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
    int width = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN) - 1);
    return (int)Math.Round((x - left) * 65535.0 / width);
  }
  static int NormalizeY(int y) {
    int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
    int height = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN) - 1);
    return (int)Math.Round((y - top) * 65535.0 / height);
  }
  static INPUT MouseInput(int x, int y, uint flags) {
    INPUT input = new INPUT();
    input.type = INPUT_MOUSE;
    input.u.mi.dx = NormalizeX(x);
    input.u.mi.dy = NormalizeY(y);
    input.u.mi.dwFlags = flags | MOUSEEVENTF_ABSOLUTE;
    return input;
  }
  static void SendMouseInput(int x, int y, uint flags) {
    INPUT[] inputs = new INPUT[1];
    inputs[0] = MouseInput(x, y, flags);
    uint sent = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    if (sent != 1) {
      throw new InvalidOperationException("SendInput failed for mouse input. Sent " + sent + " of 1, last error " + Marshal.GetLastWin32Error() + ".");
    }
  }
  public static void SendMouseMove(int x, int y) { SendMouseInput(x, y, MOUSEEVENTF_MOVE); }
  public static void SendMouseDown(int x, int y) { SendMouseInput(x, y, MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTDOWN); }
  public static void SendMouseUp(int x, int y) { SendMouseInput(x, y, MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTUP); }
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
`$form.TopMost = `$false
`$form.BackColor = [Drawing.Color]::White
`$form.Add_Shown({
    `$form.TopMost = `$true
    `$form.Activate()
    `$form.BringToFront()
    `$form.TopMost = `$false
})
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
$settingsSnapshot = $null
try {
    Start-Sleep -Seconds 2
    Set-Clipboard -Value "__OCR_SNIP_PENDING__"
    $settingsSnapshot = Set-ValidationSettings
    $app = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 8
    if ($app.HasExited) {
        throw "OcrSnip.App exited before hotkey snip verification. Exit code: $($app.ExitCode)"
    }

    $dragLeft = 90
    $dragTop = 90
    $dragRight = 760
    $dragBottom = 330
    $clipboard = Get-ClipboardText
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempt = 0
    do {
        $attempt++
        if ($app.HasExited) {
            throw "OcrSnip.App exited during hotkey snip verification. Exit code: $($app.ExitCode)"
        }

        [InputNative]::SendCtrlShiftO()
        Start-Sleep -Seconds 1
        [InputNative]::SendMouseMove($dragLeft, $dragTop)
        Start-Sleep -Milliseconds 200
        [InputNative]::SendMouseDown($dragLeft, $dragTop)
        Start-Sleep -Milliseconds 200
        foreach ($step in 1..6) {
            $x = [int]($dragLeft + (($dragRight - $dragLeft) * $step / 6))
            $y = [int]($dragTop + (($dragBottom - $dragTop) * $step / 6))
            [InputNative]::SendMouseMove($x, $y)
            Start-Sleep -Milliseconds 120
        }
        [InputNative]::SendMouseUp($dragRight, $dragBottom)

        $attemptDeadline = (Get-Date).AddSeconds(6)
        do {
            Start-Sleep -Milliseconds 500
            $clipboard = Get-ClipboardText
            if ($clipboard -match [regex]::Escape($ExpectedText)) {
                Write-Host "Hotkey snip verification passed."
                return
            }
        } while ((Get-Date) -lt $attemptDeadline -and (Get-Date) -lt $deadline)
    } while ((Get-Date) -lt $deadline -and $attempt -lt 5)

    throw "Hotkey snip verification failed. Clipboard: $clipboard"
}
finally {
    if ($app -and !$app.HasExited) {
        Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    }
    if ($target -and !$target.HasExited) {
        Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    }
    Restore-ValidationSettings $settingsSnapshot
}
