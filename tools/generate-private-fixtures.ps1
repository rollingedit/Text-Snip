param(
    [string]$FixtureRoot = "Fixtures/generated"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$root = Join-Path $repoRoot $FixtureRoot
New-Item -ItemType Directory -Force -Path $root | Out-Null

Add-Type -AssemblyName System.Drawing

function Convert-Color($value) {
    if ($value -like "FromArgb:*") {
        $parts = $value.Substring(9).Split(",") | ForEach-Object { [int]$_.Trim() }
        return [System.Drawing.Color]::FromArgb($parts[0], $parts[1], $parts[2])
    }

    return [System.Drawing.Color]::FromName($value)
}

function TextItem(
    [string]$Text,
    [int]$X,
    [int]$Y,
    [int]$Size = 32,
    [string]$Font = "Segoe UI",
    [string]$Fore = "Black",
    [string]$Style = "Regular",
    [double]$Angle = 0
) {
    return @{
        Type = "Text"
        Text = $Text
        X = $X
        Y = $Y
        Size = $Size
        Font = $Font
        Fore = $Fore
        Style = $Style
        Angle = $Angle
    }
}

function RectItem(
    [int]$X,
    [int]$Y,
    [int]$Width,
    [int]$Height,
    [string]$Fill,
    [string]$Stroke = $null
) {
    return @{
        Type = "Rect"
        X = $X
        Y = $Y
        Width = $Width
        Height = $Height
        Fill = $Fill
        Stroke = $Stroke
    }
}

function LineItem(
    [int]$X1,
    [int]$Y1,
    [int]$X2,
    [int]$Y2,
    [string]$Fore = "LightGray",
    [int]$Width = 1
) {
    return @{
        Type = "Line"
        X1 = $X1
        Y1 = $Y1
        X2 = $X2
        Y2 = $Y2
        Fore = $Fore
        Width = $Width
    }
}

function EllipseItem(
    [int]$X,
    [int]$Y,
    [int]$Width,
    [int]$Height,
    [string]$Fill,
    [string]$Stroke = $null
) {
    return @{
        Type = "Ellipse"
        X = $X
        Y = $Y
        Width = $Width
        Height = $Height
        Fill = $Fill
        Stroke = $Stroke
    }
}

function Fixture(
    [string]$Name,
    [string]$Expected,
    [int]$Width,
    [int]$Height,
    [string]$Category,
    [string]$Risk,
    [object[]]$Items,
    [string]$Back = "White",
    [string[]]$PrimaryTokens = @(),
    [string[]]$NoiseTokens = @(),
    [bool]$RequirePrimary = $true,
    [bool]$ShouldWarnEdge = $false,
    [string]$Notes = "",
    [string[]]$SpacingPhrases = $null
) {
    if ($PrimaryTokens.Count -eq 0) {
        $PrimaryTokens = @($Expected)
    }

    if ($null -eq $SpacingPhrases) {
        $SpacingPhrases = if ($Expected -match "\S\s+\S") { @($Expected) } else { @() }
    }

    return @{
        Name = $Name
        Expected = $Expected
        Width = $Width
        Height = $Height
        Back = $Back
        Category = $Category
        Risk = $Risk
        Items = $Items
        PrimaryTokens = $PrimaryTokens
        NoiseTokens = $NoiseTokens
        SpacingPhrases = $SpacingPhrases
        RequirePrimary = $RequirePrimary
        ShouldWarnEdge = $ShouldWarnEdge
        Notes = $Notes
    }
}

$emojiSymbols = ([string][char]0x263A) + " " + ([string][char]0x2713) + " " + ([string][char]0x26A0)
$emojiPrimary = ([string][char]0x263A) + " OK"
$cyrillicTest = -join ([char[]](0x0442, 0x0435, 0x0441, 0x0442))
$mixedCyrillic = "OCR $cyrillicTest OK"
$katakanaTest = -join ([char[]](0x30C6, 0x30B9, 0x30C8))
$mixedJapanese = "OCR $katakanaTest OK"
$accentedLatin = "caf" + ([string][char]0x00E9) + " d" + ([string][char]0x00E9) + "j" + ([string][char]0x00E0) + " vu fa" + ([string][char]0x00E7) + "ade"
$arabicHello = -join ([char[]](0x0645, 0x0631, 0x062D, 0x0628, 0x0627))

$fixtures = @(
    (Fixture "simple_text" "OCR TEST" 720 220 "baseline" "clean centered text" @(
        (TextItem "OCR TEST" 28 78 56 "Arial" "Black" "Bold")
    )),
    (Fixture "code_path" "C:\Temp\ocr_test.txt" 920 220 "baseline" "path punctuation" @(
        (TextItem "C:\Temp\ocr_test.txt" 28 84 44 "Consolas" "Black" "Bold")
    )),
    (Fixture "terminal" "dotnet test OCRSNIP" 980 220 "baseline" "dark terminal text" @(
        (TextItem "dotnet test OCRSNIP" 28 84 44 "Consolas" "White" "Bold")
    ) "Black"),
    (Fixture "url" "https://example.test/docs" 980 220 "baseline" "URL punctuation" @(
        (TextItem "https://example.test/docs" 28 84 42 "Arial" "Navy" "Bold")
    )),
    (Fixture "json" '{ "ok": true, "count": 42 }' 980 220 "baseline" "JSON punctuation" @(
        (TextItem '{ "ok": true, "count": 42 }' 28 84 42 "Consolas" "Black" "Bold")
    )),
    (Fixture "powershell" "Get-ChildItem -Force" 880 220 "baseline" "shell punctuation" @(
        (TextItem "Get-ChildItem -Force" 28 84 44 "Consolas" "LightGreen" "Bold")
    ) "Black"),
    (Fixture "small_ui" "Settings Privacy Clipboard" 900 180 "baseline" "small UI labels" @(
        (TextItem "Settings  Privacy  Clipboard" 28 70 28 "Segoe UI" "Black" "Regular")
    )),
    (Fixture "dark_ui" "Build succeeded" 760 180 "baseline" "dark UI text" @(
        (TextItem "Build succeeded" 28 70 36 "Segoe UI" "White" "Bold")
    ) "FromArgb:32,32,32"),
    (Fixture "low_contrast" "No readable text found" 860 180 "baseline" "low contrast text" @(
        (TextItem "No readable text found" 28 70 34 "Segoe UI" "DimGray" "Regular")
    ) "Gainsboro"),
    (Fixture "file_name" "OcrSnip.App.csproj" 820 180 "baseline" "filename punctuation" @(
        (TextItem "OcrSnip.App.csproj" 28 70 38 "Consolas" "Black" "Regular")
    )),
    (Fixture "snake_case" "copy_mode_raw_lines" 840 180 "baseline" "underscores" @(
        (TextItem "copy_mode_raw_lines" 28 70 40 "Consolas" "Black" "Regular")
    )),
    (Fixture "symbols" "[]{}<>/\_|`~" 780 180 "baseline" "symbols" @(
        (TextItem "[]{}<>/\_|`~" 28 70 44 "Consolas" "Black" "Regular")
    )),
    (Fixture "numbers" "1234567890 98.76%" 820 180 "baseline" "numbers and percent" @(
        (TextItem "1234567890 98.76%" 28 70 44 "Arial" "Black" "Bold")
    )),
    (Fixture "mixed_case" "PerMonitorV2 DPI" 760 180 "baseline" "mixed case" @(
        (TextItem "PerMonitorV2 DPI" 28 70 42 "Segoe UI" "Black" "Regular")
    )),
    (Fixture "error_message" "Clipboard busy - text opened" 920 180 "baseline" "error message" @(
        (TextItem "Clipboard busy - text opened" 28 70 36 "Segoe UI" "DarkRed" "Regular")
    )),
    (Fixture "menu_text" "Start snip Settings Exit" 900 180 "baseline" "menu labels" @(
        (TextItem "Start snip   Settings   Exit" 28 70 34 "Segoe UI" "Black" "Regular")
    )),
    (Fixture "table_row" "Name Status Time" 920 180 "baseline" "table header spacing" @(
        (TextItem "Name     Status     Time" 28 70 36 "Consolas" "Black" "Regular")
    )),
    (Fixture "quoted" '"Hotkey already in use"' 860 180 "baseline" "quoted message" @(
        (TextItem '"Hotkey already in use"' 28 70 38 "Arial" "Black" "Regular")
    )),
    (Fixture "underscores" "__init__ OCR_RESULT" 820 180 "baseline" "leading underscores" @(
        (TextItem "__init__ OCR_RESULT" 28 70 40 "Consolas" "Black" "Regular")
    )),
    (Fixture "short_word" "OK" 360 160 "baseline" "short word" @(
        (TextItem "OK" 28 48 64 "Arial" "Black" "Bold")
    ) "White" @("OK")),

    (Fixture "dense_settings_panel" "General Capture Clipboard Startup Hotkey" 980 440 "dense" "many UI labels close together" @(
        (TextItem "General" 24 28 26 "Segoe UI" "Black" "Bold")
        (TextItem "Capture" 24 78 24 "Segoe UI" "Black")
        (TextItem "Small text boost" 52 118 21 "Segoe UI" "Black")
        (TextItem "Clipboard" 24 168 24 "Segoe UI" "Black")
        (TextItem "Copy mode: Raw lines" 52 208 21 "Segoe UI" "Black")
        (TextItem "Startup" 24 258 24 "Segoe UI" "Black")
        (TextItem "Launch at login" 52 298 21 "Segoe UI" "Black")
        (TextItem "Hotkey" 520 78 24 "Segoe UI" "Black")
        (TextItem "Ctrl + Shift + O" 548 118 21 "Segoe UI" "Black")
        (TextItem "Reset" 548 168 21 "Segoe UI" "DimGray")
        (LineItem 480 24 480 390 "Gainsboro" 2)
    ) "White" @("Capture", "Clipboard", "Hotkey", "Ctrl")),
    (Fixture "dense_table_rows" "Invoice Status Amount Due" 1060 420 "dense" "small table columns and repeated text" @(
        (TextItem "Invoice   Status      Amount     Due" 24 24 28 "Consolas" "Black" "Bold")
        (LineItem 20 64 1010 64 "Silver" 1)
        (TextItem "A-1042    Paid        $98.76     Today" 24 82 24 "Consolas" "Black")
        (TextItem "A-1043    Pending     $12.00     Jun 18" 24 122 24 "Consolas" "Black")
        (TextItem "A-1044    Failed      $145.20    Jun 19" 24 162 24 "Consolas" "DarkRed")
        (TextItem "A-1045    Review      $7.50      Jun 20" 24 202 24 "Consolas" "Black")
        (TextItem "Subtotal                         $263.46" 24 274 24 "Consolas" "Black" "Bold")
    ) "White" @("Invoice", "Status", "Amount", "A-1044")),
    (Fixture "messy_terminal_log" "WARN OCR retry Clipboard busy" 1100 430 "dense" "dense terminal with prompts and warnings" @(
        (TextItem "PS E:\repo> dotnet test -c Release" 20 18 24 "Consolas" "LightGray")
        (TextItem "[00:01:12] PASS OcrSnip.Tests" 20 58 24 "Consolas" "LightGreen")
        (TextItem "[00:01:13] WARN OCR retry #2" 20 98 24 "Consolas" "Yellow")
        (TextItem "[00:01:14] Clipboard busy; using fallback window" 20 138 24 "Consolas" "White")
        (TextItem "[00:01:15] PASS privacy scan" 20 178 24 "Consolas" "LightGreen")
        (TextItem "Elapsed 00:01:15.832" 20 238 24 "Consolas" "LightGray")
    ) "Black" @("WARN", "OCR", "Clipboard")),
    (Fixture "dense_receipt_columns" "Subtotal Tax Total Approved" 620 620 "dense" "receipt-like narrow text" @(
        (TextItem "OCR SNIP STORE" 52 28 28 "Consolas" "Black" "Bold")
        (TextItem "LOCAL CPU OCR" 52 72 20 "Consolas" "Black")
        (LineItem 42 112 560 112 "Silver" 1)
        (TextItem "Snip overlay        0.00" 52 134 22 "Consolas" "Black")
        (TextItem "Model files         0.00" 52 174 22 "Consolas" "Black")
        (TextItem "Privacy             0.00" 52 214 22 "Consolas" "Black")
        (TextItem "Subtotal            0.00" 52 292 22 "Consolas" "Black")
        (TextItem "Tax                 0.00" 52 332 22 "Consolas" "Black")
        (TextItem "Total               0.00" 52 392 28 "Consolas" "Black" "Bold")
        (TextItem "Approved" 52 468 24 "Consolas" "Black" "Bold")
    ) "White" @("Subtotal", "Tax", "Total", "Approved")),
    (Fixture "mixed_font_dialog" "Save changes before closing" 900 360 "dense" "mixed font weights and button labels" @(
        (RectItem 0 0 900 56 "FromArgb:245,245,245" "Silver")
        (TextItem "OCR Snip Settings" 22 14 24 "Segoe UI" "Black" "Bold")
        (TextItem "Save changes before closing?" 48 92 32 "Segoe UI" "Black" "Regular")
        (TextItem "Unsaved hotkey and launch settings will be applied." 48 146 20 "Segoe UI" "DimGray")
        (RectItem 512 260 106 46 "White" "Gray")
        (RectItem 638 260 106 46 "White" "Gray")
        (RectItem 764 260 106 46 "FromArgb:0,120,215" "FromArgb:0,120,215")
        (TextItem "Discard" 528 272 20 "Segoe UI" "Black")
        (TextItem "Cancel" 660 272 20 "Segoe UI" "Black")
        (TextItem "Save" 798 272 20 "Segoe UI" "White" "Bold")
    ) "White" @("Save", "changes", "closing")),

    (Fixture "clipped_left_selection" "Clipboard fallback opened" 720 190 "clipped" "left edge cuts beginning of intended text" @(
        (TextItem "Clipboard fallback opened" -42 56 36 "Segoe UI" "Black" "Regular")
    ) "White" @("fallback", "opened") @() $true $true "Simulates starting the snip slightly inside the first word."),
    (Fixture "clipped_right_selection" "Hotkey already in use" 620 190 "clipped" "right edge cuts ending of intended text" @(
        (TextItem "Hotkey already in use" 34 56 38 "Segoe UI" "Black" "Regular")
    ) "White" @("Hotkey", "already") @() $true $true "The phrase extends near or beyond the right boundary."),
    (Fixture "clipped_top_selection" "OCR result copied" 760 150 "clipped" "top edge cuts ascenders" @(
        (TextItem "OCR result copied" 38 -12 42 "Segoe UI" "Black" "Bold")
    ) "White" @("result", "copied") @() $true $true "Top of text is intentionally outside the captured region."),
    (Fixture "clipped_bottom_selection" "No readable text found" 820 122 "clipped" "bottom edge cuts descenders" @(
        (TextItem "No readable text found" 28 72 42 "Segoe UI" "Black" "Regular")
    ) "White" @("readable", "text") @() $true $true "Bottom of text is intentionally outside the captured region."),
    (Fixture "border_touching_text" "Release verification passed" 980 190 "clipped" "intended text touches the border" @(
        (TextItem "Release verification passed" 1 58 38 "Segoe UI" "Black" "Bold")
    ) "White" @("Release", "verification", "passed") @() $true $true "User selected exactly to the text edge."),
    (Fixture "half_line_capture" "paddle parity comparison written" 920 128 "clipped" "only lower half of a text line is visible" @(
        (TextItem "paddle parity comparison written" 24 -4 38 "Consolas" "Black" "Regular")
        (TextItem "fixture results complete" 24 82 26 "Consolas" "DimGray" "Regular")
    ) "White" @("parity", "comparison", "fixture") @() $true $true "Captures an incomplete top line plus a second line."),

    (Fixture "tiny_neighbor_top" "Copied OCR text" 760 220 "edge_noise" "tiny text from row above enters capture" @(
        (TextItem "status hidden toolbar" 20 0 12 "Segoe UI" "Gray" "Regular")
        (TextItem "Copied OCR text" 34 74 42 "Segoe UI" "Black" "Bold")
    ) "White" @("Copied", "OCR", "text") @("status", "toolbar") $true $true),
    (Fixture "tiny_neighbor_footer" "Build succeeded" 760 220 "edge_noise" "footer text enters bottom edge" @(
        (TextItem "Build succeeded" 34 54 42 "Segoe UI" "Black" "Bold")
        (TextItem "v1.0.0  unsigned local build" 22 194 14 "Segoe UI" "Gray" "Regular")
    ) "White" @("Build", "succeeded") @("unsigned", "local") $true $true),
    (Fixture "left_edge_fragment" "Settings saved" 760 220 "edge_noise" "fragment of neighbor column appears on left edge" @(
        (TextItem "Pr" -9 76 30 "Segoe UI" "Gray" "Regular")
        (TextItem "Settings saved" 72 72 42 "Segoe UI" "Black" "Bold")
    ) "White" @("Settings", "saved") @("Pr") $true $true),
    (Fixture "right_edge_fragment" "No text found" 760 220 "edge_noise" "fragment of neighbor column appears on right edge" @(
        (TextItem "No text found" 34 72 42 "Segoe UI" "Black" "Bold")
        (TextItem "Retry" 714 78 24 "Segoe UI" "Gray" "Regular")
    ) "White" @("No", "text", "found") @("Retry") $true $true),
    (Fixture "faint_background_label" "Privacy verified" 820 240 "edge_noise" "faint background text behind main text" @(
        (TextItem "COPY MODE RAW LINES COPY MODE RAW LINES" 8 40 18 "Segoe UI" "Gainsboro" "Regular")
        (TextItem "Privacy verified" 42 84 44 "Segoe UI" "Black" "Bold")
        (TextItem "COPY MODE RAW LINES COPY MODE RAW LINES" 8 156 18 "Segoe UI" "Gainsboro" "Regular")
    ) "White" @("Privacy", "verified") @("COPY", "MODE") $true $false),
    (Fixture "overlapping_small_label" "Hotkey conflict detected" 920 260 "edge_noise" "small overlapping label near main line" @(
        (TextItem "Hotkey conflict detected" 44 92 42 "Segoe UI" "Black" "Bold")
        (TextItem "Win+Shift+O" 438 82 16 "Segoe UI" "Gray" "Regular")
    ) "White" @("Hotkey", "conflict", "detected") @("Ctrl", "Shift") $true $false),
    (Fixture "toolbar_bits_above" "Capture selected region" 880 240 "edge_noise" "icons and tiny labels above selected text" @(
        (TextItem "copy edit save ..." 18 8 14 "Segoe UI" "Gray" "Regular")
        (TextItem "Capture selected region" 38 86 42 "Segoe UI" "Black" "Bold")
    ) "White" @("Capture", "selected", "region") @("copy", "save") $true $true),

    (Fixture "colored_status_text" "Failed Pending Passed" 920 260 "messy" "colored status labels with whitespace between words" @(
        (TextItem "Failed" 32 80 42 "Segoe UI" "DarkRed" "Bold")
        (TextItem "Pending" 260 80 42 "Segoe UI" "DarkGoldenrod" "Bold")
        (TextItem "Passed" 540 80 42 "Segoe UI" "DarkGreen" "Bold")
        (TextItem "Status" 34 34 18 "Segoe UI" "DimGray" "Regular")
        (TextItem "Status" 262 34 18 "Segoe UI" "DimGray" "Regular")
        (TextItem "Status" 542 34 18 "Segoe UI" "DimGray" "Regular")
    ) "White" @("Failed", "Pending", "Passed") @("Status")),
    (Fixture "colored_dark_terminal" "ERROR retry succeeded" 980 270 "messy" "colored terminal words on dark background" @(
        (TextItem "ERROR" 28 68 34 "Consolas" "Tomato" "Bold")
        (TextItem "retry" 172 68 34 "Consolas" "Khaki" "Bold")
        (TextItem "succeeded" 316 68 34 "Consolas" "LightGreen" "Bold")
        (TextItem "worker pid=1420 elapsed=91ms" 28 136 20 "Consolas" "LightGray" "Regular")
    ) "FromArgb:24,24,24" @("ERROR", "retry", "succeeded")),
    (Fixture "tiny_low_resolution_ui" "copy raw lines" 360 112 "messy" "small low-resolution UI label" @(
        (TextItem "copy raw lines" 12 34 15 "Segoe UI" "Black" "Regular")
        (TextItem "Aa" 302 6 9 "Segoe UI" "Gray" "Regular")
    ) "White" @("copy", "raw", "lines") @("Aa") $true $true),
    (Fixture "tiny_low_contrast_code" "var totalCount = 42" 520 130 "messy" "small low-contrast code text" @(
        (TextItem "var totalCount = 42" 14 42 17 "Consolas" "DimGray" "Regular")
        (TextItem "Ln 14, Col 8" 12 104 10 "Consolas" "Silver" "Regular")
    ) "FromArgb:238,238,238" @("var", "totalCount", "42") @("Ln") $true $true),
    (Fixture "script_like_handwriting" "meet at noon" 760 260 "messy" "script-like handwriting font if available" @(
        (TextItem "meet at noon" 44 66 54 "Segoe Script" "FromArgb:25,25,25" "Regular")
        (LineItem 40 150 640 150 "Gainsboro" 1)
    ) "White" @("meet", "noon")),
    (Fixture "colored_spacing_phrase" "Local OCR only" 760 230 "messy" "colored multi-word phrase verifies spaces are not compressed" @(
        (TextItem "Local" 36 72 46 "Segoe UI" "RoyalBlue" "Bold")
        (TextItem "OCR" 230 72 46 "Segoe UI" "DarkSlateGray" "Bold")
        (TextItem "only" 398 72 46 "Segoe UI" "Purple" "Bold")
    ) "White" @("Local", "OCR", "only")),
    (Fixture "blue_background_white_text" "Sync completed" 760 220 "contrast" "white text on saturated blue background" @(
        (TextItem "Sync completed" 42 72 44 "Segoe UI" "White" "Bold")
    ) "FromArgb:0,120,215" @("Sync", "completed") @() $true $false "" @("Sync completed")),
    (Fixture "green_background_black_text" "Ready for capture" 820 220 "contrast" "dark text on saturated green background" @(
        (TextItem "Ready for capture" 42 72 42 "Segoe UI" "Black" "Bold")
    ) "FromArgb:100,210,120" @("Ready", "capture") @() $true $false "" @("Ready for capture")),
    (Fixture "red_background_yellow_text" "Warning retry now" 820 220 "contrast" "yellow warning text on red background" @(
        (TextItem "Warning retry now" 42 72 42 "Segoe UI" "Yellow" "Bold")
    ) "FromArgb:160,24,24" @("Warning", "retry", "now") @() $true $false "" @("Warning retry now")),
    (Fixture "faded_gray_on_white" "Faded secondary text" 820 220 "contrast" "faded gray text close to white background" @(
        (TextItem "Faded secondary text" 42 76 38 "Segoe UI" "FromArgb:158,158,158" "Regular")
    ) "White" @("Faded", "secondary", "text") @() $true $false "" @("Faded secondary text")),
    (Fixture "near_white_on_white" "Almost hidden text" 820 220 "contrast" "very low contrast text near background color" @(
        (TextItem "Almost hidden text" 42 76 38 "Segoe UI" "FromArgb:202,202,202" "Regular")
    ) "FromArgb:232,232,232" @("Almost", "hidden", "text") @() $false $false "Exploratory: documents the low-contrast floor."),
    (Fixture "near_black_on_black" "Dim terminal text" 820 220 "contrast" "very low contrast text on dark background" @(
        (TextItem "Dim terminal text" 42 76 38 "Consolas" "FromArgb:82,82,82" "Regular")
    ) "FromArgb:34,34,34" @("Dim", "terminal", "text") @() $false $false "Exploratory: documents the dark low-contrast floor."),
    (Fixture "slight_page_tilt" "Release checks passed" 980 300 "tilted" "whole snippet has a mild scanned-page tilt" @(
        (TextItem "Release checks passed" 72 96 42 "Segoe UI" "Black" "Bold" -5)
        (TextItem "local CPU OCR only" 92 172 24 "Segoe UI" "DimGray" "Regular" -5)
    ) "White" @("Release", "checks", "passed") @("local") $true $false "" @("Release checks passed")),
    (Fixture "steeper_page_tilt" "Clipboard copied text" 980 340 "tilted" "whole snippet has a steeper hand-selected tilt" @(
        (TextItem "Clipboard copied text" 80 126 42 "Segoe UI" "Black" "Bold" 11)
        (TextItem "Ctrl Shift O" 108 205 22 "Segoe UI" "DimGray" "Regular" 11)
    ) "White" @("Clipboard", "copied", "text") @("Ctrl") $true $false "" @("Clipboard copied text")),
    (Fixture "mixed_tilt_words" "Start snip now" 920 300 "tilted" "individual words have different small rotations" @(
        (TextItem "Start" 52 94 44 "Segoe UI" "Black" "Bold" -8)
        (TextItem "snip" 246 104 44 "Segoe UI" "Black" "Bold" 6)
        (TextItem "now" 418 92 44 "Segoe UI" "Black" "Bold" -3)
    ) "White" @("Start", "snip", "now") @() $true $false "" @("Start snip now")),
    (Fixture "sideways_margin_label" "Main text stays readable" 880 320 "rotated" "sideways margin label next to horizontal primary text" @(
        (TextItem "MARGIN" 36 258 22 "Segoe UI" "Gray" "Bold" -90)
        (TextItem "Main text stays readable" 128 104 40 "Segoe UI" "Black" "Bold")
    ) "White" @("Main", "text", "readable") @("MARGIN") $true $true),
    (Fixture "upside_down_secondary" "Primary text normal" 860 320 "rotated" "upside-down secondary text enters selected area" @(
        (TextItem "Primary text normal" 48 86 42 "Segoe UI" "Black" "Bold")
        (TextItem "upside down note" 648 244 22 "Segoe UI" "Gray" "Regular" 180)
    ) "White" @("Primary", "text", "normal") @("upside", "note") $true $true),
    (Fixture "sideways_primary_word" "Rotate" 500 500 "rotated" "primary word is sideways and may need orientation handling" @(
        (TextItem "Rotate" 212 420 54 "Segoe UI" "Black" "Bold" -90)
    ) "White" @("Rotate") @() $false $false "Exploratory: failure may mean orientation preprocessing is needed."),
    (Fixture "upside_down_primary_phrase" "Upside down text" 760 360 "rotated" "primary phrase is upside down" @(
        (TextItem "Upside down text" 646 260 40 "Segoe UI" "Black" "Bold" 180)
    ) "White" @("Upside", "down", "text") @() $false $false "Exploratory: failure may mean 180-degree retry is needed."),

    (Fixture "photo_street_sign" "MAIN ST 12" 980 560 "photo_scene" "street-sign text over a photo-like outdoor scene" @(
        (RectItem 0 0 980 330 "FromArgb:130,185,230")
        (RectItem 0 330 980 560 "FromArgb:96,108,86")
        (EllipseItem 680 42 140 88 "FromArgb:250,235,150")
        (RectItem 92 86 236 132 "FromArgb:22,112,63" "White")
        (TextItem "MAIN ST" 122 118 38 "Arial" "White" "Bold")
        (TextItem "12" 220 166 30 "Arial" "White" "Bold")
        (LineItem 210 218 210 410 "FromArgb:80,80,80" 8)
        (RectItem 520 248 190 78 "FromArgb:230,230,210" "FromArgb:90,90,90")
        (TextItem "BUS" 574 266 30 "Arial" "FromArgb:30,30,30" "Bold")
    ) "White" @("MAIN", "ST", "12") @("BUS") $true $false "" @("MAIN ST")),
    (Fixture "photo_package_label" "FRAGILE THIS SIDE UP" 980 540 "photo_scene" "shipping label on a cluttered package-like background" @(
        (RectItem 0 0 980 540 "FromArgb:176,132,78")
        (RectItem 70 60 820 410 "FromArgb:192,145,84" "FromArgb:130,88,50")
        (LineItem 120 80 820 440 "FromArgb:150,105,60" 4)
        (LineItem 120 430 830 90 "FromArgb:150,105,60" 4)
        (RectItem 270 152 430 170 "White" "FromArgb:80,80,80")
        (TextItem "FRAGILE" 338 184 48 "Arial" "DarkRed" "Bold")
        (TextItem "THIS SIDE UP" 316 250 34 "Arial" "Black" "Bold")
        (TextItem "A7-09" 732 350 24 "Consolas" "FromArgb:65,65,65" "Regular")
    ) "White" @("FRAGILE", "THIS", "SIDE", "UP") @("A7")),
    (Fixture "photo_laptop_sticker" "LOCAL OCR" 900 520 "photo_scene" "laptop sticker text on a desk-like scene" @(
        (RectItem 0 0 900 520 "FromArgb:100,78,58")
        (RectItem 118 74 620 330 "FromArgb:42,44,48" "FromArgb:20,20,20")
        (RectItem 174 128 498 220 "FromArgb:235,235,235" "FromArgb:180,180,180")
        (RectItem 254 196 300 78 "FromArgb:20,130,160" "White")
        (TextItem "LOCAL OCR" 286 212 36 "Arial" "White" "Bold")
        (EllipseItem 724 298 96 96 "FromArgb:70,50,35" "FromArgb:35,25,20")
        (EllipseItem 742 314 60 60 "FromArgb:130,90,55")
    ) "White" @("LOCAL", "OCR")),
    (Fixture "photo_name_badge" "VISITOR ALEX" 760 560 "photo_scene" "badge text on a human-shirt-like background" @(
        (RectItem 0 0 760 560 "FromArgb:48,77,102")
        (EllipseItem 282 40 180 170 "FromArgb:210,170,138")
        (RectItem 192 250 378 198 "White" "FromArgb:190,190,190")
        (TextItem "VISITOR" 278 282 42 "Arial" "DarkBlue" "Bold")
        (TextItem "ALEX" 320 350 38 "Arial" "Black" "Bold")
        (LineItem 246 250 210 170 "FromArgb:230,230,230" 3)
        (LineItem 520 250 552 170 "FromArgb:230,230,230" 3)
    ) "White" @("VISITOR", "ALEX")),

    (Fixture "messy_ascii_symbols" "ERROR <= >= != && ||" 980 300 "symbols" "ASCII operators mixed with dense non-word marks" @(
        (TextItem "ERROR <= >= != && ||" 34 84 38 "Consolas" "Black" "Bold")
        (TextItem "@@@ ### $$$ %%% ^^^ ***" 34 164 28 "Consolas" "DimGray" "Regular")
        (LineItem 24 70 900 70 "LightGray" 1)
        (LineItem 24 150 900 150 "LightGray" 1)
    ) "White" @("ERROR", "<=", ">=", "!=", "&&") @("||", "@@@", "###")),
    (Fixture "dense_symbols_noise" "API_KEY=abc-123_xyz" 980 330 "symbols" "token-like text surrounded by visual symbol noise" @(
        (TextItem "::: ::: ::: ::: ::: :::" 20 30 22 "Consolas" "Silver" "Regular")
        (TextItem "API_KEY=abc-123_xyz" 46 118 38 "Consolas" "Black" "Bold")
        (TextItem "//// \\\\ |||| ==== ++++" 20 220 24 "Consolas" "Gray" "Regular")
    ) "White" @("API_KEY", "abc", "123_xyz")),
    (Fixture "emoji_adjacent_text" "Status OK" 720 230 "symbols" "emoji-like glyphs adjacent to normal text" @(
        (TextItem "Status OK" 36 74 42 "Segoe UI" "Black" "Bold")
        (TextItem $emojiSymbols 336 80 34 "Segoe UI Emoji" "DarkGreen" "Regular")
    ) "White" @("Status", "OK") @(([string][char]0x263A), ([string][char]0x26A0)) $true $false),
    (Fixture "emoji_primary" $emojiPrimary 520 220 "symbols" "emoji as part of primary expected text" @(
        (TextItem $emojiPrimary 42 72 46 "Segoe UI Emoji" "Black" "Regular")
    ) "White" @("OK") @() $false $false "Exploratory: emoji OCR is not a v1 ship gate."),
    (Fixture "mixed_latin_cyrillic" $mixedCyrillic 840 230 "multilingual" "Latin and Cyrillic in one line" @(
        (TextItem $mixedCyrillic 44 76 42 "Segoe UI" "Black" "Bold")
    ) "White" @("OCR", "OK") @() $false $false "Exploratory: records mixed-script behavior."),
    (Fixture "mixed_latin_japanese" $mixedJapanese 840 230 "multilingual" "Latin and Japanese katakana in one line" @(
        (TextItem $mixedJapanese 44 76 42 "Yu Gothic" "Black" "Bold")
    ) "White" @("OCR", "OK") @() $false $false "Exploratory: records Japanese behavior."),
    (Fixture "accented_latin" "cafe deja vu facade" 840 230 "multilingual" "accented Latin text rendered with accents" @(
        (TextItem $accentedLatin 44 76 40 "Segoe UI" "Black" "Bold")
    ) "White" @("caf", "vu", "fa") @() $true $false "" @()),
    (Fixture "mixed_arabic_latin" "Total 42" 760 230 "multilingual" "right-to-left script near Latin numbers" @(
        (TextItem $arabicHello 44 76 40 "Segoe UI" "Black" "Bold")
        (TextItem "Total 42" 286 76 40 "Segoe UI" "Black" "Bold")
    ) "White" @("Total", "42") @() $false $false "Exploratory: records RTL-adjacent behavior."),
<#
    (Fixture "emoji_adjacent_text" "Status OK" 720 230 "symbols" "emoji-like glyphs adjacent to normal text" @(
        (TextItem "Status OK" 36 74 42 "Segoe UI" "Black" "Bold")
        (TextItem "🙂 ✓ ⚠" 336 80 34 "Segoe UI Emoji" "DarkGreen" "Regular")
    ) "White" @("Status", "OK") @("🙂", "⚠") $true $false),
    (Fixture "emoji_primary" "🙂 OK" 520 220 "symbols" "emoji as part of primary expected text" @(
        (TextItem "🙂 OK" 42 72 46 "Segoe UI Emoji" "Black" "Regular")
    ) "White" @("OK") @() $false $false "Exploratory: emoji OCR is not a v1 ship gate."),
    (Fixture "mixed_latin_cyrillic" "OCR тест OK" 840 230 "multilingual" "Latin and Cyrillic in one line" @(
        (TextItem "OCR тест OK" 44 76 42 "Segoe UI" "Black" "Bold")
    ) "White" @("OCR", "OK") @() $false $false "Exploratory: records mixed-script behavior."),
    (Fixture "mixed_latin_japanese" "OCR テスト OK" 840 230 "multilingual" "Latin and Japanese katakana in one line" @(
        (TextItem "OCR テスト OK" 44 76 42 "Yu Gothic" "Black" "Bold")
    ) "White" @("OCR", "OK") @() $false $false "Exploratory: records Japanese behavior."),
    (Fixture "accented_latin" "cafe deja vu facade" 840 230 "multilingual" "accented Latin text rendered with accents" @(
        (TextItem "café déjà vu façade" 44 76 40 "Segoe UI" "Black" "Bold")
    ) "White" @("café", "déjà", "façade") @() $true $false "" @("café déjà")),
    (Fixture "mixed_arabic_latin" "Total 42" 760 230 "multilingual" "right-to-left script near Latin numbers" @(
        (TextItem "مرحبا" 44 76 40 "Segoe UI" "Black" "Bold")
        (TextItem "Total 42" 286 76 40 "Segoe UI" "Black" "Bold")
    ) "White" @("Total", "42") @() $false $false "Exploratory: records RTL-adjacent behavior."),

#>
    (Fixture "low_contrast_dense_dark" "Memory mode balanced" 900 300 "messy" "low contrast dark UI with secondary labels" @(
        (TextItem "OCR Snip" 26 24 24 "Segoe UI" "FromArgb:210,210,210" "Bold")
        (TextItem "Memory mode" 42 94 22 "Segoe UI" "FromArgb:180,180,180" "Regular")
        (TextItem "Balanced" 232 90 30 "Segoe UI" "White" "Bold")
        (TextItem "Unload OCR after idle" 42 154 20 "Segoe UI" "FromArgb:150,150,150" "Regular")
    ) "FromArgb:31,31,31" @("Memory", "Balanced")),
    (Fixture "incomplete_search_result" "external validation status" 920 260 "messy" "partial search-result card with clipped title and URL" @(
        (TextItem "external validation status" 32 18 34 "Segoe UI" "Black" "Bold")
        (TextItem "E:\_github\Text-Snip\artifacts\reports\validation-status.md" 32 78 20 "Consolas" "DimGray")
        (TextItem "Remaining gates: windows10x64, amdCpu, dpi125..." 32 124 20 "Segoe UI" "Black")
        (TextItem "neighbor result title" 32 226 20 "Segoe UI" "Gray")
    ) "White" @("external", "validation", "status") @("neighbor") $true $true),
    (Fixture "narrow_column_wrap" "Clipboard retry fallback window opened" 420 520 "messy" "narrow capture forcing wrapped-looking UI text" @(
        (TextItem "Clipboard" 24 20 30 "Segoe UI" "Black" "Bold")
        (TextItem "retry" 24 64 30 "Segoe UI" "Black" "Bold")
        (TextItem "fallback" 24 108 30 "Segoe UI" "Black" "Bold")
        (TextItem "window" 24 152 30 "Segoe UI" "Black" "Bold")
        (TextItem "opened" 24 196 30 "Segoe UI" "Black" "Bold")
        (TextItem "copy mode: raw lines" 24 318 18 "Segoe UI" "DimGray")
    ) "White" @("Clipboard", "retry", "fallback", "opened")),
    (Fixture "mixed_size_stack" "Start snip Settings Exit" 860 320 "messy" "large command mixed with small metadata" @(
        (TextItem "Start snip" 24 28 46 "Segoe UI" "Black" "Bold")
        (TextItem "Settings" 34 118 28 "Segoe UI" "Black")
        (TextItem "Win+Shift+O" 252 124 18 "Segoe UI" "Gray")
        (TextItem "Exit" 34 180 28 "Segoe UI" "Black")
        (TextItem "v1 local CPU OCR" 34 250 16 "Segoe UI" "Gray")
    ) "White" @("Start", "snip", "Settings", "Exit") @("v1", "CPU")),
    (Fixture "dense_json_clip" '{"mode":"auto","boost":true,"copy":"raw"}' 820 190 "clipped" "JSON row clipped close to both edges" @(
        (TextItem '{"mode":"auto","boost":true,"copy":"raw"}' -18 58 32 "Consolas" "Black" "Regular")
    ) "White" @("mode", "auto", "boost", "copy") @() $false $true)
)

Get-ChildItem $root -Include "*.png","*.expected.txt","*.meta.json" -File -Recurse | Remove-Item -Force

foreach ($fixture in $fixtures) {
    $png = Join-Path $root "$($fixture.Name).png"
    $expected = Join-Path $root "$($fixture.Name).expected.txt"
    $meta = Join-Path $root "$($fixture.Name).meta.json"
    $bitmap = New-Object System.Drawing.Bitmap($fixture.Width, $fixture.Height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear((Convert-Color $fixture.Back))
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

            foreach ($item in $fixture.Items) {
                if ($item.Type -eq "Rect" -or $item.Type -eq "Ellipse") {
                    $fill = New-Object System.Drawing.SolidBrush((Convert-Color $item.Fill))
                    try {
                        if ($item.Type -eq "Ellipse") {
                            $graphics.FillEllipse($fill, $item.X, $item.Y, $item.Width, $item.Height)
                        }
                        else {
                            $graphics.FillRectangle($fill, $item.X, $item.Y, $item.Width, $item.Height)
                        }
                    }
                    finally {
                        $fill.Dispose()
                    }

                    if (![string]::IsNullOrWhiteSpace($item.Stroke)) {
                        $pen = New-Object System.Drawing.Pen((Convert-Color $item.Stroke), 1)
                        try {
                            if ($item.Type -eq "Ellipse") {
                                $graphics.DrawEllipse($pen, $item.X, $item.Y, $item.Width, $item.Height)
                            }
                            else {
                                $graphics.DrawRectangle($pen, $item.X, $item.Y, $item.Width, $item.Height)
                            }
                        }
                        finally {
                            $pen.Dispose()
                        }
                    }
                }
                elseif ($item.Type -eq "Line") {
                    $pen = New-Object System.Drawing.Pen((Convert-Color $item.Fore), $item.Width)
                    try {
                        $graphics.DrawLine($pen, $item.X1, $item.Y1, $item.X2, $item.Y2)
                    }
                    finally {
                        $pen.Dispose()
                    }
                }
                else {
                    $fontStyle = [System.Drawing.FontStyle]::Regular
                    if ($item.Style -eq "Bold") {
                        $fontStyle = [System.Drawing.FontStyle]::Bold
                    }

                    $font = New-Object System.Drawing.Font($item.Font, $item.Size, $fontStyle, [System.Drawing.GraphicsUnit]::Pixel)
                    $brush = New-Object System.Drawing.SolidBrush((Convert-Color $item.Fore))
                    try {
                        if ([double]$item.Angle -ne 0) {
                            $state = $graphics.Save()
                            try {
                                $graphics.TranslateTransform($item.X, $item.Y)
                                $graphics.RotateTransform([single]$item.Angle)
                                $graphics.DrawString($item.Text, $font, $brush, 0, 0)
                            }
                            finally {
                                $graphics.Restore($state)
                            }
                        }
                        else {
                            $graphics.DrawString($item.Text, $font, $brush, $item.X, $item.Y)
                        }
                    }
                    finally {
                        $brush.Dispose()
                        $font.Dispose()
                    }
                }
            }
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
        Set-Content -Path $expected -Value $fixture.Expected
        [pscustomobject]@{
            name = $fixture.Name
            category = $fixture.Category
            risk = $fixture.Risk
            expected = $fixture.Expected
            primaryTokens = $fixture.PrimaryTokens
            noiseTokens = $fixture.NoiseTokens
            spacingPhrases = $fixture.SpacingPhrases
            requirePrimary = $fixture.RequirePrimary
            shouldWarnEdge = $fixture.ShouldWarnEdge
            notes = $fixture.Notes
        } | ConvertTo-Json -Depth 5 | Set-Content -Path $meta
    }
    finally {
        $bitmap.Dispose()
    }
}

Write-Host "Generated $($fixtures.Count) private fixtures in $root"
