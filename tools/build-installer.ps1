param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$vcRedist = Join-Path $repoRoot "artifacts/prereqs/vc_redist.x64.exe"
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
else {
    $iscc = $iscc.Source
}

if (!$iscc) {
    throw "Inno Setup 6 ISCC.exe was not found. Install Inno Setup 6, then rerun this script."
}

if (!(Test-Path -LiteralPath $vcRedist)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $vcRedist -Parent) | Out-Null
    Write-Host "Downloading Microsoft Visual C++ Redistributable..."
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vc_redist.x64.exe" -OutFile $vcRedist
}

& (Join-Path $PSScriptRoot "publish.ps1") -Configuration $Configuration
& $iscc (Join-Path $repoRoot "installer/OcrSnip.iss")
