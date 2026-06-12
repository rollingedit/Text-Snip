using OcrSnip.Ocr;

namespace OcrSnip.Tests;

public sealed class OcrDiagnosticsAnalyzerTests
{
    [Fact]
    public void Analyze_DoesNotWarnForCenteredText()
    {
        var diagnostics = OcrDiagnosticsAnalyzer.Analyze(400, 200, [
            Line("Invoice total", 80, 70, 260, 112, 0.98f)
        ]);

        Assert.False(diagnostics.HasEdgeTouchingText);
        Assert.False(diagnostics.HasLikelyEdgeFragment);
        Assert.False(diagnostics.HasSelectionEdgeRisk);
    }

    [Fact]
    public void Analyze_WarnsWhenTextTouchesSelectionEdge()
    {
        var diagnostics = OcrDiagnosticsAnalyzer.Analyze(400, 200, [
            Line("Release verification passed", 1, 70, 300, 112, 0.98f)
        ]);

        Assert.True(diagnostics.HasEdgeTouchingText);
        Assert.False(diagnostics.HasLikelyEdgeFragment);
        Assert.True(diagnostics.HasSelectionEdgeRisk);
    }

    [Fact]
    public void Analyze_FlagsTinyLowConfidenceEdgeFragment()
    {
        var diagnostics = OcrDiagnosticsAnalyzer.Analyze(400, 200, [
            Line("Pr", 0, 80, 16, 108, 0.52f),
            Line("Settings saved", 80, 76, 260, 112, 0.98f)
        ]);

        Assert.True(diagnostics.HasEdgeTouchingText);
        Assert.True(diagnostics.HasLikelyEdgeFragment);
        Assert.True(diagnostics.HasSelectionEdgeRisk);
    }

    private static OcrLine Line(string text, float left, float top, float right, float bottom, float confidence)
    {
        return new OcrLine(text, confidence, new OcrQuadrilateral(
            new OcrPoint(left, top),
            new OcrPoint(right, top),
            new OcrPoint(right, bottom),
            new OcrPoint(left, bottom)));
    }
}
