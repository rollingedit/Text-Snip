param(
    [string]$OutputPath = "artifacts/publish/OcrSnip-ExternalValidationKit.zip",
    [string]$StagingRoot = "artifacts/external-validation-kit"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$output = Join-Path $repoRoot $OutputPath
$staging = Join-Path $repoRoot $StagingRoot
$publishRoot = Join-Path $repoRoot "artifacts/publish/OcrSnip"
$fixtureRoot = Join-Path $repoRoot "Fixtures/generated"
$fixture = Join-Path $fixtureRoot "simple_text.png"

if (!(Test-Path (Join-Path $publishRoot "OcrSnip.App.exe"))) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

if (!(Test-Path $fixture)) {
    & (Join-Path $PSScriptRoot "generate-private-fixtures.ps1")
}

Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $staging | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $staging "Fixtures") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $staging "tools") | Out-Null

Copy-Item -Recurse -LiteralPath $publishRoot -Destination (Join-Path $staging "OcrSnip")
Copy-Item -LiteralPath $fixture -Destination (Join-Path $staging "Fixtures/simple_text.png")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "run-external-validation-kit.ps1") -Destination (Join-Path $staging "tools/run-external-validation-kit.ps1")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "validation-gates.json") -Destination (Join-Path $staging "tools/validation-gates.json")

@"
# OCR Snip External Validation Kit

Run this from an interactive PowerShell session on the validation machine:

````powershell
powershell -ExecutionPolicy Bypass -File tools\run-external-validation-kit.ps1 -ExpectedWindows Windows10 -IncludeDesktopHotkey -IncludeHotkeyConflict
````

For AMD validation, add:

````powershell
-ExpectedCpuVendor AMD
````

For DPI validation, run from the target DPI layout and include the desktop hotkey check:

````powershell
-ExpectedDpiScale 125 -IncludeDesktopHotkey
-ExpectedDpiScale 150 -IncludeDesktopHotkey
-RequireMixedDpi -RequireNegativeVirtualMonitor -MultiMonitorCapturePassed -IncludeDesktopHotkey
````

The runner writes `artifacts\reports\external-validation-export.zip`.
Import that ZIP back in the repo with:

````powershell
powershell -ExecutionPolicy Bypass -File tools\import-external-validation.ps1 -SourcePath path\to\external-validation-export.zip
````
"@ | Set-Content (Join-Path $staging "README.md")

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Force
}

Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $output -Force
Write-Host "External validation kit written to $output"
