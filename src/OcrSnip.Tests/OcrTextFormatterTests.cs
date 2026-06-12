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

    private static OcrLine Line(string text, float x, float y)
    {
        return new OcrLine(text, 1, new OcrQuadrilateral(
            new OcrPoint(x, y),
            new OcrPoint(x + 40, y),
            new OcrPoint(x + 40, y + 10),
            new OcrPoint(x, y + 10)));
    }
}
