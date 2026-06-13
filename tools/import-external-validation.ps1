param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,
    [string]$EvidencePath = "artifacts/reports/external-validation.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$source = Resolve-Path $SourcePath
$destination = Join-Path $repoRoot $EvidencePath
$extractRoot = Join-Path $repoRoot "artifacts/reports/import-external-validation"

$knownGates = @((Get-Content (Join-Path $PSScriptRoot "validation-gates.json") -Raw | ConvertFrom-Json).id)

function Resolve-EvidenceSource([string]$Path) {
    if ([System.IO.Path]::GetExtension($Path).Equals(".zip", [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -Recurse -Force $extractRoot -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
        Expand-Archive -LiteralPath $Path -DestinationPath $extractRoot -Force
        $json = Get-ChildItem -LiteralPath $extractRoot -Filter "external-validation.json" -File -Recurse | Select-Object -First 2
        if (@($json).Count -ne 1) {
            throw "Validation export ZIP must contain exactly one external-validation.json file."
        }

        $metadata = Get-ChildItem -LiteralPath $extractRoot -Filter "validation-run-metadata.json" -File -Recurse | Select-Object -First 2
        if (@($metadata).Count -gt 1) {
            throw "Validation export ZIP must contain at most one validation-run-metadata.json file."
        }

        return [ordered]@{
            EvidencePath = $json[0].FullName
            MetadataPath = if (@($metadata).Count -eq 1) { $metadata[0].FullName } else { "" }
        }
    }

    return [ordered]@{
        EvidencePath = $Path
        MetadataPath = ""
    }
}

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

function Assert-MetadataSupportsGate([string]$Gate, $Metadata) {
    if ($null -eq $Metadata) {
        return
    }

    switch ($Gate) {
        "windows10x64" {
            if ($Metadata.os.version -notlike "10.0.1*" -or $Metadata.os.architecture -notmatch "64") {
                throw "Validation metadata does not support gate 'windows10x64'."
            }
        }
        "amdCpu" {
            if ("$($Metadata.cpu.manufacturer) $($Metadata.cpu.name)" -notmatch "AMD") {
                throw "Validation metadata does not support gate 'amdCpu'."
            }
        }
        "intelModelLoad" {
            if ("$($Metadata.cpu.manufacturer) $($Metadata.cpu.name)" -notmatch "Intel|GenuineIntel") {
                throw "Validation metadata does not support gate 'intelModelLoad'."
            }
            if ($Metadata.completedChecks.appSelfTests -ne $true) {
                throw "Validation metadata does not show app self-tests for gate 'intelModelLoad'."
            }
        }
        "amdModelLoad" {
            if ("$($Metadata.cpu.manufacturer) $($Metadata.cpu.name)" -notmatch "AMD") {
                throw "Validation metadata does not support gate 'amdModelLoad'."
            }
            if ($Metadata.completedChecks.appSelfTests -ne $true) {
                throw "Validation metadata does not show app self-tests for gate 'amdModelLoad'."
            }
        }
        "dpi100" {
            Assert-MetadataDpiGate $Metadata 100
        }
        "dpi125" {
            Assert-MetadataDpiGate $Metadata 125
        }
        "dpi150" {
            Assert-MetadataDpiGate $Metadata 150
        }
        "mixedDpi" {
            if ($Metadata.display.mixedDpi -ne $true -or $Metadata.completedChecks.desktopHotkey -ne $true) {
                throw "Validation metadata does not support gate 'mixedDpi'."
            }
        }
        "negativeVirtualMonitor" {
            if ($Metadata.display.hasNegativeVirtualMonitor -ne $true) {
                throw "Validation metadata does not support gate 'negativeVirtualMonitor'."
            }
        }
        "standardAccount" {
            if ($Metadata.process.elevatedAdministrator -ne $false) {
                throw "Validation metadata does not support gate 'standardAccount'."
            }
        }
        "adminAccount" {
            if ($Metadata.process.elevatedAdministrator -ne $true) {
                throw "Validation metadata does not support gate 'adminAccount'."
            }
        }
        "postRebootHotkey" {
            if ($Metadata.completedChecks.postRebootHotkey -ne $true) {
                throw "Validation metadata does not support gate 'postRebootHotkey'."
            }
        }
        "multiMonitorCapture" {
            if ($Metadata.completedChecks.multiMonitorCapture -ne $true) {
                throw "Validation metadata does not support gate 'multiMonitorCapture'."
            }
        }
    }
}

function Assert-MetadataDpiGate($Metadata, [int]$ExpectedDpi) {
    $scales = @($Metadata.display.dpiScales)
    if ($scales -notcontains $ExpectedDpi -or $Metadata.completedChecks.desktopHotkey -ne $true) {
        throw "Validation metadata does not support gate 'dpi$ExpectedDpi'."
    }
}

if (Test-Path $destination) {
    $merged = Convert-ToHashtable (Get-Content $destination -Raw | ConvertFrom-Json)
}
else {
    $merged = New-BlankEvidence
}

try {
    $resolved = Resolve-EvidenceSource $source
    $evidenceSource = $resolved.EvidencePath
    $metadata = if (![string]::IsNullOrWhiteSpace($resolved.MetadataPath)) {
        Get-Content $resolved.MetadataPath -Raw | ConvertFrom-Json
    }
    else {
        $null
    }

    $incoming = Get-Content $evidenceSource -Raw | ConvertFrom-Json
    $imported = @()
    foreach ($property in $incoming.PSObject.Properties) {
        if (Assert-ValidEntry $property.Name $property.Value) {
            Assert-MetadataSupportsGate $property.Name $metadata
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
}
finally {
    Remove-Item -Recurse -Force $extractRoot -ErrorAction SilentlyContinue
}
