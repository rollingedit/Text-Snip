param(
    [string]$FixturePath = "Fixtures/generated/simple_text.png"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$exe = Join-Path $repoRoot "artifacts/publish/OcrSnip/OcrSnip.App.exe"
$fixture = Join-Path $repoRoot $FixturePath

if (!(Test-Path $exe)) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

if (!(Test-Path $fixture)) {
    & (Join-Path $PSScriptRoot "generate-private-fixtures.ps1")
}

$startup = Start-Process -FilePath $exe -ArgumentList "--self-test-startup" -Wait -PassThru -WindowStyle Hidden
if ($startup.ExitCode -ne 0) {
    throw "Startup self-test failed with exit code $($startup.ExitCode)"
}

$ocr = Start-Process -FilePath $exe -ArgumentList @("--self-test-ocr", $fixture) -Wait -PassThru -WindowStyle Hidden
if ($ocr.ExitCode -ne 0) {
    throw "OCR clipboard self-test failed with exit code $($ocr.ExitCode)"
}

function Wait-ClipboardContains([string]$ExpectedText, [int]$TimeoutSeconds = 5) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $clipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
        if ($clipboard -match [regex]::Escape($ExpectedText)) {
            return $true
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    return $false
}

if (!(Wait-ClipboardContains "OCR TEST")) {
    throw "OCR clipboard self-test did not place expected text on clipboard."
}

Write-Host "App self-tests passed."
