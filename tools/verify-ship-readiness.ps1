param(
    [string]$EvidencePath = "artifacts/reports/external-validation.json",
    [switch]$RunAutomatedChecks,
    [switch]$CreateTemplate
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$evidenceFile = Join-Path $repoRoot $EvidencePath

$requiredExternalGates = @(
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

function Get-CurrentMachineEvidence {
    $compatibilityReport = Join-Path $repoRoot "artifacts/reports/compatibility-report.txt"
    $signingReport = Join-Path $repoRoot "artifacts/reports/signing-status.txt"
    $themeReport = Join-Path $repoRoot "artifacts/reports/theme-validation.txt"
    $evidence = @{}
    if (!(Test-Path $compatibilityReport)) {
        return $evidence
    }

    $text = Get-Content $compatibilityReport -Raw
    if ($text -match "Elevated administrator: False") {
        $evidence["standardAccount"] = "compatibility-report.txt shows non-elevated standard-account execution"
    }

    if ($text -match "App light theme: 0" -and $text -match "System light theme: 0") {
        $evidence["darkTheme"] = "compatibility-report.txt shows dark app/system theme registry values"
    }

    if (Test-Path $themeReport) {
        $themeText = Get-Content $themeReport -Raw
        if ($themeText -match "light: passed") {
            $evidence["lightTheme"] = "theme-validation.txt shows app, conflict, and hotkey snip checks passed in light mode"
        }

        if ($themeText -match "dark: passed") {
            $evidence["darkTheme"] = "theme-validation.txt shows app, conflict, and hotkey snip checks passed in dark mode"
        }
    }

    if ($text -match "CPU vendor/name: GenuineIntel" -and (Test-Path $signingReport)) {
        $evidence["intelModelLoad"] = "release verification completed model/app self-tests on an Intel x64 CPU"
    }

    return $evidence
}

function New-Template {
    $template = [ordered]@{}
    foreach ($gate in $requiredExternalGates) {
        $template[$gate] = [ordered]@{
            passed = $false
            evidence = ""
            verifiedAt = ""
        }
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $evidenceFile -Parent) | Out-Null
    $template | ConvertTo-Json -Depth 4 | Set-Content $evidenceFile
    Write-Host "External validation template written to $evidenceFile"
}

if ($CreateTemplate -and !(Test-Path $evidenceFile)) {
    New-Template
}

if ($RunAutomatedChecks) {
    & (Join-Path $PSScriptRoot "verify-release.ps1") -IncludeDesktopHotkey -IncludeHotkeyConflict -IncludeThemeModes
    & (Join-Path $PSScriptRoot "run-ocr-fixtures.ps1")
    & (Join-Path $PSScriptRoot "compare-paddle-reference.ps1")
    & (Join-Path $PSScriptRoot "verify-privacy.ps1")
    & (Join-Path $PSScriptRoot "verify-no-runtime-network.ps1")
    & (Join-Path $PSScriptRoot "verify-installer.ps1")
}

if (!(Test-Path $evidenceFile)) {
    New-Template
    throw "Ship readiness is incomplete: external validation evidence is missing."
}

$evidence = Get-Content $evidenceFile -Raw | ConvertFrom-Json
$currentMachineEvidence = Get-CurrentMachineEvidence
$missing = @()
foreach ($gate in $requiredExternalGates) {
    $entry = $evidence.$gate
    if ($currentMachineEvidence.ContainsKey($gate)) {
        continue
    }

    if ($null -eq $entry -or $entry.passed -ne $true -or [string]::IsNullOrWhiteSpace($entry.evidence)) {
        $missing += $gate
    }
}

if ($missing.Count -gt 0) {
    throw "Ship readiness is incomplete. Missing external evidence: $($missing -join ', ')"
}

Write-Host "Ship readiness evidence is complete."
