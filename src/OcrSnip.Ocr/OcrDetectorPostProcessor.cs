using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;

namespace OcrSnip.Ocr;

public static class OcrDetectorPostProcessor
{
    public static IReadOnlyList<OcrQuadrilateral> GetBoxes(
        Tensor<float> output,
        int imageWidth,
        int imageHeight,
        float mapThreshold = 0.20f,
        float boxThreshold = 0.45f,
        double unclipRatio = 1.4,
        int maxCandidates = 3000)
    {
        var mapHeight = output.Dimensions[2];
        var mapWidth = output.Dimensions[3];
        using var probability = new Mat(mapHeight, mapWidth, MatType.CV_32FC1);
        using var mask = new Mat(mapHeight, mapWidth, MatType.CV_8UC1);

        for (var y = 0; y < mapHeight; y++)
        {
            for (var x = 0; x < mapWidth; x++)
            {
                var value = output[0, 0, y, x];
                probability.Set(y, x, value);
                mask.Set(y, x, value > mapThreshold ? (byte)255 : (byte)0);
            }
        }

        return GetBoxes(probability, mask, imageWidth, imageHeight, boxThreshold, unclipRatio, maxCandidates);
    }

    public static IReadOnlyList<OcrQuadrilateral> GetBoxes(
        Mat probability,
        Mat mask,
        int imageWidth,
        int imageHeight,
        float boxThreshold = 0.45f,
        double unclipRatio = 1.4,
        int maxCandidates = 3000)
    {
        Cv2.FindContours(mask, out CvPoint[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
        var boxes = new List<OcrQuadrilateral>(Math.Min(contours.Length, maxCandidates));
        var ratioX = imageWidth / (float)probability.Width;
        var ratioY = imageHeight / (float)probability.Height;

        foreach (var contour in contours.OrderByDescending(contour => Cv2.ContourArea(contour)).Take(maxCandidates))
        {
            if (Cv2.ContourArea(contour) < 9)
            {
                continue;
            }

            var rect = Cv2.MinAreaRect(contour);
            var points = Cv2.BoxPoints(rect);
            var bounds = Cv2.BoundingRect(contour);
            var clipped = bounds & new OpenCvSharp.Rect(0, 0, probability.Width, probability.Height);
            if (clipped.Width <= 0 || clipped.Height <= 0)
            {
                continue;
            }

            var score = BoxScore(probability, contour, clipped);
            if (score < boxThreshold)
            {
                continue;
            }

            var mapped = points
                .Select(p => new OcrPoint(Clamp(p.X * ratioX, 0, imageWidth), Clamp(p.Y * ratioY, 0, imageHeight)))
                .ToArray();
            var expanded = OcrGeometry.ExpandPolygon(mapped, unclipRatio);
            boxes.Add(OcrGeometry.OrderPoints(expanded));
        }

        return boxes;
    }

    private static float Clamp(float value, float min, float max)
    {
        return MathF.Min(max, MathF.Max(min, value));
    }

    private static double BoxScore(Mat probability, CvPoint[] contour, OpenCvSharp.Rect bounds)
    {
        using var mask = new Mat(bounds.Height, bounds.Width, MatType.CV_8UC1, Scalar.All(0));
        var shifted = contour.Select(point => new CvPoint(point.X - bounds.X, point.Y - bounds.Y)).ToArray();
        Cv2.FillPoly(mask, [shifted], Scalar.All(255));
        using var roi = new Mat(probability, bounds);
        return Cv2.Mean(roi, mask).Val0;
    }
}
