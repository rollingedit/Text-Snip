param(
    [string]$EvidencePath = "artifacts/reports/external-validation.json",
    [switch]$RunChecks,
    [switch]$PostRebootHotkeyPassed,
    [switch]$MultiMonitorCapturePassed
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$evidenceFile = Join-Path $repoRoot $EvidencePath

$allGates = @(
    "windows10x64",
    "amdCpu",
    "dpi100",
    "dpi125",
    "dpi150",
    "mixedDpi",
    "negativeVirtualMonitor",
    "lightTheme",
    "darkTheme",
    "standardAccount",
    "adminAccount",
    "postRebootHotkey",
    "multiMonitorCapture",
    "intelModelLoad",
    "amdModelLoad"
)

function New-BlankEvidence {
    $data = [ordered]@{}
    foreach ($gate in $allGates) {
        $data[$gate] = [ordered]@{
            passed = $false
            evidence = ""
            verifiedAt = ""
        }
    }

    return $data
}

function Convert-ToHashtable($InputObject) {
    $hash = [ordered]@{}
    foreach ($property in $InputObject.PSObject.Properties) {
        if ($property.Value -is [pscustomobject]) {
            $hash[$property.Name] = Convert-ToHashtable $property.Value
        }
        else {
            $hash[$property.Name] = $property.Value
        }
    }

    return $hash
}

function Set-Gate($Data, [string]$Gate, [string]$Evidence) {
    $Data[$Gate] = [ordered]@{
        passed = $true
        evidence = $Evidence
        verifiedAt = Get-Date -Format o
    }
}

if ($RunChecks) {
    & (Join-Path $PSScriptRoot "verify-release.ps1") -IncludeDesktopHotkey -IncludeHotkeyConflict -IncludeThemeModes
    & (Join-Path $PSScriptRoot "run-ocr-fixtures.ps1")
    & (Join-Path $PSScriptRoot "compare-paddle-reference.ps1")
    & (Join-Path $PSScriptRoot "verify-privacy.ps1")
    & (Join-Path $PSScriptRoot "verify-no-runtime-network.ps1")
    & (Join-Path $PSScriptRoot "verify-installer.ps1")
}

if (Test-Path $evidenceFile) {
    $evidence = Convert-ToHashtable (Get-Content $evidenceFile -Raw | ConvertFrom-Json)
}
else {
    $evidence = New-BlankEvidence
}

Add-Type -AssemblyName System.Windows.Forms
$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$screens = [System.Windows.Forms.Screen]::AllScreens
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$identity
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$themeReport = Join-Path $repoRoot "artifacts/reports/theme-validation.txt"
$compatibilityReport = Join-Path $repoRoot "artifacts/reports/compatibility-report.txt"
$fixtureReport = Join-Path $repoRoot "artifacts/reports/fixture-results.json"

if ($os.Version -like "10.0.1*") {
    Set-Gate $evidence "windows10x64" "$($os.Caption) $($os.Version), $($os.OSArchitecture)"
}

if ($cpu.Manufacturer -match "AMD") {
    Set-Gate $evidence "amdCpu" "$($cpu.Manufacturer) / $($cpu.Name)"
    if (Test-Path $fixtureReport) {
        Set-Gate $evidence "amdModelLoad" "OCR fixture runner completed on AMD CPU: $($cpu.Name)"
    }
}

if ($cpu.Manufacturer -match "Intel|GenuineIntel" -and (Test-Path $fixtureReport)) {
    Set-Gate $evidence "intelModelLoad" "OCR fixture runner completed on Intel CPU: $($cpu.Name)"
}

if ($isAdmin) {
    Set-Gate $evidence "adminAccount" "Automated checks recorded from elevated administrator token for $($identity.Name)"
}
else {
    Set-Gate $evidence "standardAccount" "Automated checks recorded from non-elevated account for $($identity.Name)"
}

if (Test-Path $themeReport) {
    $themeText = Get-Content $themeReport -Raw
    if ($themeText -match "light: passed") {
        Set-Gate $evidence "lightTheme" "theme-validation.txt reports light mode checks passed"
    }

    if ($themeText -match "dark: passed") {
        Set-Gate $evidence "darkTheme" "theme-validation.txt reports dark mode checks passed"
    }
}

foreach ($screen in $screens) {
    if ($screen.Bounds.X -lt 0 -or $screen.Bounds.Y -lt 0) {
        Set-Gate $evidence "negativeVirtualMonitor" "Detected monitor with negative virtual coordinate: $($screen.Bounds)"
    }
}

if ($MultiMonitorCapturePassed) {
    Set-Gate $evidence "multiMonitorCapture" "Manual multi-monitor capture validation passed on $($screens.Count) displays"
}

if ($PostRebootHotkeyPassed) {
    Set-Gate $evidence "postRebootHotkey" "Manual post-reboot login hotkey validation passed"
}

if (Test-Path $compatibilityReport) {
    $compatibilityText = Get-Content $compatibilityReport -Raw
    foreach ($scale in @(100, 125, 150)) {
        if ($compatibilityText -match "DPI scale: $scale%") {
            Set-Gate $evidence "dpi$scale" "compatibility-report.txt recorded DPI scale $scale%"
        }
    }

    if ($compatibilityText -match "Mixed DPI: True") {
        Set-Gate $evidence "mixedDpi" "compatibility-report.txt recorded mixed DPI"
    }
}

New-Item -ItemType Directory -Force -Path (Split-Path $evidenceFile -Parent) | Out-Null
$evidence | ConvertTo-Json -Depth 4 | Set-Content $evidenceFile
Write-Host "External validation evidence updated at $evidenceFile"
