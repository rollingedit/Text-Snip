param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "artifacts/publish/OcrSnip"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnet = Join-Path $repoRoot ".dotnet/dotnet.exe"

& (Join-Path $PSScriptRoot "verify-models.ps1")
$publishDir = Join-Path $repoRoot $OutputPath
& $dotnet publish (Join-Path $repoRoot "src/OcrSnip.App/OcrSnip.App.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$zipPath = Join-Path $repoRoot "artifacts/publish/OcrSnip-Portable-x64.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath
}
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

$hashPath = Join-Path $repoRoot "artifacts/publish/SHA256SUMS.txt"
New-Item -ItemType Directory -Force -Path (Split-Path $hashPath -Parent) | Out-Null
Get-ChildItem $publishDir -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $relative = Resolve-Path -Relative $_.FullName
        $hash = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant()
        "$hash  $relative"
    } | Set-Content $hashPath
