using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;

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

        using var source = OcrImageOperations.BitmapSourceToBgrMat(crop);
        using var boosted = OcrImageOperations.ApplySmallTextBoost(source, Options.SmallTextBoost);
        var lines = DetectLines(boosted, cancellationToken)
            .Select(box => RecognizeLine(boosted, box, cancellationToken))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text) && line.Confidence >= 0.30f)
            .ToArray();

        var ordered = OcrTextFormatter.SortLines(lines);
        return Task.FromResult(new OcrResult(OcrTextFormatter.FormatLines(ordered, Options.CopyMode), ordered));
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
        var input = OcrImageOperations.CreateDetectorTensor(resized);
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
            var expanded = OcrGeometry.ExpandPolygon(mapped, 1.4);
            boxes.Add(OcrGeometry.OrderPoints(expanded));
        }

        return boxes;
    }

    private OcrLine RecognizeLine(Mat image, OcrQuadrilateral box, CancellationToken cancellationToken)
    {
        var recognizer = _recognizer ?? throw new InvalidOperationException("Recognizer is not loaded.");
        using var crop = OcrImageOperations.PerspectiveCrop(image, box);
        if (crop.Width < 8 || crop.Height < 4)
        {
            return new OcrLine(string.Empty, 0, box);
        }

        var input = OcrImageOperations.CreateRecognizerTensor(crop);
        using var results = recognizer.Run([NamedOnnxValue.CreateFromTensor("x", input)]);
        cancellationToken.ThrowIfCancellationRequested();
        var (text, confidence) = CtcDecoder.Decode(results[0].AsTensor<float>(), _characters ?? []);
        return new OcrLine(text, confidence, box);
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
            _characters ??= ModelConfig.LoadCharacters(modelPaths.RecognizerConfig);
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
