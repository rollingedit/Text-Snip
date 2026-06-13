param(
    [string]$EvidencePath = "artifacts/reports/external-validation.json",
    [switch]$RunAutomatedChecks,
    [switch]$AllowHostInputAutomation,
    [switch]$CreateTemplate
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$evidenceFile = Join-Path $repoRoot $EvidencePath
. (Join-Path $PSScriptRoot "HostInputAutomationGuard.ps1")

$requiredExternalGates = @((Get-Content (Join-Path $PSScriptRoot "validation-gates.json") -Raw | ConvertFrom-Json).id)

function Assert-ValidEvidenceFile($Evidence) {
    foreach ($property in $Evidence.PSObject.Properties) {
        if ($requiredExternalGates -notcontains $property.Name) {
            throw "Unknown validation gate '$($property.Name)' in $evidenceFile"
        }

        $entry = $property.Value
        if ($null -eq $entry -or $entry.passed -ne $true) {
            continue
        }

        if ([string]::IsNullOrWhiteSpace($entry.evidence)) {
            throw "Validation gate '$($property.Name)' is marked passed but has no evidence text."
        }

        $timestamp = [datetimeoffset]::MinValue
        if (![datetimeoffset]::TryParse([string]$entry.verifiedAt, [ref]$timestamp)) {
            throw "Validation gate '$($property.Name)' is marked passed but has an invalid verifiedAt timestamp."
        }
    }
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
    Assert-HostInputAutomationAllowed -AllowedBySwitch ([bool]$AllowHostInputAutomation) -Reason "verify-ship-readiness.ps1 -RunAutomatedChecks runs release hotkey, hotkey-conflict, and theme validations."
    & (Join-Path $PSScriptRoot "verify-release.ps1") -IncludeDesktopHotkey -IncludeHotkeyConflict -IncludeThemeModes -AllowHostInputAutomation:$AllowHostInputAutomation
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
Assert-ValidEvidenceFile $evidence
$missing = @()
foreach ($gate in $requiredExternalGates) {
    $entry = $evidence.$gate
    if ($null -eq $entry -or $entry.passed -ne $true -or [string]::IsNullOrWhiteSpace($entry.evidence)) {
        $missing += $gate
    }
}

if ($missing.Count -gt 0) {
    throw "Ship readiness is incomplete. Missing external evidence: $($missing -join ', ')"
}

Write-Host "Ship readiness evidence is complete."
