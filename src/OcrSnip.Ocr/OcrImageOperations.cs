using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using WpfPixelFormats = System.Windows.Media.PixelFormats;

namespace OcrSnip.Ocr;

public static class OcrImageOperations
{
    public static DenseTensor<float> CreateDetectorTensor(Mat bgr)
    {
        var tensor = new DenseTensor<float>([1, 3, bgr.Height, bgr.Width]);
        var mean = new[] { 0.485f, 0.456f, 0.406f };
        var std = new[] { 0.229f, 0.224f, 0.225f };

        var height = bgr.Height;
        var width = bgr.Width;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = bgr.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = ((pixel.Item2 / 255f) - mean[0]) / std[0];
                tensor[0, 1, y, x] = ((pixel.Item1 / 255f) - mean[1]) / std[1];
                tensor[0, 2, y, x] = ((pixel.Item0 / 255f) - mean[2]) / std[2];
            }
        }

        return tensor;
    }

    public static DenseTensor<float> CreateRecognizerTensor(Mat bgr)
    {
        const int targetHeight = 48;
        var targetWidth = GetRecognizerTargetWidth(bgr.Width, bgr.Height);
        var ratio = Math.Min(targetWidth / (double)bgr.Width, targetHeight / (double)bgr.Height);
        var resizedWidth = Math.Clamp((int)Math.Round(bgr.Width * ratio), 1, targetWidth);
        using var resized = new Mat();
        Cv2.Resize(bgr, resized, new OpenCvSharp.Size(resizedWidth, targetHeight), 0, 0, InterpolationFlags.Linear);

        using var padded = new Mat(targetHeight, targetWidth, MatType.CV_8UC3, Scalar.All(0));
        resized.CopyTo(new Mat(padded, new OpenCvSharp.Rect(0, 0, resizedWidth, targetHeight)));

        var tensor = new DenseTensor<float>([1, 3, targetHeight, targetWidth]);
        for (var y = 0; y < targetHeight; y++)
        {
            for (var x = 0; x < targetWidth; x++)
            {
                var pixel = padded.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = (pixel.Item2 / 255f - 0.5f) / 0.5f;
                tensor[0, 1, y, x] = (pixel.Item1 / 255f - 0.5f) / 0.5f;
                tensor[0, 2, y, x] = (pixel.Item0 / 255f - 0.5f) / 0.5f;
            }
        }

        return tensor;
    }

    public static int GetRecognizerTargetWidth(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        const int targetHeight = 48;
        const int minWidth = 160;
        const int maxWidth = 3200;
        var aspectWidth = (int)Math.Ceiling(targetHeight * (width / (double)height));
        return Math.Clamp(RoundUpToMultiple(aspectWidth, 8), minWidth, maxWidth);
    }

    private static int RoundUpToMultiple(int value, int multiple)
    {
        return Math.Max(multiple, (int)Math.Ceiling(value / (double)multiple) * multiple);
    }

    public static Mat BitmapSourceToBgrMat(System.Windows.Media.Imaging.BitmapSource source)
    {
        var converted = source.Format == WpfPixelFormats.Bgra32
            ? source
            : new System.Windows.Media.Imaging.FormatConvertedBitmap(source, WpfPixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        var bgra = Mat.FromPixelData(converted.PixelHeight, converted.PixelWidth, MatType.CV_8UC4, pixels);
        var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        bgra.Dispose();
        return bgr;
    }

    public static Mat ApplySmallTextBoost(Mat source, SmallTextBoostMode mode)
    {
        var maxSide = Math.Max(source.Width, source.Height);
        var minSide = Math.Min(source.Width, source.Height);
        var scale = mode switch
        {
            SmallTextBoostMode.Off => 1.0,
            SmallTextBoostMode.Scale150 => 1.5,
            SmallTextBoostMode.Scale200 => 2.0,
            SmallTextBoostMode.Scale300 => 3.0,
            _ => maxSide < 900 ? 2.0 : maxSide < 1400 && minSide < 700 ? 1.5 : 1.0
        };
        var finalMax = maxSide * scale;
        if (finalMax > 1800)
        {
            scale = 1800.0 / maxSide;
        }

        if (scale <= 1.01)
        {
            return source.Clone();
        }

        var resized = new Mat();
        Cv2.Resize(source, resized, new OpenCvSharp.Size((int)Math.Round(source.Width * scale), (int)Math.Round(source.Height * scale)), 0, 0, InterpolationFlags.Cubic);
        return resized;
    }

    public static Mat PerspectiveCrop(Mat image, OcrQuadrilateral box)
    {
        var width = Math.Max(
            OcrGeometry.Distance(box.TopLeft, box.TopRight),
            OcrGeometry.Distance(box.BottomLeft, box.BottomRight));
        var height = Math.Max(
            OcrGeometry.Distance(box.TopLeft, box.BottomLeft),
            OcrGeometry.Distance(box.TopRight, box.BottomRight));

        var outputWidth = Math.Max(1, (int)Math.Round(width));
        var outputHeight = Math.Max(1, (int)Math.Round(height));
        var source = new[]
        {
            new Point2f(box.TopLeft.X, box.TopLeft.Y),
            new Point2f(box.TopRight.X, box.TopRight.Y),
            new Point2f(box.BottomRight.X, box.BottomRight.Y),
            new Point2f(box.BottomLeft.X, box.BottomLeft.Y)
        };
        var destination = new[]
        {
            new Point2f(0, 0),
            new Point2f(outputWidth - 1, 0),
            new Point2f(outputWidth - 1, outputHeight - 1),
            new Point2f(0, outputHeight - 1)
        };

        using var transform = Cv2.GetPerspectiveTransform(source, destination);
        var cropped = new Mat();
        Cv2.WarpPerspective(image, cropped, transform, new OpenCvSharp.Size(outputWidth, outputHeight), InterpolationFlags.Cubic, BorderTypes.Replicate);
        return cropped;
    }
}
