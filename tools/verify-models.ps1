param(
    [string]$ManifestPath = "tools/model-hashes.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$manifest = Get-Content (Join-Path $repoRoot $ManifestPath) -Raw | ConvertFrom-Json

foreach ($model in $manifest.models) {
    $path = Join-Path $repoRoot $model.path
    if (!(Test-Path $path)) {
        throw "Missing model file: $($model.path)"
    }

    $actual = (Get-FileHash -Algorithm SHA256 $path).Hash.ToLowerInvariant()
    if ($actual -ne $model.sha256) {
        throw "Hash mismatch for $($model.path): expected $($model.sha256), got $actual"
    }

    Write-Host "OK $($model.path)"
}
