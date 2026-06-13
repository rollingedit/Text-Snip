param(
    [string]$InstallDir = "artifacts/installer-test/OcrSnip",
    [string]$FixturePath = "Fixtures/generated/simple_text.png"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnet = Join-Path $repoRoot ".dotnet/dotnet.exe"
if (!(Test-Path $dotnet)) {
    $dotnet = "dotnet"
}
$setup = Join-Path $repoRoot "installer/Output/OcrSnip-Setup-x64.exe"
$vcRedist = Join-Path $repoRoot "artifacts/prereqs/vc_redist.x64.exe"
$target = Join-Path $repoRoot $InstallDir
$fixture = Join-Path $repoRoot $FixturePath
$cliProject = Join-Path $repoRoot "src/OcrSnip.Tools.OcrCli/OcrSnip.Tools.OcrCli.csproj"
$userSettingsPath = Join-Path $env:APPDATA "OcrSnip/settings.json"
$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$runValueName = "OcrSnip"

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

function Stop-ExistingOcrSnipApp {
    Get-Process -Name "OcrSnip.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $deadline = (Get-Date).AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 250
        $remaining = Get-Process -Name "OcrSnip.App" -ErrorAction SilentlyContinue | Select-Object -First 1
    } while ($remaining -and (Get-Date) -lt $deadline)
    if ($remaining) {
        throw "Could not stop an existing OcrSnip.App instance before installer verification."
    }
}

function Get-OcrSnipRunValue {
    $key = Get-ItemProperty -Path $runKeyPath -Name $runValueName -ErrorAction SilentlyContinue
    if ($null -eq $key) {
        return [ordered]@{ Exists = $false; Value = "" }
    }

    return [ordered]@{ Exists = $true; Value = [string]$key.$runValueName }
}

function Restore-OcrSnipRunValue($Snapshot) {
    if ($Snapshot.Exists) {
        New-Item -Path $runKeyPath -Force | Out-Null
        Set-ItemProperty -Path $runKeyPath -Name $runValueName -Value $Snapshot.Value
        return
    }

    Remove-ItemProperty -Path $runKeyPath -Name $runValueName -ErrorAction SilentlyContinue
}

function Use-IsolatedAppSettings {
    $snapshot = [ordered]@{
        Exists = Test-Path -LiteralPath $userSettingsPath
        Value = ""
    }
    if ($snapshot.Exists) {
        $snapshot.Value = Get-Content -LiteralPath $userSettingsPath -Raw
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $userSettingsPath -Parent) | Out-Null
    @'
{
  "Hotkey": {
    "Modifiers": 12,
    "Key": 79
  },
  "MemoryMode": 1,
  "SmallTextBoost": 0,
  "CopyMode": 0,
  "ToastEnabled": true,
  "LaunchAtLogin": false
}
'@ | Set-Content -LiteralPath $userSettingsPath

    return $snapshot
}

function Restore-AppSettings($Snapshot) {
    if ($Snapshot.Exists) {
        Set-Content -LiteralPath $userSettingsPath -Value $Snapshot.Value
        return
    }

    Remove-Item -LiteralPath $userSettingsPath -Force -ErrorAction SilentlyContinue
}

if (!(Test-Path $setup)) {
    & (Join-Path $PSScriptRoot "build-installer.ps1")
}

if (!(Test-Path $fixture)) {
    & (Join-Path $PSScriptRoot "generate-private-fixtures.ps1")
}

Assert-AssociatedIcon $setup
if (!(Test-Path -LiteralPath $vcRedist)) {
    throw "Installer prerequisite missing: $vcRedist"
}

if (Test-Path $target) {
    Remove-Item $target -Recurse -Force
}

Stop-ExistingOcrSnipApp
$runSnapshot = Get-OcrSnipRunValue
$settingsSnapshot = $null

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

$installedModelRoot = Join-Path $target "models"
$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
    $ocrOutput = & $dotnet run --project $cliProject -c Release -- $fixture --model-root $installedModelRoot 2>&1
    $ocrExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}

if ($ocrExitCode -ne 0) {
    throw "Installed model OCR verification failed with exit code $ocrExitCode.`n$ocrOutput"
}

if (($ocrOutput -join "`n") -notmatch "OCR TEST") {
    throw "Installed model OCR verification did not output expected text.`n$ocrOutput"
}

Stop-ExistingOcrSnipApp

try {
    $settingsSnapshot = Use-IsolatedAppSettings
    Remove-ItemProperty -Path $runKeyPath -Name $runValueName -ErrorAction SilentlyContinue

    $process = Start-Process -FilePath $exe -ArgumentList "--tray" -PassThru -WindowStyle Hidden
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
}
finally {
    Stop-ExistingOcrSnipApp
    Restore-OcrSnipRunValue $runSnapshot
    if ($settingsSnapshot) {
        Restore-AppSettings $settingsSnapshot
    }
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
