param(
    [string]$Configuration = "Release",
    [switch]$IncludeDesktopHotkey,
    [switch]$IncludeHotkeyConflict,
    [switch]$IncludeThemeModes,
    [switch]$AllowHostInputAutomation
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnet = Join-Path $repoRoot ".dotnet/dotnet.exe"
$publishDir = Join-Path $repoRoot "artifacts/publish/OcrSnip"
$zipPath = Join-Path $repoRoot "artifacts/publish/OcrSnip-Portable-x64.zip"
. (Join-Path $PSScriptRoot "HostInputAutomationGuard.ps1")

if ($IncludeDesktopHotkey -or $IncludeHotkeyConflict -or $IncludeThemeModes) {
    Assert-HostInputAutomationAllowed -AllowedBySwitch ([bool]$AllowHostInputAutomation) -Reason "verify-release.ps1 was asked to run desktop/global-hotkey validation."
}

function Assert-Exists([string]$Path) {
    if (!(Test-Path $Path)) {
        throw "Required path missing: $Path"
    }
}

function Assert-ZipEntry([string]$Zip, [string]$Entry) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Zip)
    try {
        if (!($archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq $Entry })) {
            throw "Portable ZIP missing entry: $Entry"
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Invoke-Native([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Native command failed with exit code $LASTEXITCODE"
    }
}

Invoke-Native { & $dotnet build (Join-Path $repoRoot "OcrSnip.slnx") -c $Configuration }
Invoke-Native { & $dotnet test (Join-Path $repoRoot "OcrSnip.slnx") -c $Configuration --no-build }
& (Join-Path $PSScriptRoot "verify-models.ps1")
& (Join-Path $PSScriptRoot "run-ocr-fixtures.ps1") | Out-Null
Invoke-Native { & (Join-Path $PSScriptRoot "inspect-onnx.ps1") -ModelPath "assets/models/ppocrv6-small-det/inference.onnx" | Out-Null }
Invoke-Native { & (Join-Path $PSScriptRoot "inspect-onnx.ps1") -ModelPath "assets/models/ppocrv6-small-rec/inference.onnx" | Out-Null }
& (Join-Path $PSScriptRoot "publish.ps1") -Configuration $Configuration
& (Join-Path $PSScriptRoot "create-external-validation-kit.ps1") | Out-Null
& (Join-Path $PSScriptRoot "verify-external-validation-kit.ps1") -RunLocalSelfTest | Out-Null
& (Join-Path $PSScriptRoot "create-external-validation-iso.ps1") | Out-Null
& (Join-Path $PSScriptRoot "measure-memory.ps1") | Out-Null
& (Join-Path $PSScriptRoot "verify-signing-status.ps1") | Out-Null
& (Join-Path $PSScriptRoot "verify-app-selftests.ps1") | Out-Null
& (Join-Path $PSScriptRoot "collect-compatibility-report.ps1") | Out-Null
& (Join-Path $PSScriptRoot "verify-validation-tooling.ps1") | Out-Null
if ($IncludeDesktopHotkey) {
    & (Join-Path $PSScriptRoot "verify-hotkey-snip.ps1") -AllowHostInputAutomation:$AllowHostInputAutomation | Out-Null
}

if ($IncludeHotkeyConflict) {
    & (Join-Path $PSScriptRoot "verify-hotkey-conflict.ps1") -AllowHostInputAutomation:$AllowHostInputAutomation | Out-Null
}

if ($IncludeThemeModes) {
    & (Join-Path $PSScriptRoot "verify-theme-modes.ps1") -AllowHostInputAutomation:$AllowHostInputAutomation | Out-Null
}

Assert-Exists (Join-Path $publishDir "OcrSnip.App.exe")
Assert-Exists (Join-Path $publishDir "onnxruntime.dll")
Assert-Exists (Join-Path $publishDir "OpenCvSharpExtern.dll")
Assert-Exists (Join-Path $publishDir "models/ppocrv6-small-det/inference.onnx")
Assert-Exists (Join-Path $publishDir "models/ppocrv6-small-rec/inference.onnx")
Assert-Exists (Join-Path $publishDir "licenses/ONNXRuntime-MIT.txt")
Assert-Exists (Join-Path $publishDir "licenses/PP-OCRv6-ONNX-Apache-2.0.txt")
Assert-Exists $zipPath
Assert-Exists (Join-Path $repoRoot "artifacts/publish/OcrSnip-ExternalValidationKit.iso")
Assert-ZipEntry $zipPath "OcrSnip.App.exe"
Assert-ZipEntry $zipPath "models/ppocrv6-small-det/inference.onnx"
Assert-ZipEntry $zipPath "models/ppocrv6-small-rec/inference.onnx"
Assert-ZipEntry $zipPath "licenses/ONNXRuntime-MIT.txt"

$exe = Join-Path $publishDir "OcrSnip.App.exe"
$process = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 3
try {
    $running = Get-Process -Id $process.Id -ErrorAction Stop
    if (!$running.Responding) {
        throw "Published app process is not responding."
    }
}
finally {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}

Write-Host "Release verification passed."
