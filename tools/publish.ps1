param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "artifacts/publish/OcrSnip"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnet = Join-Path $repoRoot ".dotnet/dotnet.exe"

& (Join-Path $PSScriptRoot "verify-models.ps1")
& $dotnet publish (Join-Path $repoRoot "src/OcrSnip.App/OcrSnip.App.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=false `
    -o (Join-Path $repoRoot $OutputPath)

$hashPath = Join-Path $repoRoot "artifacts/publish/SHA256SUMS.txt"
New-Item -ItemType Directory -Force -Path (Split-Path $hashPath -Parent) | Out-Null
Get-ChildItem (Join-Path $repoRoot $OutputPath) -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $relative = Resolve-Path -Relative $_.FullName
        $hash = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant()
        "$hash  $relative"
    } | Set-Content $hashPath
