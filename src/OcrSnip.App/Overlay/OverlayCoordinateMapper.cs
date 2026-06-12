using System.Windows;
using WpfPoint = System.Windows.Point;

namespace OcrSnip.App.Overlay;

public static class OverlayCoordinateMapper
{
    public static Int32Rect ToPhysicalRect(Rect selectionDip, double virtualLeftDip, double virtualTopDip, double scaleX, double scaleY)
    {
        var left = (int)Math.Round((virtualLeftDip + selectionDip.X) * scaleX);
        var top = (int)Math.Round((virtualTopDip + selectionDip.Y) * scaleY);
        var width = Math.Max(1, (int)Math.Round(selectionDip.Width * scaleX));
        var height = Math.Max(1, (int)Math.Round(selectionDip.Height * scaleY));
        return new Int32Rect(left, top, width, height);
    }

    public static Rect Normalize(WpfPoint a, WpfPoint b)
    {
        return new Rect(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X),
            Math.Abs(a.Y - b.Y));
    }
}
