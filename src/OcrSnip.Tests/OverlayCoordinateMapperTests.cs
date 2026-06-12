using System.Windows;
using OcrSnip.App.Overlay;

namespace OcrSnip.Tests;

public sealed class OverlayCoordinateMapperTests
{
    [Fact]
    public void Normalize_HandlesReverseDrag()
    {
        var rect = OverlayCoordinateMapper.Normalize(new Point(100, 80), new Point(40, 20));

        Assert.Equal(new Rect(40, 20, 60, 60), rect);
    }

    [Fact]
    public void FromPhysicalPoints_HandlesNegativeVirtualCoordinates()
    {
        var rect = OverlayCoordinateMapper.FromPhysicalPoints(new Point(-1840, 120), new Point(-1320, 420));

        Assert.Equal(-1840, rect.X);
        Assert.Equal(120, rect.Y);
        Assert.Equal(520, rect.Width);
        Assert.Equal(300, rect.Height);
    }

    [Fact]
    public void FromPhysicalPoints_NormalizesReverseDrag()
    {
        var rect = OverlayCoordinateMapper.FromPhysicalPoints(new Point(300.4, 240.4), new Point(100.2, 80.2));

        Assert.Equal(100, rect.X);
        Assert.Equal(80, rect.Y);
        Assert.Equal(200, rect.Width);
        Assert.Equal(160, rect.Height);
    }
}
