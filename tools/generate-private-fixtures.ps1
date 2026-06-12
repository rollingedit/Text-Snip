param(
    [string]$FixtureRoot = "Fixtures/generated"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$root = Join-Path $repoRoot $FixtureRoot
New-Item -ItemType Directory -Force -Path $root | Out-Null

Add-Type -AssemblyName System.Drawing

$fixtures = @(
    @{ Name = "simple_text"; Text = "OCR TEST"; Width = 720; Height = 220; Size = 56; Font = "Arial"; Back = "White"; Fore = "Black" },
    @{ Name = "code_path"; Text = "C:\\Temp\\ocr_test.txt"; Width = 920; Height = 220; Size = 44; Font = "Consolas"; Back = "White"; Fore = "Black" },
    @{ Name = "terminal"; Text = "dotnet test OCRSNIP"; Width = 980; Height = 220; Size = 44; Font = "Consolas"; Back = "Black"; Fore = "White" },
    @{ Name = "url"; Text = "https://example.test/docs"; Width = 980; Height = 220; Size = 42; Font = "Arial"; Back = "White"; Fore = "Navy" },
    @{ Name = "json"; Text = '{ "ok": true, "count": 42 }'; Width = 980; Height = 220; Size = 42; Font = "Consolas"; Back = "White"; Fore = "Black" },
    @{ Name = "powershell"; Text = "Get-ChildItem -Force"; Width = 880; Height = 220; Size = 44; Font = "Consolas"; Back = "Black"; Fore = "LightGreen" },
    @{ Name = "small_ui"; Text = "Settings  Privacy  Clipboard"; Width = 900; Height = 180; Size = 28; Font = "Segoe UI"; Back = "White"; Fore = "Black" },
    @{ Name = "dark_ui"; Text = "Build succeeded"; Width = 760; Height = 180; Size = 36; Font = "Segoe UI"; Back = "FromArgb:32,32,32"; Fore = "White" },
    @{ Name = "low_contrast"; Text = "No readable text found"; Width = 860; Height = 180; Size = 34; Font = "Segoe UI"; Back = "Gainsboro"; Fore = "DimGray" },
    @{ Name = "file_name"; Text = "OcrSnip.App.csproj"; Width = 820; Height = 180; Size = 38; Font = "Consolas"; Back = "White"; Fore = "Black" },
    @{ Name = "snake_case"; Text = "copy_mode_raw_lines"; Width = 840; Height = 180; Size = 40; Font = "Consolas"; Back = "White"; Fore = "Black" },
    @{ Name = "symbols"; Text = "[]{}<>/\\_|`~"; Width = 780; Height = 180; Size = 44; Font = "Consolas"; Back = "White"; Fore = "Black" },
    @{ Name = "numbers"; Text = "1234567890 98.76%"; Width = 820; Height = 180; Size = 44; Font = "Arial"; Back = "White"; Fore = "Black" },
    @{ Name = "mixed_case"; Text = "PerMonitorV2 DPI"; Width = 760; Height = 180; Size = 42; Font = "Segoe UI"; Back = "White"; Fore = "Black" },
    @{ Name = "error_message"; Text = "Clipboard busy - text opened"; Width = 920; Height = 180; Size = 36; Font = "Segoe UI"; Back = "White"; Fore = "DarkRed" },
    @{ Name = "menu_text"; Text = "Start snip   Settings   Exit"; Width = 900; Height = 180; Size = 34; Font = "Segoe UI"; Back = "White"; Fore = "Black" },
    @{ Name = "table_row"; Text = "Name     Status     Time"; Width = 920; Height = 180; Size = 36; Font = "Consolas"; Back = "White"; Fore = "Black" },
    @{ Name = "quoted"; Text = '"Hotkey already in use"'; Width = 860; Height = 180; Size = 38; Font = "Arial"; Back = "White"; Fore = "Black" },
    @{ Name = "underscores"; Text = "__init__ OCR_RESULT"; Width = 820; Height = 180; Size = 40; Font = "Consolas"; Back = "White"; Fore = "Black" },
    @{ Name = "short_word"; Text = "OK"; Width = 360; Height = 160; Size = 64; Font = "Arial"; Back = "White"; Fore = "Black" }
)

function Convert-Color($value) {
    if ($value -like "FromArgb:*") {
        $parts = $value.Substring(9).Split(",") | ForEach-Object { [int]$_.Trim() }
        return [System.Drawing.Color]::FromArgb($parts[0], $parts[1], $parts[2])
    }

    return [System.Drawing.Color]::FromName($value)
}

foreach ($fixture in $fixtures) {
    $png = Join-Path $root "$($fixture.Name).png"
    $expected = Join-Path $root "$($fixture.Name).expected.txt"
    $bitmap = New-Object System.Drawing.Bitmap($fixture.Width, $fixture.Height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear((Convert-Color $fixture.Back))
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
            $font = New-Object System.Drawing.Font($fixture.Font, $fixture.Size, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            $brush = New-Object System.Drawing.SolidBrush((Convert-Color $fixture.Fore))
            try {
                $graphics.DrawString($fixture.Text, $font, $brush, 28, [Math]::Max(24, [int](($fixture.Height - $fixture.Size) / 2)))
            }
            finally {
                $brush.Dispose()
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
