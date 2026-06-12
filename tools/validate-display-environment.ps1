param(
    [ValidateSet(100, 125, 150)]
    [int]$ExpectedDpiScale,
    [switch]$RequireMixedDpi,
    [switch]$RequireNegativeVirtualMonitor,
    [switch]$MultiMonitorCapturePassed
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$compatibilityReport = Join-Path $repoRoot "artifacts/reports/compatibility-report.txt"

& (Join-Path $PSScriptRoot "collect-compatibility-report.ps1") | Out-Null
$report = Get-Content $compatibilityReport -Raw

if ($ExpectedDpiScale -and $report -notmatch "DPI scale: $ExpectedDpiScale%") {
    throw "Expected DPI scale $ExpectedDpiScale% was not detected. See $compatibilityReport"
}

if ($RequireMixedDpi -and $report -notmatch "Mixed DPI: True") {
    throw "Mixed DPI was required but not detected. See $compatibilityReport"
}

if ($RequireNegativeVirtualMonitor -and $report -notmatch "Bounds=\{X=-|Bounds=\{X=[^,]+,Y=-") {
    throw "A negative virtual monitor coordinate was required but not detected. See $compatibilityReport"
}

if ($MultiMonitorCapturePassed) {
    $screenCount = ([regex]::Matches($report, "Primary=")).Count
    if ($screenCount -lt 2) {
        throw "Multi-monitor capture was marked passed, but fewer than two screens were detected."
    }
}

& (Join-Path $PSScriptRoot "verify-hotkey-snip.ps1") | Out-Null
& (Join-Path $PSScriptRoot "record-external-validation.ps1") -MultiMonitorCapturePassed:$MultiMonitorCapturePassed | Out-Null
Write-Host "Display environment validation passed."
