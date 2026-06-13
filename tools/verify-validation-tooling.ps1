param(
    [string]$ScratchRoot = "artifacts/reports/validation-tooling"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$scratch = Join-Path $repoRoot $ScratchRoot
New-Item -ItemType Directory -Force -Path $scratch | Out-Null

function Join-Scratch([string]$FileName) {
    return Join-Path $ScratchRoot $FileName
}

function Join-ScratchAbsolute([string]$FileName) {
    return Join-Path $scratch $FileName
}

function Assert-Throws([scriptblock]$Command, [string]$ExpectedText) {
    try {
        & $Command
    }
    catch {
        if ($_.Exception.Message -notmatch [regex]::Escape($ExpectedText)) {
            throw "Expected error containing '$ExpectedText', got: $($_.Exception.Message)"
        }

        return
    }

    throw "Expected command to fail with '$ExpectedText'."
}

function Test-PowerShellSyntax([string]$Path) {
    $errors = $null
    $null = [System.Management.Automation.PSParser]::Tokenize((Get-Content $Path -Raw), [ref]$errors)
    if ($errors) {
        throw "PowerShell parse failed for $Path`: $($errors | Out-String)"
    }
}

function New-Evidence([string[]]$PassedGates) {
    $data = [ordered]@{}
    foreach ($gate in $gates) {
        $isPassed = $PassedGates -contains $gate
        $data[$gate] = [ordered]@{
            passed = $isPassed
            evidence = if ($isPassed) { "validation tooling self-test evidence for $gate" } else { "" }
            verifiedAt = if ($isPassed) { Get-Date -Format o } else { "" }
        }
    }

    return $data
}

try {
    $scriptsToParse = @(
        "verify-ship-readiness.ps1",
        "record-external-validation.ps1",
        "import-external-validation.ps1",
        "write-validation-status.ps1",
        "export-external-validation.ps1",
        "create-external-validation-kit.ps1",
        "create-external-validation-iso.ps1",
        "verify-external-validation-kit.ps1",
        "run-external-validation-kit.ps1",
        "invoke-external-validation-profile.ps1",
        "validate-machine-environment.ps1",
        "validate-display-environment.ps1",
        "validate-admin-environment.ps1",
        "verify-monitor-capture.ps1",
        "prepare-post-reboot-validation.ps1",
        "complete-post-reboot-validation.ps1"
    )

    foreach ($script in $scriptsToParse) {
        Test-PowerShellSyntax (Join-Path $PSScriptRoot $script)
    }

    $gates = @((Get-Content (Join-Path $PSScriptRoot "validation-gates.json") -Raw | ConvertFrom-Json).id)
    if ($gates.Count -eq 0) {
        throw "validation-gates.json contains no gates."
    }

    if (($gates | Select-Object -Unique).Count -ne $gates.Count) {
        throw "validation-gates.json contains duplicate gate ids."
    }

    foreach ($gate in $gates) {
        if ([string]::IsNullOrWhiteSpace($gate)) {
            throw "validation-gates.json contains a blank gate id."
        }
    }

    $completeEvidence = Join-ScratchAbsolute "complete.json"
    New-Evidence $gates | ConvertTo-Json -Depth 4 | Set-Content $completeEvidence
    & (Join-Path $PSScriptRoot "verify-ship-readiness.ps1") -EvidencePath (Join-Scratch "complete.json")

    $incompleteEvidence = Join-ScratchAbsolute "incomplete.json"
    New-Evidence @($gates[0]) | ConvertTo-Json -Depth 4 | Set-Content $incompleteEvidence
    Assert-Throws { & (Join-Path $PSScriptRoot "verify-ship-readiness.ps1") -EvidencePath (Join-Scratch "incomplete.json") } "Missing external evidence"

    $missingEvidenceText = Join-ScratchAbsolute "missing-evidence-text.json"
    New-Evidence @($gates[0]) | ConvertTo-Json -Depth 4 | Set-Content $missingEvidenceText
    $bad = Get-Content $missingEvidenceText -Raw | ConvertFrom-Json
    $bad.($gates[0]).evidence = ""
    $bad | ConvertTo-Json -Depth 4 | Set-Content $missingEvidenceText
    Assert-Throws { & (Join-Path $PSScriptRoot "verify-ship-readiness.ps1") -EvidencePath (Join-Scratch "missing-evidence-text.json") } "has no evidence text"

    $invalidTimestamp = Join-ScratchAbsolute "invalid-timestamp.json"
    New-Evidence @($gates[0]) | ConvertTo-Json -Depth 4 | Set-Content $invalidTimestamp
    $badTimestamp = Get-Content $invalidTimestamp -Raw | ConvertFrom-Json
    $badTimestamp.($gates[0]).verifiedAt = "not-a-timestamp"
    $badTimestamp | ConvertTo-Json -Depth 4 | Set-Content $invalidTimestamp
    Assert-Throws { & (Join-Path $PSScriptRoot "verify-ship-readiness.ps1") -EvidencePath (Join-Scratch "invalid-timestamp.json") } "invalid verifiedAt timestamp"

    $unknownGate = Join-ScratchAbsolute "unknown-gate.json"
    @{ notAGate = @{ passed = $true; evidence = "bad"; verifiedAt = Get-Date -Format o } } | ConvertTo-Json -Depth 4 | Set-Content $unknownGate
    Assert-Throws { & (Join-Path $PSScriptRoot "verify-ship-readiness.ps1") -EvidencePath (Join-Scratch "unknown-gate.json") } "Unknown validation gate"
    Assert-Throws { & (Join-Path $PSScriptRoot "import-external-validation.ps1") -SourcePath $unknownGate -EvidencePath (Join-Scratch "import-target.json") } "Unknown validation gate"

    $importSource = Join-ScratchAbsolute "import-source.json"
    New-Evidence @($gates[0]) | ConvertTo-Json -Depth 4 | Set-Content $importSource
    $importTarget = Join-ScratchAbsolute "import-target.json"
    Remove-Item $importTarget -Force -ErrorAction SilentlyContinue
    & (Join-Path $PSScriptRoot "import-external-validation.ps1") -SourcePath $importSource -EvidencePath (Join-Scratch "import-target.json")
    $imported = Get-Content $importTarget -Raw | ConvertFrom-Json
    if ($imported.($gates[0]).passed -ne $true) {
        throw "Import target did not include the expected passed gate."
    }

    $zipSourceRoot = Join-ScratchAbsolute "zip-source"
    New-Item -ItemType Directory -Force -Path $zipSourceRoot | Out-Null
    Copy-Item -LiteralPath $importSource -Destination (Join-Path $zipSourceRoot "external-validation.json")
    "validation tooling zip import status" | Set-Content (Join-Path $zipSourceRoot "validation-status.md")
    @{ generatedAt = Get-Date -Format o; source = "validation tooling self-test" } |
        ConvertTo-Json -Depth 3 |
        Set-Content (Join-Path $zipSourceRoot "validation-run-metadata.json")
    $zipSource = Join-ScratchAbsolute "external-validation-export.zip"
    Compress-Archive -Path (Join-Path $zipSourceRoot "*") -DestinationPath $zipSource -Force
    $zipImportTarget = Join-ScratchAbsolute "zip-import-target.json"
    Remove-Item $zipImportTarget -Force -ErrorAction SilentlyContinue
    & (Join-Path $PSScriptRoot "import-external-validation.ps1") -SourcePath $zipSource -EvidencePath (Join-Scratch "zip-import-target.json")
    $zipImported = Get-Content $zipImportTarget -Raw | ConvertFrom-Json
    if ($zipImported.($gates[0]).passed -ne $true) {
        throw "ZIP import target did not include the expected passed gate."
    }

    $statusPath = Join-ScratchAbsolute "status.md"
    & (Join-Path $PSScriptRoot "write-validation-status.ps1") -EvidencePath (Join-Scratch "complete.json") -OutputPath (Join-Scratch "status.md")
    if (!(Test-Path $statusPath) -or (Get-Content $statusPath -Raw) -notmatch "\| $($gates[0]) \| PASS \|") {
        throw "Validation status output did not include expected passed gate."
    }
}
finally {
    Remove-Item -Recurse -Force $scratch -ErrorAction SilentlyContinue
}

Write-Host "Validation tooling verification passed."
