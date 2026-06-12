param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "CurrentMachineFull",
        "Windows10",
        "Windows10Amd",
        "AmdCpu",
        "Admin",
        "Dpi100",
        "Dpi125",
        "Dpi150",
        "MixedNegativeMultiMonitor",
        "PostRebootPrepare",
        "PostRebootComplete",
        "Readiness",
        "Status"
    )]
    [string]$Profile
)

$ErrorActionPreference = "Stop"

switch ($Profile) {
    "CurrentMachineFull" {
        & (Join-Path $PSScriptRoot "record-external-validation.ps1") -RunChecks
        break
    }
    "Windows10" {
        & (Join-Path $PSScriptRoot "validate-machine-environment.ps1") -ExpectedWindows Windows10 -RunChecks
        break
    }
    "Windows10Amd" {
        & (Join-Path $PSScriptRoot "validate-machine-environment.ps1") -ExpectedWindows Windows10 -ExpectedCpuVendor AMD -RunChecks
        break
    }
    "AmdCpu" {
        & (Join-Path $PSScriptRoot "validate-machine-environment.ps1") -ExpectedCpuVendor AMD -RunChecks
        break
    }
    "Admin" {
        & (Join-Path $PSScriptRoot "validate-admin-environment.ps1") -RunChecks
        break
    }
    "Dpi100" {
        & (Join-Path $PSScriptRoot "validate-display-environment.ps1") -ExpectedDpiScale 100
        break
    }
    "Dpi125" {
        & (Join-Path $PSScriptRoot "validate-display-environment.ps1") -ExpectedDpiScale 125
        break
    }
    "Dpi150" {
        & (Join-Path $PSScriptRoot "validate-display-environment.ps1") -ExpectedDpiScale 150
        break
    }
    "MixedNegativeMultiMonitor" {
        & (Join-Path $PSScriptRoot "validate-display-environment.ps1") -ExpectedDpiScale 100 -RequireMixedDpi -RequireNegativeVirtualMonitor -MultiMonitorCapturePassed
        break
    }
    "PostRebootPrepare" {
        & (Join-Path $PSScriptRoot "prepare-post-reboot-validation.ps1")
        break
    }
    "PostRebootComplete" {
        & (Join-Path $PSScriptRoot "complete-post-reboot-validation.ps1")
        break
    }
    "Readiness" {
        & (Join-Path $PSScriptRoot "verify-ship-readiness.ps1")
        break
    }
    "Status" {
        & (Join-Path $PSScriptRoot "write-validation-status.ps1")
        break
    }
}
