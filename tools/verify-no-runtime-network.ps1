param(
    [int]$Seconds = 5
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$exe = Join-Path $repoRoot "artifacts/publish/OcrSnip/OcrSnip.App.exe"

if (!(Test-Path $exe)) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

Get-Process -Name "OcrSnip.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $exe -ArgumentList "--tray" -PassThru -WindowStyle Hidden
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

Write-Host "No runtime TCP sockets detected during idle launch window."
