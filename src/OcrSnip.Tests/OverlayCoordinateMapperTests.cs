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

    [Fact]
    public void GetRegions_DimsOutsideSelectionOnly()
    {
        var regions = OverlayDimming.GetRegions(new Rect(0, 0, 100, 80), new Rect(20, 10, 50, 30));

        Assert.Equal(
            [
                new Rect(0, 0, 100, 10),
                new Rect(0, 40, 100, 40),
                new Rect(0, 10, 20, 30),
                new Rect(70, 10, 30, 30)
            ],
            regions);
    }

    [Fact]
    public void GetRegions_ClipsSelectionToBounds()
    {
        var regions = OverlayDimming.GetRegions(new Rect(0, 0, 100, 80), new Rect(-10, 20, 40, 30));

        Assert.Equal(
            [
                new Rect(0, 0, 100, 20),
                new Rect(0, 50, 100, 30),
                new Rect(30, 20, 70, 30)
            ],
            regions);
    }
}
