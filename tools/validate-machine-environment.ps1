param(
    [ValidateSet("Windows10", "Windows11")]
    [string]$ExpectedWindows,
    [ValidateSet("Intel", "AMD")]
    [string]$ExpectedCpuVendor,
    [switch]$RunChecks
)

$ErrorActionPreference = "Stop"
$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1

if ($ExpectedWindows) {
    $isWindows10 = $os.Version -like "10.0.1*"
    $isWindows11 = $os.Version -like "10.0.2*"
    if ($ExpectedWindows -eq "Windows10" -and !$isWindows10) {
        throw "Expected Windows 10 x64, but detected $($os.Caption) $($os.Version)."
    }

    if ($ExpectedWindows -eq "Windows11" -and !$isWindows11) {
        throw "Expected Windows 11 x64, but detected $($os.Caption) $($os.Version)."
    }
}

if ($ExpectedCpuVendor) {
    $manufacturer = "$($cpu.Manufacturer) $($cpu.Name)"
    if ($ExpectedCpuVendor -eq "Intel" -and $manufacturer -notmatch "Intel|GenuineIntel") {
        throw "Expected Intel CPU, but detected $manufacturer."
    }

    if ($ExpectedCpuVendor -eq "AMD" -and $manufacturer -notmatch "AMD") {
        throw "Expected AMD CPU, but detected $manufacturer."
    }
}

if ($RunChecks) {
    & (Join-Path $PSScriptRoot "record-external-validation.ps1") -RunChecks
}
else {
    & (Join-Path $PSScriptRoot "record-external-validation.ps1")
}

Write-Host "Machine environment validation passed for $($os.Caption) / $($cpu.Name)."
