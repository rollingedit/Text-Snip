param(
    [string]$KitPath = "artifacts/publish/OcrSnip-ExternalValidationKit.zip",
    [string]$ScratchRoot = "artifacts/reports/external-validation-kit-verify",
    [switch]$RunLocalSelfTest
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$kit = Join-Path $repoRoot $KitPath
$scratch = Join-Path $repoRoot $ScratchRoot

function Assert-Exists([string]$Path) {
    if (!(Test-Path $Path)) {
        throw "Required path missing: $Path"
    }
}

function Test-PowerShellSyntax([string]$Path) {
    $errors = $null
    $null = [System.Management.Automation.PSParser]::Tokenize((Get-Content $Path -Raw), [ref]$errors)
    if ($errors) {
        throw "PowerShell parse failed for $Path`: $($errors | Out-String)"
    }
}

function Assert-ZipEntry($Entries, [string]$Entry) {
    if (!($Entries | Where-Object { $_.FullName.Replace('\', '/') -eq $Entry })) {
        throw "External validation kit missing entry: $Entry"
    }
}

Assert-Exists $kit

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($kit)
try {
    $entries = @($archive.Entries)
    Assert-ZipEntry $entries "README.md"
    Assert-ZipEntry $entries "START-HERE.txt"
    Assert-ZipEntry $entries "autorun.inf"
    Assert-ZipEntry $entries "Run-Windows10.cmd"
    Assert-ZipEntry $entries "Run-AMD.cmd"
    Assert-ZipEntry $entries "Run-Admin-Elevated.cmd"
    Assert-ZipEntry $entries "Prepare-PostReboot.cmd"
    Assert-ZipEntry $entries "Run-DPI-125.cmd"
    Assert-ZipEntry $entries "Run-DPI-150.cmd"
    Assert-ZipEntry $entries "Run-MixedDPI-MultiMonitor.cmd"
    Assert-ZipEntry $entries "OcrSnip/OcrSnip.App.exe"
    Assert-ZipEntry $entries "OcrSnip/models/ppocrv6-small-det/inference.onnx"
    Assert-ZipEntry $entries "OcrSnip/models/ppocrv6-small-rec/inference.onnx"
    Assert-ZipEntry $entries "OcrSnip/onnxruntime.dll"
    Assert-ZipEntry $entries "OcrSnip/OpenCvSharpExtern.dll"
    Assert-ZipEntry $entries "Fixtures/simple_text.png"
    Assert-ZipEntry $entries "tools/run-external-validation-kit.ps1"
    Assert-ZipEntry $entries "tools/validation-gates.json"

    $forbiddenPatterns = @(
        "(^|/)plan\.md$",
        "(^|/)todo\.md$",
        "(^|/)changelog\.md$",
        "(^|/)agents\.md$",
        "(^|/)\.git(/|$)",
        "(^|/)\.venv(/|$)",
        "(^|/)\.dotnet(/|$)",
        "(^|/)external-validation\.json$",
        "(^|/)validation-status\.md$",
        "(^|/)Fixtures/generated/"
    )

    foreach ($entry in $entries) {
        $name = $entry.FullName.Replace('\', '/')
        foreach ($pattern in $forbiddenPatterns) {
            if ($name -match $pattern) {
                throw "External validation kit contains forbidden private/generated entry: $name"
            }
        }
    }
}
finally {
    $archive.Dispose()
}

Remove-Item -Recurse -Force $scratch -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
Expand-Archive -LiteralPath $kit -DestinationPath $scratch -Force

$runner = Join-Path $scratch "tools/run-external-validation-kit.ps1"
$kitManifest = Join-Path $scratch "tools/validation-gates.json"
Assert-Exists $runner
Assert-Exists $kitManifest
Test-PowerShellSyntax $runner

$repoGates = @((Get-Content (Join-Path $PSScriptRoot "validation-gates.json") -Raw | ConvertFrom-Json).id)
$kitGates = @((Get-Content $kitManifest -Raw | ConvertFrom-Json).id)
if (($kitGates | Select-Object -Unique).Count -ne $kitGates.Count) {
    throw "External validation kit gate manifest contains duplicate gate ids."
}

$missingInKit = @($repoGates | Where-Object { $kitGates -notcontains $_ })
$extraInKit = @($kitGates | Where-Object { $repoGates -notcontains $_ })
if ($missingInKit.Count -gt 0 -or $extraInKit.Count -gt 0) {
    throw "External validation kit gate manifest does not match repo manifest. Missing: $($missingInKit -join ', '); Extra: $($extraInKit -join ', ')"
}

if ($RunLocalSelfTest) {
    & $runner
    $export = Join-Path $scratch "artifacts/reports/external-validation-export.zip"
    Assert-Exists $export

    $exportArchive = [System.IO.Compression.ZipFile]::OpenRead($export)
    try {
        $exportEntries = @($exportArchive.Entries)
        Assert-ZipEntry $exportEntries "external-validation.json"
        Assert-ZipEntry $exportEntries "validation-status.md"
        Assert-ZipEntry $exportEntries "validation-run-metadata.json"
    }
    finally {
        $exportArchive.Dispose()
    }
}

Remove-Item -Recurse -Force $scratch -ErrorAction SilentlyContinue
Write-Host "External validation kit verification passed."
