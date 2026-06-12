using Clipper2Lib;

namespace OcrSnip.Ocr;

public static class OcrGeometry
{
    private const double ClipperScale = 1000.0;

    public static OcrQuadrilateral OrderPoints(IReadOnlyList<OcrPoint> points)
    {
        if (points.Count < 4)
        {
            throw new ArgumentException("At least four points are required.", nameof(points));
        }

        var topLeft = points.MinBy(p => p.X + p.Y)!;
        var bottomRight = points.MaxBy(p => p.X + p.Y)!;
        var topRight = points.MaxBy(p => p.X - p.Y)!;
        var bottomLeft = points.MinBy(p => p.X - p.Y)!;
        return new OcrQuadrilateral(topLeft, topRight, bottomRight, bottomLeft);
    }

    public static IReadOnlyList<OcrPoint> ExpandPolygon(IReadOnlyList<OcrPoint> points, double unclipRatio)
    {
        if (points.Count < 3)
        {
            return points;
        }

        var area = Math.Abs(PolygonArea(points));
        var perimeter = PolygonPerimeter(points);
        if (area <= 0 || perimeter <= 0)
        {
            return points;
        }

        var offsetDistance = area * unclipRatio / perimeter;
        var path = new Path64();
        foreach (var point in points)
        {
            path.Add(new Point64(
                (long)Math.Round(point.X * ClipperScale),
                (long)Math.Round(point.Y * ClipperScale)));
        }

        var solution = new Paths64();
        var offset = new ClipperOffset(2.0, 0.25, false, false);
        offset.AddPath(path, JoinType.Round, EndType.Polygon);
        offset.Execute(offsetDistance * ClipperScale, solution);

        if (solution.Count == 0 || solution[0].Count < 3)
        {
            return points;
        }

        return solution[0]
            .Select(point => new OcrPoint((float)(point.X / ClipperScale), (float)(point.Y / ClipperScale)))
            .ToArray();
    }

    public static float CenterY(OcrQuadrilateral box)
    {
        return (box.TopLeft.Y + box.TopRight.Y + box.BottomRight.Y + box.BottomLeft.Y) / 4f;
    }

    public static float CenterX(OcrQuadrilateral box)
    {
        return (box.TopLeft.X + box.TopRight.X + box.BottomRight.X + box.BottomLeft.X) / 4f;
    }

    public static float LineHeight(OcrLine line)
    {
        return MathF.Max(
            Distance(line.Bounds.TopLeft, line.Bounds.BottomLeft),
            Distance(line.Bounds.TopRight, line.Bounds.BottomRight));
    }

    public static float Distance(OcrPoint a, OcrPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static double PolygonArea(IReadOnlyList<OcrPoint> points)
    {
        var sum = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            sum += current.X * next.Y - next.X * current.Y;
        }

        return sum / 2.0;
    }

    private static double PolygonPerimeter(IReadOnlyList<OcrPoint> points)
    {
        var perimeter = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            perimeter += Distance(points[i], points[(i + 1) % points.Count]);
        }

        return perimeter;
    }
}
