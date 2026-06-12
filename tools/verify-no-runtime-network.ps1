param(
    [int]$Seconds = 5
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$exe = Join-Path $repoRoot "artifacts/publish/OcrSnip/OcrSnip.App.exe"

if (!(Test-Path $exe)) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

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

Write-Host "No runtime TCP sockets detected during idle launch window."
