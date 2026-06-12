namespace OcrSnip.Ocr;

public static class OcrDiagnosticsAnalyzer
{
    public static OcrDiagnostics Analyze(int imageWidth, int imageHeight, IReadOnlyList<OcrLine> lines)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);

        var edgeMargin = Math.Max(3, Math.Min(imageWidth, imageHeight) * 0.02f);
        var hasEdgeTouchingText = false;
        var hasLikelyEdgeFragment = false;

        foreach (var line in lines)
        {
            var bounds = GetBounds(line.Bounds);
            var touchesEdge = bounds.Left <= edgeMargin
                || bounds.Top <= edgeMargin
                || imageWidth - bounds.Right <= edgeMargin
                || imageHeight - bounds.Bottom <= edgeMargin;

            if (!touchesEdge)
            {
                continue;
            }

            hasEdgeTouchingText = true;
            var nonWhitespace = line.Text.Count(character => !char.IsWhiteSpace(character));
            var narrow = bounds.Width <= Math.Max(18, imageWidth * 0.08f);
            var shortText = nonWhitespace <= 3;
            if ((shortText && narrow) || line.Confidence < 0.65f)
            {
                hasLikelyEdgeFragment = true;
            }
        }

        return new OcrDiagnostics(hasEdgeTouchingText, hasLikelyEdgeFragment);
    }

    private static (float Left, float Top, float Right, float Bottom, float Width, float Height) GetBounds(OcrQuadrilateral box)
    {
        var xs = new[] { box.TopLeft.X, box.TopRight.X, box.BottomRight.X, box.BottomLeft.X };
        var ys = new[] { box.TopLeft.Y, box.TopRight.Y, box.BottomRight.Y, box.BottomLeft.Y };
        var left = xs.Min();
        var top = ys.Min();
        var right = xs.Max();
        var bottom = ys.Max();
        return (left, top, right, bottom, right - left, bottom - top);
    }
}
