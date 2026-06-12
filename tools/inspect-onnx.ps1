param(
    [Parameter(Mandatory = $true)]
    [string]$ModelPath
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src/OcrSnip.Tools.OnnxInspect/OcrSnip.Tools.OnnxInspect.csproj"

if (!(Test-Path $project)) {
    throw "Inspector project not found. Build tooling is expected at src/OcrSnip.Tools.OnnxInspect."
}

& (Join-Path $repoRoot ".dotnet/dotnet.exe") run --configuration Release --project $project -- $ModelPath
