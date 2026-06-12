param(
    [string]$OutputPath = "artifacts/reports/compatibility-report.txt"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$output = Join-Path $repoRoot $OutputPath
New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
Add-Type -AssemblyName System.Windows.Forms

$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$screens = [System.Windows.Forms.Screen]::AllScreens
$dotnetInfo = & (Join-Path $repoRoot ".dotnet/dotnet.exe") --info

$lines = @()
$lines += "Generated: $(Get-Date -Format o)"
$lines += "OS: $($os.Caption) $($os.Version)"
$lines += "Architecture: $($os.OSArchitecture)"
$lines += "CPU vendor/name: $($cpu.Manufacturer) / $($cpu.Name)"
$lines += "Logical processors: $($cpu.NumberOfLogicalProcessors)"
$lines += "Screens:"
foreach ($screen in $screens) {
    $lines += "  Primary=$($screen.Primary) Bounds=$($screen.Bounds) WorkingArea=$($screen.WorkingArea)"
}
$lines += ""
$lines += ".NET:"
$lines += $dotnetInfo

$lines | Set-Content $output
Write-Host "Compatibility report written to $output"
