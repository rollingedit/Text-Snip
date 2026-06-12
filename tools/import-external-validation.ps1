param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,
    [string]$EvidencePath = "artifacts/reports/external-validation.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$source = Resolve-Path $SourcePath
$destination = Join-Path $repoRoot $EvidencePath

$knownGates = @(
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
    foreach ($gate in $knownGates) {
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

function Assert-ValidEntry([string]$Gate, $Entry) {
    if ($knownGates -notcontains $Gate) {
        throw "Unknown validation gate '$Gate' in $source"
    }

    if ($null -eq $Entry -or $Entry.passed -ne $true) {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($Entry.evidence)) {
        throw "Validation gate '$Gate' is marked passed but has no evidence text."
    }

    $timestamp = [datetimeoffset]::MinValue
    if (![datetimeoffset]::TryParse([string]$Entry.verifiedAt, [ref]$timestamp)) {
        throw "Validation gate '$Gate' is marked passed but has an invalid verifiedAt timestamp."
    }

    return $true
}

if (Test-Path $destination) {
    $merged = Convert-ToHashtable (Get-Content $destination -Raw | ConvertFrom-Json)
}
else {
    $merged = New-BlankEvidence
}

$incoming = Get-Content $source -Raw | ConvertFrom-Json
$imported = @()
foreach ($property in $incoming.PSObject.Properties) {
    if (Assert-ValidEntry $property.Name $property.Value) {
        $merged[$property.Name] = [ordered]@{
            passed = $true
            evidence = [string]$property.Value.evidence
            verifiedAt = [string]$property.Value.verifiedAt
        }
        $imported += $property.Name
    }
}

if ($imported.Count -eq 0) {
    throw "No passed validation gates were imported from $source"
}

New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
$merged | ConvertTo-Json -Depth 4 | Set-Content $destination
Write-Host "Imported validation gates: $($imported -join ', ')"
