param(
    [string]$ExpectedText = "OCR TEST",
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$exe = Join-Path $repoRoot "artifacts/publish/OcrSnip/OcrSnip.App.exe"

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
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public static void SendCtrlShiftO() {
    INPUT[] inputs = new INPUT[6];
    inputs[0].type = 1; inputs[0].u.ki.wVk = 0x11;
    inputs[1].type = 1; inputs[1].u.ki.wVk = 0x10;
    inputs[2].type = 1; inputs[2].u.ki.wVk = 0x4F;
    inputs[3].type = 1; inputs[3].u.ki.wVk = 0x4F; inputs[3].u.ki.dwFlags = 0x0002;
    inputs[4].type = 1; inputs[4].u.ki.wVk = 0x10; inputs[4].u.ki.dwFlags = 0x0002;
    inputs[5].type = 1; inputs[5].u.ki.wVk = 0x11; inputs[5].u.ki.dwFlags = 0x0002;
    uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    if (sent != inputs.Length) {
      throw new InvalidOperationException("SendInput failed for Ctrl+Shift+O. Sent " + sent + " of " + inputs.Length + ", last error " + Marshal.GetLastWin32Error() + ".");
    }
  }
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
try {
    Start-Sleep -Seconds 2
    Set-Clipboard -Value "__OCR_SNIP_PENDING__"
    $app = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 8
    if ($app.HasExited) {
        throw "OcrSnip.App exited before hotkey snip verification. Exit code: $($app.ExitCode)"
    }

    [InputNative]::SendCtrlShiftO()

    Start-Sleep -Seconds 1
    [InputNative]::SetCursorPos(130, 155) | Out-Null
    Start-Sleep -Milliseconds 100
    [InputNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    [InputNative]::SetCursorPos(620, 245) | Out-Null
    Start-Sleep -Milliseconds 250
    [InputNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        $clipboard = Get-ClipboardText
        if ($clipboard -match [regex]::Escape($ExpectedText)) {
            Write-Host "Hotkey snip verification passed."
            return
        }
    } while ((Get-Date) -lt $deadline)

    throw "Hotkey snip verification failed. Clipboard: $clipboard"
}
finally {
    if ($app -and !$app.HasExited) {
        Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    }
    if ($target -and !$target.HasExited) {
        Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    }
}
