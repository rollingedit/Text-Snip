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
    public void ToPhysicalRect_HandlesNegativeVirtualOriginAndScaling()
    {
        var rect = OverlayCoordinateMapper.ToPhysicalRect(new Rect(20, 10, 200, 100), -160, 40, 1.5, 1.5);

        Assert.Equal(-210, rect.X);
        Assert.Equal(75, rect.Y);
        Assert.Equal(300, rect.Width);
        Assert.Equal(150, rect.Height);
    }
}
