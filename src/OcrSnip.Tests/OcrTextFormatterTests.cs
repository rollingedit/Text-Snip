using OcrSnip.Ocr;

namespace OcrSnip.Tests;

public sealed class OcrTextFormatterTests
{
    [Fact]
    public void SortLines_OrdersTopToBottomThenLeftToRight()
    {
        var bottom = Line("bottom", 10, 50);
        var topRight = Line("right", 60, 10);
        var topLeft = Line("left", 10, 10);

        var sorted = OcrTextFormatter.SortLines([bottom, topRight, topLeft]);

        Assert.Equal(["left", "right", "bottom"], sorted.Select(line => line.Text));
    }

    [Fact]
    public void FormatLines_TrimsRawButPreservesCodeWhitespace()
    {
        var lines = new[] { Line("  one  ", 0, 0), Line("\ttwo", 0, 20) };

        Assert.Equal($"one{Environment.NewLine}two", OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
        Assert.Equal($"  one  {Environment.NewLine}\ttwo", OcrTextFormatter.FormatLines(lines, OcrCopyMode.Code));
    }

    [Fact]
    public void FormatLines_DropsEmptyClipboardLines()
    {
        var lines = new[] { Line(" alpha ", 0, 0), Line("   ", 0, 20), Line("beta", 0, 40) };

        Assert.Equal($"alpha{Environment.NewLine}beta", OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesSameRowFragmentsWithInferredSpaces()
    {
        var lines = new[]
        {
            Line("Text-Snip-Setup-x64.exe", 520, 0, width: 180),
            Line("355cde4fbf61c130fd4b0ee669cbd11f5f472378e291f12456f674c121c07988", 10, 0, width: 480),
            Line("Signing", 10, 30, width: 60)
        };

        Assert.Equal(
            $"355cde4fbf61c130fd4b0ee669cbd11f5f472378e291f12456f674c121c07988    Text-Snip-Setup-x64.exe{Environment.NewLine}Signing",
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_DoesNotInsertSpacesForTouchingFragments()
    {
        var lines = new[]
        {
            Line("Text", 10, 0, width: 32),
            Line("Snip", 43, 0, width: 32)
        };

        Assert.Equal("TextSnip", OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_SeparatesDenseNavigationFragments()
    {
        var lines = new[]
        {
            Line("U.S.", 10, 0, width: 24),
            Line("World", 34, 0, width: 42),
            Line("Business", 76, 0, width: 62),
            Line("Arts", 138, 0, width: 32),
            Line("Lifestyle", 170, 0, width: 68),
            Line("Opinion", 238, 0, width: 56)
        };

        Assert.Equal("U.S. World Business Arts Lifestyle Opinion", OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_SeparatesShortNavigationFragments()
    {
        var lines = new[]
        {
            Line("U.S.", 10, 0, width: 24),
            Line("World", 34, 0, width: 42),
            Line("Business", 76, 0, width: 62)
        };

        Assert.Equal("U.S. World Business", OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_SeparatesPackedNavigationTextWithoutWordList()
    {
        var lines = new[]
        {
            Line("U.S.WorldBusinessArtsLifestyleOpinionVideoAudioGamesCookingWirecutterThe Athletic", 10, 0, width: 780)
        };

        Assert.Equal(
            "U.S. World Business Arts Lifestyle Opinion Video Audio Games Cooking Wirecutter The Athletic",
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesMixedHeightListRowAndIndentsSubtext()
    {
        var lines = new[]
        {
            Line("#", 18, 0, width: 8, height: 10),
            Line("Title", 42, 0, width: 38, height: 10),
            Line("1", 18, 42, width: 8, height: 12),
            Line("Sample One", 42, 34, width: 80, height: 16),
            Line("Example Artist", 42, 54, width: 96, height: 10),
            Line("2", 18, 84, width: 8, height: 12),
            Line("Sample Two", 42, 76, width: 80, height: 16),
            Line("Example Artist", 42, 96, width: 96, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "# Title",
                "1 Sample One",
                "Example Artist",
                "2 Sample Two",
                "Example Artist"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesCenteredListMarkersWithUpperText()
    {
        var lines = new[]
        {
            Line("#", 18, 0, width: 8, height: 10),
            Line("Title", 42, 0, width: 38, height: 10),
            Line("1", 18, 42, width: 8, height: 12),
            Line("Sample One", 42, 32, width: 80, height: 10),
            Line("Example Source", 42, 48, width: 96, height: 8),
            Line("2", 18, 78, width: 8, height: 12),
            Line("Sample Two", 42, 68, width: 80, height: 10),
            Line("Example Source", 42, 84, width: 96, height: 8),
            Line("June 5, 2026", 10, 130, width: 90, height: 9),
            Line("Example Records", 10, 144, width: 110, height: 9)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "# Title",
                "1 Sample One",
                "Example Source",
                "2 Sample Two",
                "Example Source",
                "",
                "June 5, 2026",
                "Example Records"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_UsesSingleSpaceForListMarkers()
    {
        var lines = new[]
        {
            Line("#", 18, 0, width: 8, height: 10),
            Line("Title", 42, 0, width: 38, height: 10),
            Line("10", 18, 30, width: 14, height: 12),
            Line("Sample Ten", 42, 30, width: 80, height: 12)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "# Title",
                "10 Sample Ten"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_PreservesSectionGapAfterListBlock()
    {
        var lines = new[]
        {
            Line("Highlights", 10, 0, width: 75, height: 12),
            Line("•", 26, 30, width: 8, height: 10),
            Line("First item", 48, 30, width: 80, height: 10),
            Line("•", 26, 50, width: 8, height: 10),
            Line("Second item", 48, 50, width: 90, height: 10),
            Line("Installer", 10, 88, width: 70, height: 12)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Highlights",
                "\u2022 First item",
                "\u2022 Second item",
                "",
                "Installer"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_RestoresMissingBulletsInsideDetectedBulletList()
    {
        var lines = new[]
        {
            Line("Highlights", 10, 0, width: 75, height: 12),
            Line("â€¢", 14, 42, width: 8, height: 10),
            Line("First OCR feature item.", 28, 42, width: 170, height: 10),
            Line("â€¢", 14, 64, width: 8, height: 10),
            Line("Second OCR feature item.", 28, 64, width: 180, height: 10),
            Line("Third OCR feature item.", 28, 86, width: 170, height: 10),
            Line("Fourth OCR feature item.", 28, 108, width: 176, height: 10),
            Line("Fifth OCR feature item.", 28, 130, width: 164, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Highlights",
                "\u2022 First OCR feature item.",
                "\u2022 Second OCR feature item.",
                "\u2022 Third OCR feature item.",
                "\u2022 Fourth OCR feature item.",
                "\u2022 Fifth OCR feature item."),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_RestoresLeadingMissingBulletsInsideDetectedBulletList()
    {
        var lines = new[]
        {
            Line("Highlights", 10, 0, width: 75, height: 12),
            Line("First OCR feature item.", 28, 42, width: 170, height: 10),
            Line("Second OCR feature item.", 28, 64, width: 180, height: 10),
            Line("Third OCR feature item.", 28, 86, width: 170, height: 10),
            Line("â€¢", 14, 108, width: 8, height: 10),
            Line("Fourth OCR feature item.", 28, 108, width: 176, height: 10),
            Line("â€¢", 14, 130, width: 8, height: 10),
            Line("Fifth OCR feature item.", 28, 130, width: 164, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Highlights",
                "\u2022 First OCR feature item.",
                "\u2022 Second OCR feature item.",
                "\u2022 Third OCR feature item.",
                "\u2022 Fourth OCR feature item.",
                "\u2022 Fifth OCR feature item."),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_RestoresBulletsForAlignedSentenceListUnderHeading()
    {
        var lines = new[]
        {
            Line("Highlights", 10, 0, width: 75, height: 12),
            Line("First OCR feature item.", 28, 42, width: 170, height: 10),
            Line("Second OCR feature item.", 28, 64, width: 180, height: 10),
            Line("Third OCR feature item.", 28, 86, width: 170, height: 10),
            Line("Fourth OCR feature item.", 28, 108, width: 176, height: 10),
            Line("Fifth OCR feature item.", 28, 130, width: 164, height: 10),
            Line("Installer", 10, 184, width: 70, height: 12)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Highlights",
                "\u2022 First OCR feature item.",
                "\u2022 Second OCR feature item.",
                "\u2022 Third OCR feature item.",
                "\u2022 Fourth OCR feature item.",
                "\u2022 Fifth OCR feature item.",
                "",
                "Installer"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_NormalizesMojibakeAndDoubledBulletMarkers()
    {
        var lines = new[]
        {
            Line("Highlights", 10, 0, width: 75, height: 12),
            Line("\u00e2\u20ac\u00a2", 14, 42, width: 8, height: 10),
            Line("\u2022 Mobile-style OCR selection.", 28, 42, width: 220, height: 10),
            Line("oR to run now,", 28, 72, width: 120, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Highlights",
                "\u2022 Mobile-style OCR selection.",
                "o R to run now,"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_DoesNotTreatCenteredSingleFlowAsColumns()
    {
        var lines = new[]
        {
            Line("This is my real handwriting! As a", 40, 40, width: 270, height: 18),
            Line("font! WHOA! This is amazing!", 64, 72, width: 245, height: 18),
            Line("Hello! Everyone should do this! It's", 64, 144, width: 310, height: 18),
            Line("pretty awesome. Make sure you follow", 49, 176, width: 330, height: 18),
            Line("instructions. Otherwise, you'll have a", 49, 208, width: 345, height: 18),
            Line("bad letter or number. Sheesh! Look at", 43, 240, width: 350, height: 18),
            Line("my number eight! 8", 143, 272, width: 165, height: 18),
            Line("Carly A. Example", 116, 344, width: 145, height: 18)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "This is my real handwriting! As a",
                "font! WHOA! This is amazing!",
                "",
                "Hello! Everyone should do this! It's",
                "pretty awesome. Make sure you follow",
                "instructions. Otherwise, you'll have a",
                "bad letter or number. Sheesh! Look at",
                "my number eight! 8",
                "",
                "Carly A. Example"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_RemovesSpacesBeforePunctuation()
    {
        var lines = new[]
        {
            Line("Press Win+Shift+O", 10, 0, width: 125),
            Line(",", 138, 0, width: 5),
            Line("release", 150, 0, width: 55)
        };

        Assert.Equal("Press Win+Shift+O, release", OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_SeparatesTwoColumnCardsIntoReadableBlocks()
    {
        var lines = new[]
        {
            Line("Featured items", 10, 0, width: 110, height: 12),
            Line("Customize view", 620, 0, width: 105, height: 12),
            Line("Item Group One", 18, 58, width: 115, height: 12),
            Line("Public", 370, 58, width: 40, height: 12),
            Line("Self-contained Windows tool for local files.", 18, 90, width: 310, height: 12),
            Line("Batchfile", 18, 166, width: 65, height: 10),
            Line("Item Group Two", 474, 58, width: 115, height: 12),
            Line("Public", 825, 58, width: 40, height: 12),
            Line("Local benchmark reports for your own media.", 474, 90, width: 330, height: 12),
            Line("Python", 474, 166, width: 50, height: 10),
            Line("Item Group Three", 18, 230, width: 130, height: 12),
            Line("Public", 370, 230, width: 40, height: 12),
            Line("Trim, crop, merge, captions, and export.", 18, 262, width: 310, height: 12),
            Line("Python", 18, 338, width: 50, height: 10),
            Line("Item Group Four", 474, 230, width: 120, height: 12),
            Line("Public", 825, 230, width: 40, height: 12),
            Line("Drag and drop files to extract and convert.", 474, 262, width: 320, height: 12),
            Line("C#", 474, 338, width: 20, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Featured items",
                "",
                "Customize view",
                "",
                "Item Group One    Public",
                "Self-contained Windows tool for local files.",
                "Batchfile",
                "",
                "Item Group Two    Public",
                "Local benchmark reports for your own media.",
                "Python",
                "",
                "Item Group Three    Public",
                "Trim, crop, merge, captions, and export.",
                "Python",
                "",
                "Item Group Four    Public",
                "Drag and drop files to extract and convert.",
                "C#"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_SeparatesNarrowThreeColumnMenu()
    {
        var lines = new[]
        {
            Line("HOME", 36, 0, width: 40, height: 10),
            Line("NEWS", 140, 0, width: 32, height: 10),
            Line("REVIEWS", 244, 0, width: 65, height: 10),
            Line("COMPARE", 36, 28, width: 70, height: 10),
            Line("TERMS", 140, 28, width: 40, height: 10),
            Line("ABOUT US", 244, 28, width: 75, height: 10),
            Line("BRAND A", 36, 70, width: 65, height: 10),
            Line("MAKE C", 140, 70, width: 45, height: 10),
            Line("BRAND E", 244, 70, width: 65, height: 10),
            Line("BRAND B", 36, 96, width: 65, height: 10),
            Line("MAKE D", 140, 96, width: 45, height: 10),
            Line("BRAND F", 244, 96, width: 65, height: 10),
            Line("EV FINDER", 134, 360, width: 80, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "HOME",
                "COMPARE",
                "BRAND A",
                "BRAND B",
                "",
                "NEWS",
                "TERMS",
                "MAKE C",
                "MAKE D",
                "",
                "REVIEWS",
                "ABOUT US",
                "BRAND E",
                "BRAND F",
                "",
                "EV FINDER"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_KeepsDenseTableRowsTogether()
    {
        var lines = new List<OcrLine>
        {
            Line("Repository Title", 14, 0, width: 120, height: 12),
            Line("Branch", 76, 36, width: 52, height: 10),
            Line("Tags", 168, 36, width: 36, height: 10)
        };
        var rowData = new[]
        {
            ("folder-a", "Add setup environment evidence check", "3 days ago"),
            ("folder-b", "Initial review release", "last week"),
            ("folder-c", "Initial review release", "last week"),
            ("folder-d", "Initial review release", "last week"),
            ("folder-e", "Prepare release build", "last week"),
            ("folder-f", "Initial review release", "last week"),
            ("folder-g", "Preflight local download path lengths", "3 days ago"),
            ("folder-h", "Refresh release evidence", "4 days ago"),
            ("script-a.bat", "Prepare production hardening update", "last week")
        };
        for (var i = 0; i < rowData.Length; i++)
        {
            var y = 82 + (i * 42);
            lines.Add(Line(rowData[i].Item1, 28, y, width: 92, height: 10));
            lines.Add(Line(rowData[i].Item2, 390, y, width: 310, height: 10));
            lines.Add(Line(rowData[i].Item3, 832, y, width: 74, height: 10));
        }

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Repository Title",
                "Branch    Tags",
                "",
                "folder-a    Add setup environment evidence check    3 days ago",
                "folder-b    Initial review release    last week",
                "folder-c    Initial review release    last week",
                "folder-d    Initial review release    last week",
                "folder-e    Prepare release build    last week",
                "folder-f    Initial review release    last week",
                "folder-g    Preflight local download path lengths    3 days ago",
                "folder-h    Refresh release evidence    4 days ago",
                "script-a.bat    Prepare production hardening update    last week"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_PreservesRestaurantMenuSectionHeadings()
    {
        var lines = new[]
        {
            Line("STARTERS", 36, 0, width: 70, height: 12),
            Line("Small Plate One - $7.99", 36, 36, width: 170, height: 10),
            Line("Small Plate Two - $8.99", 36, 58, width: 175, height: 10),
            Line("SOUP & SALAD", 36, 92, width: 105, height: 12),
            Line("Salad Bowl One - $10.99", 36, 126, width: 175, height: 10),
            Line("Salad Bowl Two - $11.99", 36, 148, width: 175, height: 10),
            Line("MAIN COURSE", 278, 0, width: 105, height: 12),
            Line("Main Plate One - $18.99", 278, 36, width: 175, height: 10),
            Line("Main Plate Two - $24.99", 278, 58, width: 175, height: 10),
            Line("BEVERAGES", 278, 92, width: 90, height: 12),
            Line("Drink One - $2.99", 278, 126, width: 125, height: 10),
            Line("Drink Two - $2.49", 278, 148, width: 125, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "STARTERS",
                "",
                "Small Plate One - $7.99",
                "Small Plate Two - $8.99",
                "",
                "SOUP & SALAD",
                "",
                "Salad Bowl One - $10.99",
                "Salad Bowl Two - $11.99",
                "",
                "MAIN COURSE",
                "",
                "Main Plate One - $18.99",
                "Main Plate Two - $24.99",
                "",
                "BEVERAGES",
                "",
                "Drink One - $2.99",
                "Drink Two - $2.49"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesDetachedMenuPriceColumnWithinSection()
    {
        var lines = new[]
        {
            Line("SECTION ONE", 48, 0, width: 100, height: 12),
            Line("Dish One", 48, 42, width: 70, height: 10),
            Line("Dish Two", 48, 68, width: 70, height: 10),
            Line("Dish Three", 48, 94, width: 88, height: 10),
            Line("$21", 204, 188, width: 26, height: 10),
            Line("$22", 204, 214, width: 26, height: 10),
            Line("$23", 204, 240, width: 26, height: 10),
            Line("SECTION TWO", 306, 0, width: 100, height: 12),
            Line("Item One", 306, 42, width: 70, height: 10),
            Line("$11", 466, 42, width: 26, height: 10),
            Line("Item Two", 306, 68, width: 70, height: 10),
            Line("$12", 466, 68, width: 26, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "SECTION ONE",
                "Dish One    $21",
                "Dish Two    $22",
                "Dish Three    $23",
                "",
                "SECTION TWO",
                "Item One    $11",
                "Item Two    $12"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesAdjacentMenuItemAndPriceColumns()
    {
        var lines = new[]
        {
            Line("Menu and Prices:", 20, 0, width: 130, height: 12),
            Line("Dish Option One", 20, 44, width: 130, height: 10),
            Line("Dish Option Two", 20, 70, width: 130, height: 10),
            Line("Dish Option Three", 20, 96, width: 145, height: 10),
            Line("Dish Option Four", 20, 122, width: 138, height: 10),
            Line("$12.99", 220, 44, width: 48, height: 10),
            Line("$10.99", 220, 70, width: 48, height: 10),
            Line("$14.99", 220, 96, width: 48, height: 10),
            Line("$11.99", 220, 122, width: 48, height: 10),
            Line("Drinks:", 310, 0, width: 55, height: 12),
            Line("Drink Option One", 310, 44, width: 125, height: 10),
            Line("Drink Option Two", 310, 70, width: 125, height: 10),
            Line("$2.99", 490, 44, width: 42, height: 10),
            Line("$3.99", 490, 70, width: 42, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Menu and Prices:",
                "",
                "Dish Option One    $12.99",
                "Dish Option Two    $10.99",
                "Dish Option Three    $14.99",
                "Dish Option Four    $11.99",
                "",
                "Drinks:",
                "",
                "Drink Option One    $2.99",
                "Drink Option Two    $3.99"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_DoesNotCollapseTightlyStackedMenuRows()
    {
        var lines = new[]
        {
            Line("MAIN COURSE", 28, 0, width: 118, height: 18),
            Line("Dish One", 28, 38, width: 70, height: 18),
            Line("$10", 210, 38, width: 28, height: 18),
            Line("Dish Two", 28, 48, width: 70, height: 18),
            Line("$11", 210, 48, width: 28, height: 18),
            Line("Dish Three", 28, 58, width: 86, height: 18),
            Line("$12", 210, 58, width: 28, height: 18),
            Line("DRINKS", 28, 120, width: 62, height: 18),
            Line("Drink One", 28, 158, width: 80, height: 18),
            Line("$3", 210, 158, width: 22, height: 18),
            Line("Drink Two", 28, 168, width: 80, height: 18),
            Line("$4", 210, 168, width: 22, height: 18)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "MAIN COURSE",
                "Dish One    $10",
                "Dish Two    $11",
                "Dish Three    $12",
                "",
                "DRINKS",
                "Drink One    $3",
                "Drink Two    $4"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesMenuItemsWithTrailingDetachedPriceBlock()
    {
        var lines = new[]
        {
            Line("FOOD MENU", 28, 0, width: 90, height: 18),
            Line("MAIN COURSE", 28, 34, width: 118, height: 18),
            Line("Dish One", 28, 74, width: 70, height: 18),
            Line("Dish Two", 28, 94, width: 70, height: 18),
            Line("Dish Three", 28, 114, width: 86, height: 18),
            Line("APPETIZERS", 28, 166, width: 110, height: 18),
            Line("Small Item One", 28, 206, width: 118, height: 18),
            Line("Small Item Two", 28, 226, width: 118, height: 18),
            Line("DRINKS", 28, 278, width: 62, height: 18),
            Line("Drink One", 28, 318, width: 80, height: 18),
            Line("Drink Two", 28, 338, width: 80, height: 18),
            Line("$10", 210, 430, width: 28, height: 18),
            Line("$11", 210, 450, width: 28, height: 18),
            Line("$12", 210, 470, width: 28, height: 18),
            Line("$13", 210, 510, width: 28, height: 18),
            Line("$14", 210, 530, width: 28, height: 18),
            Line("$3", 210, 570, width: 22, height: 18),
            Line("$4", 210, 590, width: 22, height: 18)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "FOOD MENU",
                "MAIN COURSE",
                "Dish One    $10",
                "Dish Two    $11",
                "Dish Three    $12",
                "",
                "APPETIZERS",
                "Small Item One    $13",
                "Small Item Two    $14",
                "",
                "DRINKS",
                "Drink One    $3",
                "Drink Two    $4"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesSecondMenuSectionWithTrailingDetachedPriceBlock()
    {
        var lines = new[]
        {
            Line("Menu and Prices:", 16, 0, width: 120, height: 12),
            Line("Dish One", 16, 42, width: 70, height: 10),
            Line("$12.99", 220, 42, width: 48, height: 10),
            Line("Dish Two", 16, 66, width: 70, height: 10),
            Line("$10.99", 220, 66, width: 48, height: 10),
            Line("Beverages:", 16, 150, width: 80, height: 12),
            Line("Drink One", 16, 190, width: 80, height: 10),
            Line("Drink Two", 16, 214, width: 80, height: 10),
            Line("Drink Three", 16, 238, width: 92, height: 10),
            Line("$2.99", 16, 310, width: 42, height: 10),
            Line("$3.99", 16, 334, width: 42, height: 10),
            Line("$4.99", 16, 358, width: 42, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Menu and Prices:",
                "",
                "Dish One    $12.99",
                "Dish Two    $10.99",
                "",
                "Beverages:",
                "",
                "Drink One    $2.99",
                "Drink Two    $3.99",
                "Drink Three    $4.99"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesDetachedMenuPriceBlockBeforeFooter()
    {
        var lines = new[]
        {
            Line("Beverages:", 8, 0, width: 80, height: 12),
            Line("Soft Drinks", 8, 28, width: 82, height: 10),
            Line("Iced Tea", 8, 50, width: 58, height: 10),
            Line("Freshly Squeezed Lemonade", 8, 72, width: 205, height: 10),
            Line("Fruit Smoothies", 8, 94, width: 118, height: 10),
            Line("Coffee", 8, 116, width: 50, height: 10),
            Line("Hot Tea", 8, 138, width: 55, height: 10),
            Line("Bottled Water", 8, 160, width: 95, height: 10),
            Line("$2.99", 8, 222, width: 42, height: 10),
            Line("$2.99", 8, 244, width: 42, height: 10),
            Line("$3.99", 8, 266, width: 42, height: 10),
            Line("$4.99", 8, 288, width: 42, height: 10),
            Line("$2.99", 8, 310, width: 42, height: 10),
            Line("$2.99", 8, 332, width: 42, height: 10),
            Line("$1.99", 8, 354, width: 42, height: 10),
            Line("816x1,056", 8, 410, width: 75, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Beverages:",
                "Soft Drinks    $2.99",
                "Iced Tea    $2.99",
                "Freshly Squeezed Lemonade    $3.99",
                "Fruit Smoothies    $4.99",
                "Coffee    $2.99",
                "Hot Tea    $2.99",
                "Bottled Water    $1.99",
                "",
                "816x1,056"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesMultipleMenuSectionsWithTrailingPriceBlocksAfterFooterNoise()
    {
        var lines = new[]
        {
            Line("FOOD MENU", 18, 0, width: 90, height: 12),
            Line("MAIN COURSE", 18, 28, width: 110, height: 12),
            Line("Item One", 18, 58, width: 70, height: 10),
            Line("Item Two", 18, 80, width: 70, height: 10),
            Line("Item Three", 18, 102, width: 86, height: 10),
            Line("Item Four", 18, 124, width: 76, height: 10),
            Line("Item Five", 18, 146, width: 76, height: 10),
            Line("APPETIZERS", 18, 204, width: 98, height: 12),
            Line("Small One", 18, 234, width: 80, height: 10),
            Line("Small Two", 18, 256, width: 80, height: 10),
            Line("Small Three", 18, 278, width: 95, height: 10),
            Line("Small Four", 18, 300, width: 86, height: 10),
            Line("Small Five", 18, 322, width: 86, height: 10),
            Line("DRINKS", 18, 380, width: 62, height: 12),
            Line("Drink One", 18, 410, width: 80, height: 10),
            Line("Drink Two", 18, 432, width: 80, height: 10),
            Line("Drink Three", 18, 454, width: 95, height: 10),
            Line("Drink Four", 18, 476, width: 86, height: 10),
            Line("Drink Five", 18, 498, width: 86, height: 10),
            Line("FREE DELIVERY", 18, 560, width: 120, height: 12),
            Line("+123 456 7890", 48, 588, width: 118, height: 10),
            Line("www.example.com", 18, 612, width: 125, height: 10),
            Line("$10", 18, 672, width: 28, height: 10),
            Line("$11", 18, 694, width: 28, height: 10),
            Line("$12", 18, 716, width: 28, height: 10),
            Line("$13", 18, 738, width: 28, height: 10),
            Line("$14", 18, 760, width: 28, height: 10),
            Line("$10", 18, 820, width: 28, height: 10),
            Line("$11", 18, 842, width: 28, height: 10),
            Line("$12", 18, 864, width: 28, height: 10),
            Line("$13", 18, 886, width: 28, height: 10),
            Line("$14", 18, 908, width: 28, height: 10),
            Line("$10", 18, 968, width: 28, height: 10),
            Line("$11", 18, 990, width: 28, height: 10),
            Line("$12", 18, 1012, width: 28, height: 10),
            Line("$13", 18, 1034, width: 28, height: 10),
            Line("$14", 18, 1056, width: 28, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "FOOD MENU",
                "MAIN COURSE",
                "Item One    $10",
                "Item Two    $11",
                "Item Three    $12",
                "Item Four    $13",
                "Item Five    $14",
                "",
                "APPETIZERS",
                "Small One    $10",
                "Small Two    $11",
                "Small Three    $12",
                "Small Four    $13",
                "Small Five    $14",
                "",
                "DRINKS",
                "Drink One    $10",
                "Drink Two    $11",
                "Drink Three    $12",
                "Drink Four    $13",
                "Drink Five    $14",
                "",
                "FREE DELIVERY",
                "+123 456 7890",
                "www.example.com"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesFirstMenuSectionDetachedPricesAndSecondSectionInlinePrices()
    {
        var lines = new[]
        {
            Line("Menu and Prices:", 16, 0, width: 120, height: 12),
            Line("Dish One", 16, 42, width: 70, height: 10),
            Line("Dish Two", 16, 66, width: 70, height: 10),
            Line("Dish Three", 16, 90, width: 86, height: 10),
            Line("$12.99", 16, 150, width: 48, height: 10),
            Line("$10.99", 16, 174, width: 48, height: 10),
            Line("$14.99", 16, 198, width: 48, height: 10),
            Line("Beverages:", 16, 270, width: 80, height: 12),
            Line("$2.99", 130, 270, width: 42, height: 10),
            Line("Drink One", 16, 310, width: 80, height: 10),
            Line("$3.99", 142, 310, width: 42, height: 10),
            Line("Drink Two", 16, 334, width: 80, height: 10),
            Line("$4.99", 168, 334, width: 42, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Menu and Prices:",
                "",
                "Dish One    $12.99",
                "Dish Two    $10.99",
                "Dish Three    $14.99",
                "",
                "Beverages:    $2.99",
                "",
                "Drink One    $3.99",
                "Drink Two    $4.99"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesAdjacentMenuColumnsWithTrailingCurrencySymbol()
    {
        var lines = new[]
        {
            Line("BURGERS", 20, 0, width: 72, height: 12),
            Line("Small", 20, 34, width: 45, height: 10),
            Line("9.99$", 148, 34, width: 42, height: 10),
            Line("Large", 20, 56, width: 45, height: 10),
            Line("15.99$", 148, 56, width: 50, height: 10),
            Line("DRINKS", 20, 100, width: 60, height: 12),
            Line("Water", 20, 134, width: 45, height: 10),
            Line(".99$", 148, 134, width: 34, height: 10),
            Line("Tea", 20, 156, width: 30, height: 10),
            Line("1.99$", 148, 156, width: 42, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "BURGERS",
                "",
                "Small    9.99$",
                "Large    15.99$",
                "",
                "DRINKS",
                "",
                "Water   .99$",
                "Tea    1.99$"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_KeepsPosterMenuBlocksLocal()
    {
        var lines = new[]
        {
            Line("FAST FOOD MENU", 152, 0, width: 145, height: 18),
            Line("TACO", 210, 68, width: 58, height: 16),
            Line("item line one", 210, 100, width: 92, height: 10),
            Line("$9", 330, 100, width: 22, height: 10),
            Line("item line two", 210, 118, width: 92, height: 10),
            Line("$9", 330, 118, width: 22, height: 10),
            Line("item line three", 210, 136, width: 105, height: 10),
            Line("$9", 330, 136, width: 22, height: 10),
            Line("BURGER", 30, 224, width: 78, height: 16),
            Line("REGULAR", 30, 254, width: 70, height: 14),
            Line("9.99$", 142, 254, width: 48, height: 14),
            Line("DOUBLE", 30, 278, width: 62, height: 14),
            Line("15.99$", 142, 278, width: 58, height: 14),
            Line("PIZZA", 210, 332, width: 66, height: 16),
            Line("FULL", 210, 362, width: 44, height: 14),
            Line("29.99$", 305, 362, width: 58, height: 14),
            Line("SLICE", 210, 386, width: 52, height: 14),
            Line("9.99$", 305, 386, width: 48, height: 14),
            Line("DRINKS", 30, 472, width: 70, height: 16),
            Line("drink line one", 126, 504, width: 98, height: 10),
            Line("$9", 245, 504, width: 22, height: 10),
            Line("drink line two", 126, 522, width: 98, height: 10),
            Line("$9", 245, 522, width: 22, height: 10),
            Line("FRIES", 278, 472, width: 58, height: 16),
            Line("SMALL", 278, 504, width: 50, height: 14),
            Line("9.99$", 360, 504, width: 48, height: 14),
            Line("BIG", 278, 528, width: 32, height: 14),
            Line("19.99$", 360, 528, width: 58, height: 14),
            Line("GENERIC FOOTER TEXT", 126, 620, width: 190, height: 12)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "FAST FOOD MENU",
                "",
                "TACO",
                "item line one   $9",
                "item line two   $9",
                "item line three   $9",
                "",
                "BURGER",
                "REGULAR    9.99$",
                "DOUBLE    15.99$",
                "",
                "PIZZA",
                "FULL    29.99$",
                "SLICE    9.99$",
                "",
                "DRINKS",
                "drink line one   $9",
                "drink line two   $9",
                "",
                "FRIES",
                "SMALL   9.99$",
                "BIG    19.99$",
                "",
                "GENERIC FOOTER TEXT"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_MergesAllCapsMenuItemsWithOffsetPriceColumn()
    {
        var lines = new[]
        {
            Line("SECTION", 30, 0, width: 72, height: 16),
            Line("SMALL", 30, 32, width: 55, height: 14),
            Line("9.99$", 140, 36, width: 48, height: 10),
            Line("LARGE", 30, 56, width: 55, height: 14),
            Line("15.99$", 140, 60, width: 58, height: 10),
            Line("OTHER", 220, 0, width: 58, height: 16),
            Line("ITEM ONE", 220, 32, width: 78, height: 14),
            Line("$9", 330, 36, width: 22, height: 10),
            Line("ITEM TWO", 220, 56, width: 78, height: 14),
            Line("$9", 330, 60, width: 22, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "SECTION",
                "SMALL    9.99$",
                "LARGE    15.99$",
                "",
                "OTHER",
                "ITEM ONE   $9",
                "ITEM TWO   $9"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_PreservesAlternatingChatBubbleOrder()
    {
        var lines = new[]
        {
            Line("Where are you? This place is", 38, 20, width: 250, height: 24),
            Line("delicious.", 38, 50, width: 88, height: 24),
            Line("Be careful. You don't", 165, 142, width: 220, height: 24),
            Line("know who lives there.", 165, 172, width: 218, height: 24),
            Line("Don't be such a wimp.", 38, 244, width: 210, height: 24),
            Line("Those pine trees can't", 38, 274, width: 215, height: 24),
            Line("taste like much and I'm", 38, 304, width: 228, height: 24),
            Line("hungry.", 38, 334, width: 78, height: 24),
            Line("Listen! What's that?", 220, 404, width: 190, height: 24),
            Line("Uh-oh.", 38, 470, width: 65, height: 24)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Where are you? This place is",
                "delicious.",
                "",
                "Be careful. You don't",
                "know who lives there.",
                "",
                "Don't be such a wimp.",
                "Those pine trees can't",
                "taste like much and I'm",
                "hungry.",
                "",
                "Listen! What's that?",
                "",
                "Uh-oh."),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_PreservesChatOrderWithHeaderAndFooterChrome()
    {
        var lines = new[]
        {
            Line("Ryan", 180, 10, width: 34, height: 10),
            Line("Hey man, we on for hoops this", 126, 56, width: 225, height: 18),
            Line("afternoon?", 126, 76, width: 88, height: 18),
            Line("You know it!", 36, 116, width: 105, height: 18),
            Line("Good, cuz we're taking you boys", 112, 164, width: 240, height: 18),
            Line("down.", 112, 184, width: 48, height: 18),
            Line("Not a chance.", 36, 224, width: 112, height: 18),
            Line("Hi lamb chop, it's only been an", 112, 274, width: 240, height: 18),
            Line("hour and I miss you already.", 112, 294, width: 226, height: 18),
            Line("Luv you.", 112, 314, width: 70, height: 18),
            Line("Delivered", 300, 340, width: 64, height: 9),
            Line("iMessage", 142, 382, width: 76, height: 12),
            Line("Cash", 226, 418, width: 34, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Ryan",
                "",
                "Hey man, we on for hoops this",
                "afternoon?",
                "You know it!",
                "",
                "Good, cuz we're taking you boys",
                "down.",
                "Not a chance.",
                "",
                "Hi lamb chop, it's only been an",
                "hour and I miss you already.",
                "Luv you.",
                "Delivered",
                "",
                "iMessage",
                "Cash"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_PreservesChatOrderWhenBubbleColumnsAreClose()
    {
        var lines = new[]
        {
            Line("Hey man, we on for hoops this", 92, 20, width: 225, height: 18),
            Line("afternoon?", 92, 40, width: 88, height: 18),
            Line("You know it!", 22, 76, width: 105, height: 18),
            Line("Good, cuz we're taking you boys", 82, 112, width: 240, height: 18),
            Line("down.", 82, 132, width: 48, height: 18),
            Line("Not a chance.", 22, 168, width: 112, height: 18),
            Line("Hi lamb chop, it's only been an", 92, 214, width: 240, height: 18),
            Line("hour and I miss you already.", 92, 234, width: 226, height: 18),
            Line("Luv you.", 92, 254, width: 70, height: 18),
            Line("Delivered", 270, 280, width: 64, height: 9)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Hey man, we on for hoops this",
                "afternoon?",
                "You know it!",
                "Good, cuz we're taking you boys",
                "down.",
                "Not a chance.",
                "",
                "Hi lamb chop, it's only been an",
                "hour and I miss you already.",
                "Luv you.",
                "Delivered"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_PreservesSingleMessageChatAndDropsIconNoise()
    {
        var lines = new[]
        {
            Line("9:41", 10, 0, width: 34, height: 10),
            Line("<", 10, 28, width: 8, height: 10),
            Line("Jane>", 205, 132, width: 45, height: 10),
            Line("iMessage", 190, 168, width: 70, height: 10),
            Line("Today 9:41 AM", 172, 186, width: 105, height: 10),
            Line("Can I call you back later? I'm at an", 112, 224, width: 300, height: 18),
            Line("appointment.", 112, 246, width: 105, height: 18),
            Line("ooll D", 120, 314, width: 52, height: 10),
            Line("Delivered", 350, 300, width: 70, height: 10)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "9:41",
                "<",
                "",
                "Jane>",
                "",
                "iMessage",
                "Today 9:41 AM",
                "",
                "Can I call you back later? I'm at an",
                "appointment.",
                "",
                "Delivered"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_UsesSpatialFormattingForSymbolHeavyCaptures()
    {
        var lines = new[]
        {
            Line("))", 410, 0, width: 12, height: 10),
            Line("(+)(+)", 498, 0, width: 42, height: 10),
            Line("00 #", 410, 24, width: 32, height: 10),
            Line("W", 410, 74, width: 10, height: 10),
            Line("Clothing &", 410, 98, width: 80, height: 10),
            Line("Animals", 10, 122, width: 60, height: 10),
            Line("Art and design", 100, 122, width: 110, height: 10)
        };

        var result = OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw);
        var resultLines = result.Split(Environment.NewLine);
        Assert.Contains("))", resultLines[0]);
        Assert.Contains("(+)(+)", resultLines[0]);
        Assert.Contains("00 #", result);
        Assert.Contains("W", result);
        Assert.Contains("Clothing &", result);
        Assert.Contains("Animals", resultLines[^1]);
        Assert.Contains("Art and design", resultLines[^1]);
    }

    [Fact]
    public void FormatLines_PreservesLargeVerticalGapsBetweenSections()
    {
        var lines = new[]
        {
            Line("Text Snip 1.1", 10, 0, width: 100, height: 14),
            Line("Native OCR snipping for Windows.", 10, 42, width: 210, height: 12),
            Line("Highlights", 10, 90, width: 80, height: 14),
            Line("Local CPU OCR", 30, 132, width: 110, height: 12)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Text Snip 1.1",
                "",
                "Native OCR snipping for Windows.",
                "",
                "Highlights",
                "",
                "Local CPU OCR"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_KeepsWrappedNumberedStepsTogetherButPreservesSectionBreaks()
    {
        var lines = new[]
        {
            Line("Contents", 18, 0, width: 70, height: 13),
            Line("Quick Start", 18, 48, width: 92, height: 15),
            Line("1. Go to the latest release.", 18, 104, width: 210, height: 13),
            Line("2. Download setup.bat.", 18, 125, width: 180, height: 13),
            Line("3. Double-click it.", 18, 146, width: 150, height: 13),
            Line("4. After setup, choose:", 18, 190, width: 190, height: 13),
            Line("o R to run now,", 34, 212, width: 130, height: 13),
            Line("o P to paste a model link,", 34, 233, width: 205, height: 13),
            Line("o M to open the model folder,", 34, 254, width: 240, height: 13),
            Line("o I to open the input folder.", 34, 275, width: 230, height: 13),
            Line("5. Add a model by pasting a model link, using a first-run baseline, or copying a supported model", 18, 320, width: 780, height: 13),
            Line("package into Models.", 18, 358, width: 160, height: 13),
            Line("6. Add media by dropping files into Input, pasting paths when prompted, or dragging files/folders onto", 18, 402, width: 780, height: 13),
            Line("Drop_Files_Here.bat.", 18, 424, width: 175, height: 13),
            Line("7. Choose one model for transcription or multiple models for comparison.", 18, 446, width: 520, height: 13),
            Line("8. Open Open_Latest_Report.bat or the newest final_results.html under Output.", 18, 500, width: 590, height: 13),
            Line("The first-run baseline is a small CPU-safe sanity check. It proves the app can run a model; it is not meant to be a full", 18, 552, width: 880, height: 13),
            Line("ranking pack.", 18, 574, width: 105, height: 13)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Contents",
                "",
                "Quick Start",
                "",
                "1. Go to the latest release.",
                "2. Download setup.bat.",
                "3. Double-click it.",
                "4. After setup, choose:",
                "o R to run now,",
                "o P to paste a model link,",
                "o M to open the model folder,",
                "o I to open the input folder.",
                "",
                "5. Add a model by pasting a model link, using a first-run baseline, or copying a supported model",
                "package into Models.",
                "",
                "6. Add media by dropping files into Input, pasting paths when prompted, or dragging files/folders onto",
                "Drop_Files_Here.bat.",
                "7. Choose one model for transcription or multiple models for comparison.",
                "8. Open Open_Latest_Report.bat or the newest final_results.html under Output.",
                "",
                "The first-run baseline is a small CPU-safe sanity check. It proves the app can run a model; it is not meant to be a full",
                "ranking pack."),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_PreservesParagraphAndCodeBlockSpacing()
    {
        var lines = new[]
        {
            Line("Project Bench", 14, 0, width: 130, height: 18),
            Line("Transcribe media with local models on Windows, then compare models on your own files and", 14, 62, width: 720, height: 14),
            Line("your own hardware.", 14, 84, width: 150, height: 14),
            Line("It is the benchmark for your audio, on your machine.", 14, 132, width: 430, height: 14),
            Line("Public leaderboards are useful, but they are not your files: fast speech, accents, crosstalk,", 14, 180, width: 760, height: 14),
            Line("noisy clips, language switches, and whatever else you actually need transcribed.", 14, 202, width: 650, height: 14),
            Line("That is the product promise:", 14, 250, width: 250, height: 14),
            Line("No project setup.", 30, 302, width: 140, height: 13),
            Line("No hunting for sidecar files.", 30, 322, width: 230, height: 13),
            Line("No guessing which optional runtime package to install.", 30, 342, width: 430, height: 13),
            Line("Paste a link. Drop files. Get transcripts.", 14, 408, width: 350, height: 14),
            Line("The normal user flow is intentionally small:", 14, 456, width: 360, height: 14),
            Line("Download setup.bat", 30, 508, width: 160, height: 13),
            Line("Double-click", 30, 528, width: 100, height: 13),
            Line("Open final_results.html", 30, 548, width: 200, height: 13),
            Line("Contents", 14, 612, width: 75, height: 14)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Project Bench",
                "",
                "Transcribe media with local models on Windows, then compare models on your own files and",
                "your own hardware.",
                "",
                "It is the benchmark for your audio, on your machine.",
                "",
                "Public leaderboards are useful, but they are not your files: fast speech, accents, crosstalk,",
                "noisy clips, language switches, and whatever else you actually need transcribed.",
                "",
                "That is the product promise:",
                "",
                "No project setup.",
                "No hunting for sidecar files.",
                "No guessing which optional runtime package to install.",
                "",
                "Paste a link. Drop files. Get transcripts.",
                "",
                "The normal user flow is intentionally small:",
                "",
                "Download setup.bat",
                "Double-click",
                "Open final_results.html",
                "",
                "Contents"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    [Fact]
    public void FormatLines_DropsExplicitEmptyRows()
    {
        var lines = new[]
        {
            Line("Heading", 10, 0, width: 70, height: 12),
            Line("   ", 10, 18, width: 70, height: 12),
            Line("Body", 10, 36, width: 45, height: 12)
        };

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Heading",
                "Body"),
            OcrTextFormatter.FormatLines(lines, OcrCopyMode.Raw));
    }

    private static OcrLine Line(string text, float x, float y, float width = 40, float height = 10)
    {
        return new OcrLine(text, 1, new OcrQuadrilateral(
            new OcrPoint(x, y),
            new OcrPoint(x + width, y),
            new OcrPoint(x + width, y + height),
            new OcrPoint(x, y + height)));
    }
}
