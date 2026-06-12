param(
    [string]$FixtureRoot = "Fixtures/generated"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$root = Join-Path $repoRoot $FixtureRoot
New-Item -ItemType Directory -Force -Path $root | Out-Null

Add-Type -AssemblyName System.Drawing

$fixtures = @(
    @{ Name = "simple_text"; Text = "OCR TEST"; Width = 720; Height = 220; Size = 56 },
    @{ Name = "code_path"; Text = "C:\\Temp\\ocr_test.txt"; Width = 920; Height = 220; Size = 44 },
    @{ Name = "terminal"; Text = "dotnet test OCRSNIP"; Width = 980; Height = 220; Size = 44 }
)

foreach ($fixture in $fixtures) {
    $png = Join-Path $root "$($fixture.Name).png"
    $expected = Join-Path $root "$($fixture.Name).expected.txt"
    $bitmap = New-Object System.Drawing.Bitmap($fixture.Width, $fixture.Height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::White)
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
            $font = New-Object System.Drawing.Font("Arial", $fixture.Size, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            try {
                $graphics.DrawString($fixture.Text, $font, [System.Drawing.Brushes]::Black, 28, 72)
            }
            finally {
                $font.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
        Set-Content -Path $expected -Value $fixture.Text
    }
    finally {
        $bitmap.Dispose()
    }
}

Write-Host "Generated private fixtures in $root"
