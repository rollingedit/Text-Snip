param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $candidates = @(
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

& (Join-Path $PSScriptRoot "publish.ps1") -Configuration $Configuration
& $iscc (Join-Path $repoRoot "installer/OcrSnip.iss")
