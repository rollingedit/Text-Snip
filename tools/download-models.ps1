param(
    [string]$ManifestPath = "tools/model-hashes.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$manifest = Get-Content (Join-Path $repoRoot $ManifestPath) -Raw | ConvertFrom-Json

foreach ($model in $manifest.models) {
    $target = Join-Path $repoRoot $model.path
    $directory = Split-Path $target -Parent
    New-Item -ItemType Directory -Force -Path $directory | Out-Null

    if (!(Test-Path $target)) {
        $url = "https://huggingface.co/$($model.repo)/resolve/$($model.revision)/$($model.file)"
        Write-Host "Downloading $($model.repo) $($model.file)"
        Invoke-WebRequest -Uri $url -OutFile $target
    }
}

& (Join-Path $PSScriptRoot "verify-models.ps1") -ManifestPath $ManifestPath
