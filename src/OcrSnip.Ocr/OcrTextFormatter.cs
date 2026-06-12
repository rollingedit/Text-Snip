namespace OcrSnip.Ocr;

public static class OcrTextFormatter
{
    public static IReadOnlyList<OcrLine> SortLines(IReadOnlyList<OcrLine> lines)
    {
        if (lines.Count <= 1)
        {
            return lines;
        }

        var medianHeight = lines.Select(OcrGeometry.LineHeight).Order().ElementAt(lines.Count / 2);
        return lines
            .OrderBy(line => Math.Round(OcrGeometry.CenterY(line.Bounds) / Math.Max(1, medianHeight * 0.6f)))
            .ThenBy(line => line.Bounds.TopLeft.X)
            .ToArray();
    }

    public static string FormatLines(IReadOnlyList<OcrLine> lines, OcrCopyMode copyMode)
    {
        if (copyMode == OcrCopyMode.Code)
        {
            return string.Join(Environment.NewLine, lines.Select(line => line.Text).Where(text => text.Length > 0));
        }

        return string.Join(Environment.NewLine, lines.Select(line => line.Text.Trim()).Where(text => text.Length > 0));
    }
}
