using OcrSnip.Ocr;

namespace OcrSnip.Tests;

public sealed class OcrGeometryTests
{
    [Fact]
    public void OrderPoints_ReturnsCanonicalQuadrilateral()
    {
        var ordered = OcrGeometry.OrderPoints([
            new OcrPoint(20, 20),
            new OcrPoint(10, 10),
            new OcrPoint(10, 20),
            new OcrPoint(20, 10)
        ]);

        Assert.Equal(new OcrPoint(10, 10), ordered.TopLeft);
        Assert.Equal(new OcrPoint(20, 10), ordered.TopRight);
        Assert.Equal(new OcrPoint(20, 20), ordered.BottomRight);
        Assert.Equal(new OcrPoint(10, 20), ordered.BottomLeft);
    }

    [Fact]
    public void ExpandPolygon_IncreasesAxisAlignedBox()
    {
        var expanded = OcrGeometry.ExpandPolygon([
            new OcrPoint(10, 10),
            new OcrPoint(20, 10),
            new OcrPoint(20, 20),
            new OcrPoint(10, 20)
        ], 1.4);

        Assert.True(expanded.Min(p => p.X) < 10);
        Assert.True(expanded.Min(p => p.Y) < 10);
        Assert.True(expanded.Max(p => p.X) > 20);
        Assert.True(expanded.Max(p => p.Y) > 20);
    }
}
