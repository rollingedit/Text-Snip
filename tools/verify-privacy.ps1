param(
    [string]$AppDataSubdir = "OcrSnip"
)

$ErrorActionPreference = "Stop"
$appDataPath = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) $AppDataSubdir

if (Test-Path $appDataPath) {
    $forbidden = Get-ChildItem $appDataPath -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @(".png", ".jpg", ".jpeg", ".bmp", ".log") }
    if ($forbidden) {
        throw "Potential privacy-sensitive files found under app data: $($forbidden.FullName -join ', ')"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sourceHits = Get-ChildItem (Join-Path $repoRoot "src") -Recurse -File -Include *.cs |
    Select-String -Pattern "File\.WriteAllText\([^)]*(result\.Text|Clipboard|Recognize|OcrResult)|\.Save\([^)]*\.png|\.Save\([^)]*\.jpg" -ErrorAction SilentlyContinue
if ($sourceHits) {
    throw "Potential OCR text or screenshot persistence code found: $($sourceHits | Select-Object -First 5)"
}

Write-Host "Privacy verification passed: no screenshot/log artifacts or obvious OCR text persistence found."
