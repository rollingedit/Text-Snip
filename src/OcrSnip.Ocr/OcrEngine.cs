using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;
using WpfPixelFormats = System.Windows.Media.PixelFormats;

namespace OcrSnip.Ocr;

public sealed class OcrEngine(ModelPaths modelPaths) : IDisposable
{
    private readonly object _lock = new();
    private InferenceSession? _detector;
    private InferenceSession? _recognizer;
    private string[]? _characters;

    public OcrOptions Options { get; } = new();

    public Task WarmAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLoaded();
        return Task.CompletedTask;
    }

    public Task<OcrResult> RecognizeAsync(BitmapSource crop, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(crop);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLoaded();

        using var source = BitmapSourceToBgrMat(crop);
        using var boosted = ApplySmallTextBoost(source, Options.SmallTextBoost);
        var lines = DetectLines(boosted, cancellationToken)
            .Select(box => RecognizeLine(boosted, box, cancellationToken))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text) && line.Confidence >= 0.30f)
            .ToArray();

        var ordered = SortLines(lines);
        return Task.FromResult(new OcrResult(FormatLines(ordered, Options.CopyMode), ordered));
    }

    public void Unload()
    {
        lock (_lock)
        {
            _detector?.Dispose();
            _recognizer?.Dispose();
            _detector = null;
            _recognizer = null;
            _characters = null;
        }
    }

    public void Dispose() => Unload();

    private IReadOnlyList<OcrQuadrilateral> DetectLines(Mat image, CancellationToken cancellationToken)
    {
        var detector = _detector ?? throw new InvalidOperationException("Detector is not loaded.");
        var scale = GetDetectorScale(image.Width, image.Height);
        var resizedWidth = RoundToMultiple(Math.Max(32, (int)Math.Round(image.Width * scale)), 32);
        var resizedHeight = RoundToMultiple(Math.Max(32, (int)Math.Round(image.Height * scale)), 32);

        using var resized = new Mat();
        Cv2.Resize(image, resized, new OpenCvSharp.Size(resizedWidth, resizedHeight), 0, 0, InterpolationFlags.Linear);
        var input = CreateDetectorTensor(resized);
        using var results = detector.Run([NamedOnnxValue.CreateFromTensor("x", input)]);
        cancellationToken.ThrowIfCancellationRequested();

        var output = results[0].AsTensor<float>();
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
                mask.Set(y, x, value > 0.20f ? (byte)255 : (byte)0);
            }
        }

        Cv2.FindContours(mask, out CvPoint[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
        var boxes = new List<OcrQuadrilateral>(Math.Min(contours.Length, 3000));
        var ratioX = image.Width / (float)mapWidth;
        var ratioY = image.Height / (float)mapHeight;

        foreach (var contour in contours.OrderByDescending(contour => Cv2.ContourArea(contour)).Take(3000))
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

            using var roi = new Mat(probability, clipped);
            var score = Cv2.Mean(roi).Val0;
            if (score < 0.45)
            {
                continue;
            }

            var mapped = points
                .Select(p => new OcrPoint(Clamp(p.X * ratioX, 0, image.Width), Clamp(p.Y * ratioY, 0, image.Height)))
                .ToArray();
            boxes.Add(OrderPoints(mapped));
        }

        return boxes;
    }

    private OcrLine RecognizeLine(Mat image, OcrQuadrilateral box, CancellationToken cancellationToken)
    {
        var recognizer = _recognizer ?? throw new InvalidOperationException("Recognizer is not loaded.");
        using var crop = CropAxisAligned(image, box);
        if (crop.Width < 8 || crop.Height < 4)
        {
            return new OcrLine(string.Empty, 0, box);
        }

        var input = CreateRecognizerTensor(crop);
        using var results = recognizer.Run([NamedOnnxValue.CreateFromTensor("x", input)]);
        cancellationToken.ThrowIfCancellationRequested();
        var (text, confidence) = DecodeCtc(results[0].AsTensor<float>());
        return new OcrLine(text, confidence, box);
    }

    private (string Text, float Confidence) DecodeCtc(Tensor<float> output)
    {
        var characters = _characters ?? throw new InvalidOperationException("Character dictionary is not loaded.");
        var timeSteps = output.Dimensions[1];
        var classCount = output.Dimensions[2];
        var builder = new StringBuilder();
        var confidences = new List<float>();
        var previous = -1;

        for (var t = 0; t < timeSteps; t++)
        {
            var bestIndex = 0;
            var best = float.NegativeInfinity;
            for (var c = 0; c < classCount; c++)
            {
                var value = output[0, t, c];
                if (value > best)
                {
                    best = value;
                    bestIndex = c;
                }
            }

            if (bestIndex != 0 && bestIndex != previous)
            {
                var charIndex = bestIndex - 1;
                if ((uint)charIndex < (uint)characters.Length)
                {
                    builder.Append(characters[charIndex]);
                    confidences.Add(best);
                }
            }

            previous = bestIndex;
        }

        var confidence = confidences.Count == 0 ? 0 : confidences.Average();
        return (builder.ToString(), confidence);
    }

    private void EnsureLoaded()
    {
        var missing = modelPaths.AllFiles.Where(path => !File.Exists(path)).ToArray();
        if (missing.Length > 0)
        {
            throw new ModelUnavailableException("OCR model missing");
        }

        lock (_lock)
        {
            _detector ??= CreateSession(modelPaths.DetectorOnnx);
            _recognizer ??= CreateSession(modelPaths.RecognizerOnnx);
            _characters ??= LoadCharacters(modelPaths.RecognizerConfig);
        }
    }

    private static InferenceSession CreateSession(string path)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Math.Min(Environment.ProcessorCount, 8)
        };
        return new InferenceSession(path, options);
    }

    private static DenseTensor<float> CreateDetectorTensor(Mat bgr)
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

    private static DenseTensor<float> CreateRecognizerTensor(Mat bgr)
    {
        const int targetHeight = 48;
        const int targetWidth = 320;
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

    private static Mat BitmapSourceToBgrMat(BitmapSource source)
    {
        var converted = source.Format == WpfPixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, WpfPixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        var bgra = Mat.FromPixelData(converted.PixelHeight, converted.PixelWidth, MatType.CV_8UC4, pixels);
        var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        bgra.Dispose();
        return bgr;
    }

    private static Mat ApplySmallTextBoost(Mat source, SmallTextBoostMode mode)
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

    private static Mat CropAxisAligned(Mat image, OcrQuadrilateral box)
    {
        var xs = new[] { box.TopLeft.X, box.TopRight.X, box.BottomRight.X, box.BottomLeft.X };
        var ys = new[] { box.TopLeft.Y, box.TopRight.Y, box.BottomRight.Y, box.BottomLeft.Y };
        var left = (int)Math.Floor(Math.Max(0, xs.Min() - 2));
        var top = (int)Math.Floor(Math.Max(0, ys.Min() - 2));
        var right = (int)Math.Ceiling(Math.Min(image.Width, xs.Max() + 2));
        var bottom = (int)Math.Ceiling(Math.Min(image.Height, ys.Max() + 2));
        var rect = new OpenCvSharp.Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        return new Mat(image, rect).Clone();
    }

    private static string[] LoadCharacters(string configPath)
    {
        var characters = new List<string>();
        var inDictionary = false;
        foreach (var rawLine in File.ReadLines(configPath))
        {
            var line = rawLine.TrimEnd();
            if (line.Trim() == "character_dict:")
            {
                inDictionary = true;
                continue;
            }

            if (!inDictionary)
            {
                continue;
            }

            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                break;
            }

            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            var value = trimmed[2..];
            characters.Add(UnquoteYamlScalar(value));
        }

        return characters.ToArray();
    }

    private static string UnquoteYamlScalar(string value)
    {
        if (value == "''")
        {
            return "'";
        }

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }

    private static IReadOnlyList<OcrLine> SortLines(IReadOnlyList<OcrLine> lines)
    {
        if (lines.Count <= 1)
        {
            return lines;
        }

        var medianHeight = lines.Select(LineHeight).Order().ElementAt(lines.Count / 2);
        return lines
            .OrderBy(line => Math.Round(CenterY(line.Bounds) / Math.Max(1, medianHeight * 0.6f)))
            .ThenBy(line => line.Bounds.TopLeft.X)
            .ToArray();
    }

    private static string FormatLines(IReadOnlyList<OcrLine> lines, OcrCopyMode copyMode)
    {
        if (copyMode == OcrCopyMode.Code)
        {
            return string.Join(Environment.NewLine, lines.Select(line => line.Text).Where(text => text.Length > 0));
        }

        return string.Join(Environment.NewLine, lines.Select(line => line.Text.Trim()).Where(text => text.Length > 0));
    }

    private static OcrQuadrilateral OrderPoints(IReadOnlyList<OcrPoint> points)
    {
        var topLeft = points.MinBy(p => p.X + p.Y)!;
        var bottomRight = points.MaxBy(p => p.X + p.Y)!;
        var topRight = points.MaxBy(p => p.X - p.Y)!;
        var bottomLeft = points.MinBy(p => p.X - p.Y)!;
        return new OcrQuadrilateral(topLeft, topRight, bottomRight, bottomLeft);
    }

    private static float CenterY(OcrQuadrilateral box)
    {
        return (box.TopLeft.Y + box.TopRight.Y + box.BottomRight.Y + box.BottomLeft.Y) / 4f;
    }

    private static float LineHeight(OcrLine line)
    {
        return MathF.Max(
            Distance(line.Bounds.TopLeft, line.Bounds.BottomLeft),
            Distance(line.Bounds.TopRight, line.Bounds.BottomRight));
    }

    private static float Distance(OcrPoint a, OcrPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static double GetDetectorScale(int width, int height)
    {
        const int maxSide = 1536;
        var side = Math.Max(width, height);
        return side > maxSide ? maxSide / (double)side : 1.0;
    }

    private static int RoundToMultiple(int value, int multiple)
    {
        return Math.Max(multiple, (int)Math.Ceiling(value / (double)multiple) * multiple);
    }

    private static float Clamp(float value, float min, float max)
    {
        return MathF.Min(max, MathF.Max(min, value));
    }
}

public sealed record OcrResult(string Text, IReadOnlyList<OcrLine> Lines);

public sealed record OcrLine(string Text, float Confidence, OcrQuadrilateral Bounds);

public sealed record OcrQuadrilateral(OcrPoint TopLeft, OcrPoint TopRight, OcrPoint BottomRight, OcrPoint BottomLeft);

public sealed record OcrPoint(float X, float Y);

public sealed class ModelUnavailableException(string message) : Exception(message);
