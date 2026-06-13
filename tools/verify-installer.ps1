param(
    [string]$InstallDir = "artifacts/installer-test/OcrSnip"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$setup = Join-Path $repoRoot "installer/Output/OcrSnip-Setup-x64.exe"
$target = Join-Path $repoRoot $InstallDir

function Assert-AssociatedIcon([string]$Path) {
    Add-Type -AssemblyName System.Drawing
    $icon = [System.Drawing.Icon]::ExtractAssociatedIcon($Path)
    try {
        if ($null -eq $icon) {
            throw "No associated icon found for $Path"
        }
    }
    finally {
        if ($icon) {
            $icon.Dispose()
        }
    }
}

if (!(Test-Path $setup)) {
    & (Join-Path $PSScriptRoot "build-installer.ps1")
}

Assert-AssociatedIcon $setup

if (Test-Path $target) {
    Remove-Item $target -Recurse -Force
}

New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
$installArgs = @(
    "/VERYSILENT",
    "/SUPPRESSMSGBOXES",
    "/NORESTART",
    "/CURRENTUSER",
    "/TASKS=",
    "/DIR=$target"
)
$install = Start-Process -FilePath $setup -ArgumentList $installArgs -Wait -PassThru
if ($install.ExitCode -ne 0) {
    throw "Installer failed with exit code $($install.ExitCode)."
}

$exe = Join-Path $target "OcrSnip.App.exe"
$uninstaller = Join-Path $target "unins000.exe"
Assert-AssociatedIcon $exe
foreach ($path in @(
    $exe,
    $uninstaller,
    (Join-Path $target "models/ppocrv6-small-det/inference.onnx"),
    (Join-Path $target "models/ppocrv6-small-rec/inference.onnx"),
    (Join-Path $target "licenses/ONNXRuntime-MIT.txt")
)) {
    if (!(Test-Path $path)) {
        throw "Installer output missing required file: $path"
    }
}

$process = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 3
try {
    $running = Get-Process -Id $process.Id -ErrorAction Stop
    if (!$running.Responding) {
        throw "Installed app process is not responding."
    }
}
finally {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}

$uninstall = Start-Process -FilePath $uninstaller -ArgumentList @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART") -Wait -PassThru
if ($uninstall.ExitCode -ne 0) {
    throw "Uninstaller failed with exit code $($uninstall.ExitCode)."
}

if (Test-Path $target) {
    $remaining = Get-ChildItem $target -Recurse -Force -ErrorAction SilentlyContinue
    if ($remaining) {
        throw "Uninstaller left files behind in $target"
    }
}

Write-Host "Installer verification passed."
