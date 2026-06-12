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

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class InputNative {
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
    $app = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 3

    # Ctrl+Shift+O
    [InputNative]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    [InputNative]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero)
    [InputNative]::keybd_event(0x4F, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [InputNative]::keybd_event(0x4F, 0, 2, [UIntPtr]::Zero)
    [InputNative]::keybd_event(0x10, 0, 2, [UIntPtr]::Zero)
    [InputNative]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)

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
        $clipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
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
