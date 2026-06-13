param(
    [Parameter(Mandatory = $true)]
    [string]$VmName,
    [string]$IsoPath = "artifacts/publish/OcrSnip-ExternalValidationKit.iso",
    [string]$VBoxManagePath,
    [string]$ControllerName = "SATA",
    [int]$Port = 2,
    [int]$Device = 0,
    [ValidateSet("gui", "headless", "separate")]
    [string]$StartType = "gui",
    [string]$AttachedIsoRoot = "artifacts/virtualbox-validation",
    [switch]$RecreateIso,
    [switch]$AttachOriginalIso,
    [switch]$SkipInputOptimization,
    [switch]$DryRun,
    [switch]$NoStart,
    [switch]$Detach
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$iso = Join-Path $repoRoot $IsoPath

if (!$VBoxManagePath) {
    $command = Get-Command VBoxManage.exe -ErrorAction SilentlyContinue
    if ($command) {
        $VBoxManagePath = $command.Source
    }
    else {
        $defaultPath = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
        if (Test-Path $defaultPath) {
            $VBoxManagePath = $defaultPath
        }
    }
}

if (!$VBoxManagePath -or !(Test-Path $VBoxManagePath)) {
    throw "VBoxManage.exe was not found. Pass -VBoxManagePath or install VirtualBox."
}

if (!$Detach -and ($RecreateIso -or !(Test-Path $iso))) {
    & (Join-Path $PSScriptRoot "create-external-validation-iso.ps1") -OutputPath $IsoPath
}

if (!$Detach -and !(Test-Path $iso)) {
    throw "Validation ISO does not exist: $iso"
}

function Get-AttachedIsoPath {
    if ($AttachOriginalIso) {
        return $iso
    }

    $safeVmName = ($VmName -replace '[^A-Za-z0-9_.-]', '_')
    $copyRoot = Join-Path $repoRoot (Join-Path $AttachedIsoRoot $safeVmName)
    $copyPath = Join-Path $copyRoot "OcrSnip-ExternalValidationKit.iso"
    if (!$DryRun) {
        New-Item -ItemType Directory -Force -Path $copyRoot | Out-Null
        Copy-Item -LiteralPath $iso -Destination $copyPath -Force
    }

    return $copyPath
}

function Invoke-VBoxManage([string[]]$Arguments) {
    $display = "`"$VBoxManagePath`" $($Arguments -join ' ')"
    if ($DryRun) {
        Write-Host "[dry-run] $display"
        return
    }

    & $VBoxManagePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "VBoxManage failed with exit code $LASTEXITCODE for: $display"
    }
}

function Get-VmInfoWithRetry {
    $lastOutput = $null
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        $lastOutput = & $VBoxManagePath showvminfo $VmName --machinereadable 2>&1
        if ($LASTEXITCODE -eq 0) {
            return $lastOutput
        }

        if (($lastOutput | Out-String) -notmatch "lock request pending|VBOX_E_INVALID_OBJECT_STATE") {
            break
        }

        Start-Sleep -Milliseconds (250 * $attempt)
    }

    throw "VirtualBox VM was not found or could not be inspected: $VmName. Output: $($lastOutput | Out-String)"
}

$info = Get-VmInfoWithRetry
$stateLine = $info | Where-Object { $_ -like "VMState=*" } | Select-Object -First 1
$state = if ($stateLine -match 'VMState="([^"]+)"') { $Matches[1] } else { "unknown" }
if ($state -ne "poweroff" -and ($Detach -or !$NoStart)) {
    throw "VM '$VmName' is currently '$state'. Power it off before changing the attached validation ISO."
}

if ($Detach) {
    Invoke-VBoxManage @("storageattach", $VmName, "--storagectl", $ControllerName, "--port", "$Port", "--device", "$Device", "--type", "dvddrive", "--medium", "none")
    Write-Host "Validation ISO detached from VM '$VmName' on $ControllerName port $Port device $Device."
    return
}

$attachedIso = Get-AttachedIsoPath
Invoke-VBoxManage @("storageattach", $VmName, "--storagectl", $ControllerName, "--port", "$Port", "--device", "$Device", "--type", "dvddrive", "--medium", $attachedIso)
if (!$SkipInputOptimization -and $StartType -ne "headless") {
    Invoke-VBoxManage @("modifyvm", $VmName, "--mouse", "usbtablet")
}

if (!$NoStart) {
    Invoke-VBoxManage @("startvm", $VmName, "--type", $StartType)
}

Write-Host "Validation ISO attached to VM '$VmName' on $ControllerName port $Port device $Device."
if (!$SkipInputOptimization -and $StartType -ne "headless") {
    Write-Host "VM pointing device set to usbtablet for more reliable GUI validation input."
}
if (!$NoStart) {
    Write-Host "Inside the VM, open the mounted CD drive and run the matching launcher, for example Run-Windows10.cmd."
}
Write-Host "After the kit finishes, copy external-validation-export.zip back to this repo and import it with:"
Write-Host "powershell -ExecutionPolicy Bypass -File tools\import-external-validation.ps1 -SourcePath path\to\external-validation-export.zip"
