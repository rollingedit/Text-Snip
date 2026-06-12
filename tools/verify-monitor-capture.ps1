param(
    [switch]$RequireMultipleMonitors,
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
public static class MonitorCaptureInput {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
"@

$screens = [System.Windows.Forms.Screen]::AllScreens | Sort-Object { $_.Bounds.X }, { $_.Bounds.Y }
if ($RequireMultipleMonitors -and $screens.Count -lt 2) {
    throw "Multiple monitors were required, but only $($screens.Count) monitor was detected."
}

function Invoke-Hotkey {
    [MonitorCaptureInput]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    [MonitorCaptureInput]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero)
    [MonitorCaptureInput]::keybd_event(0x4F, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [MonitorCaptureInput]::keybd_event(0x4F, 0, 2, [UIntPtr]::Zero)
    [MonitorCaptureInput]::keybd_event(0x10, 0, 2, [UIntPtr]::Zero)
    [MonitorCaptureInput]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
}

function Invoke-Drag([int]$Left, [int]$Top, [int]$Right, [int]$Bottom) {
    [MonitorCaptureInput]::SetCursorPos($Left, $Top) | Out-Null
    Start-Sleep -Milliseconds 100
    [MonitorCaptureInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    [MonitorCaptureInput]::SetCursorPos($Right, $Bottom) | Out-Null
    Start-Sleep -Milliseconds 250
    [MonitorCaptureInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
}

function Start-Target([string]$Text, [int]$X, [int]$Y) {
    $encodedText = $Text.Replace("'", "''")
    $formScript = @"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
`$form = New-Object Windows.Forms.Form
`$form.Text = 'OCR Snip Monitor Target'
`$form.StartPosition = 'Manual'
`$form.Location = New-Object Drawing.Point($X,$Y)
`$form.Size = New-Object Drawing.Size(900,260)
`$form.TopMost = `$true
`$form.BackColor = [Drawing.Color]::White
`$label = New-Object Windows.Forms.Label
`$label.Text = '$encodedText'
`$label.Font = New-Object Drawing.Font('Arial', 54, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
`$label.AutoSize = `$true
`$label.Location = New-Object Drawing.Point(40,70)
`$form.Controls.Add(`$label)
[Windows.Forms.Application]::Run(`$form)
"@

    Start-Process powershell -ArgumentList @("-NoProfile", "-STA", "-Command", $formScript) -PassThru
}

$app = $null
$targets = @()
try {
    $app = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 3

    for ($index = 0; $index -lt $screens.Count; $index++) {
        $screen = $screens[$index]
        $text = "OCR MONITOR $($index + 1)"
        $targetX = $screen.Bounds.X + 80
        $targetY = $screen.Bounds.Y + 80
        $targets += Start-Target $text $targetX $targetY
        Start-Sleep -Seconds 2

        Set-Clipboard -Value "__OCR_SNIP_PENDING__"
        Invoke-Hotkey
        Start-Sleep -Seconds 1
        Invoke-Drag ($targetX + 30) ($targetY + 55) ($targetX + 620) ($targetY + 155)

        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        do {
            Start-Sleep -Milliseconds 500
            $clipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
            if ($clipboard -match [regex]::Escape($text)) {
                Write-Host "Monitor $($index + 1) capture passed: $($screen.Bounds)"
                break
            }
        } while ((Get-Date) -lt $deadline)

        if ($clipboard -notmatch [regex]::Escape($text)) {
            throw "Monitor $($index + 1) capture failed. Clipboard: $clipboard"
        }

        if ($targets[-1] -and !$targets[-1].HasExited) {
            Stop-Process -Id $targets[-1].Id -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Host "Monitor capture verification passed for $($screens.Count) monitor(s)."
}
finally {
    if ($app -and !$app.HasExited) {
        Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    }

    foreach ($target in $targets) {
        if ($target -and !$target.HasExited) {
            Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
