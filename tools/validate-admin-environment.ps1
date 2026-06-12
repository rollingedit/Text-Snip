param(
    [switch]$RunChecks
)

$ErrorActionPreference = "Stop"
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$identity
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (!$isAdmin) {
    throw "Admin validation must be run from an elevated administrator PowerShell session."
}

if ($RunChecks) {
    & (Join-Path $PSScriptRoot "record-external-validation.ps1") -RunChecks
}
else {
    & (Join-Path $PSScriptRoot "record-external-validation.ps1")
}

Write-Host "Admin environment validation passed."
