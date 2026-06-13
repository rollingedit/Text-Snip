param(
    [string]$OutputPath = "artifacts/reports/memory-summary.txt",
    [int]$IdleSeconds = 5
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $repoRoot "artifacts/publish/OcrSnip"
$exe = Join-Path $publishDir "OcrSnip.App.exe"
$output = Join-Path $repoRoot $OutputPath

if (!(Test-Path $exe)) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
Get-Process -Name "OcrSnip.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $exe -ArgumentList "--tray" -PassThru -WindowStyle Hidden
Start-Sleep -Seconds $IdleSeconds
try {
    $running = Get-Process -Id $process.Id -ErrorAction Stop
    $summary = [pscustomobject]@{
        processName = $running.ProcessName
        responding = $running.Responding
        workingSetMB = [math]::Round($running.WorkingSet64 / 1MB, 2)
        privateMemoryMB = [math]::Round($running.PrivateMemorySize64 / 1MB, 2)
        idleSeconds = $IdleSeconds
    }
    $summary | Format-List | Out-String | Set-Content $output
    $summary
}
finally {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}
