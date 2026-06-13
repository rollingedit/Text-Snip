using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

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
        lock (_lock)
        {
            EnsureLoaded();

            using var source = OcrImageOperations.BitmapSourceToBgrMat(crop);
            var lines = Options.SmallTextBoost == SmallTextBoostMode.Auto
                ? RecognizeAuto(source, cancellationToken)
                : RecognizeScaled(source, Options.SmallTextBoost, cancellationToken);

            var ordered = OcrTextFormatter.SortLines(lines);
            var diagnostics = OcrDiagnosticsAnalyzer.Analyze(source.Width, source.Height, ordered);
            return Task.FromResult(new OcrResult(OcrTextFormatter.FormatLines(ordered, Options.CopyMode), ordered, diagnostics));
        }
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

    private IReadOnlyList<OcrLine> RecognizeAuto(Mat source, CancellationToken cancellationToken)
    {
        var originalLines = RecognizeLines(source, cancellationToken, unclipRatio: 2.0);
        using var boosted = OcrImageOperations.ApplySmallTextBoost(source, SmallTextBoostMode.Auto);
        var boostedLines = RecognizeLines(boosted, cancellationToken, unclipRatio: 1.4)
            .Select(line => ScaleLine(line, source.Width / (float)boosted.Width, source.Height / (float)boosted.Height));

        return MergeOverlappingLines(originalLines.Concat(boostedLines));
    }

    private IReadOnlyList<OcrLine> RecognizeScaled(Mat source, SmallTextBoostMode boostMode, CancellationToken cancellationToken)
    {
        using var processed = OcrImageOperations.ApplySmallTextBoost(source, boostMode);
        return RecognizeLines(processed, cancellationToken, unclipRatio: 1.4)
            .Select(line => ScaleLine(line, source.Width / (float)processed.Width, source.Height / (float)processed.Height))
            .ToArray();
    }

    private IReadOnlyList<OcrLine> RecognizeLines(Mat image, CancellationToken cancellationToken, double unclipRatio)
    {
        return DetectLines(image, cancellationToken, unclipRatio)
            .Select(box => RecognizeLine(image, box, cancellationToken))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text) && line.Confidence >= 0.30f)
            .ToArray();
    }

    private IReadOnlyList<OcrQuadrilateral> DetectLines(Mat image, CancellationToken cancellationToken, double unclipRatio)
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

        return OcrDetectorPostProcessor.GetBoxes(results[0].AsTensor<float>(), image.Width, image.Height, unclipRatio: unclipRatio);
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
            throw new ModelUnavailableException(
                "OCR model files are missing: " + string.Join(", ", missing.Select(Path.GetFileName)));
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

    private static IReadOnlyList<OcrLine> MergeOverlappingLines(IEnumerable<OcrLine> candidates)
    {
        var merged = new List<OcrLine>();
        foreach (var candidate in candidates)
        {
            var existingIndex = merged.FindIndex(line => Overlaps(line.Bounds, candidate.Bounds));
            if (existingIndex < 0)
            {
                merged.Add(candidate);
                continue;
            }

            if (IsBetterLine(candidate, merged[existingIndex]))
            {
                merged[existingIndex] = candidate;
            }
        }

        return merged;
    }

    private static bool IsBetterLine(OcrLine candidate, OcrLine existing)
    {
        var candidateLength = candidate.Text.Count(character => !char.IsWhiteSpace(character));
        var existingLength = existing.Text.Count(character => !char.IsWhiteSpace(character));
        if (candidateLength >= existingLength + 2 && candidate.Confidence >= existing.Confidence - 0.06f)
        {
            return true;
        }

        if (existingLength >= candidateLength + 2 && existing.Confidence >= candidate.Confidence - 0.06f)
        {
            return false;
        }

        return candidate.Confidence > existing.Confidence;
    }

    private static bool Overlaps(OcrQuadrilateral first, OcrQuadrilateral second)
    {
        var a = Bounds(first);
        var b = Bounds(second);
        var intersectionWidth = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
        var intersectionHeight = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
        var intersection = intersectionWidth * intersectionHeight;
        if (intersection <= 0)
        {
            return false;
        }

        var smallerArea = Math.Min(a.Width * a.Height, b.Width * b.Height);
        return smallerArea > 0 && intersection / smallerArea >= 0.55f;
    }

    private static (float Left, float Top, float Right, float Bottom, float Width, float Height) Bounds(OcrQuadrilateral box)
    {
        var xs = new[] { box.TopLeft.X, box.TopRight.X, box.BottomRight.X, box.BottomLeft.X };
        var ys = new[] { box.TopLeft.Y, box.TopRight.Y, box.BottomRight.Y, box.BottomLeft.Y };
        var left = xs.Min();
        var top = ys.Min();
        var right = xs.Max();
        var bottom = ys.Max();
        return (left, top, right, bottom, right - left, bottom - top);
    }

    private static OcrLine ScaleLine(OcrLine line, float scaleX, float scaleY)
    {
        return line with { Bounds = ScaleBox(line.Bounds, scaleX, scaleY) };
    }

    private static OcrQuadrilateral ScaleBox(OcrQuadrilateral box, float scaleX, float scaleY)
    {
        return new OcrQuadrilateral(
            ScalePoint(box.TopLeft, scaleX, scaleY),
            ScalePoint(box.TopRight, scaleX, scaleY),
            ScalePoint(box.BottomRight, scaleX, scaleY),
            ScalePoint(box.BottomLeft, scaleX, scaleY));
    }

    private static OcrPoint ScalePoint(OcrPoint point, float scaleX, float scaleY)
    {
        return new OcrPoint(point.X * scaleX, point.Y * scaleY);
    }
}

public sealed record OcrResult(string Text, IReadOnlyList<OcrLine> Lines, OcrDiagnostics? Diagnostics = null);

public sealed record OcrDiagnostics(bool HasEdgeTouchingText, bool HasLikelyEdgeFragment)
{
    public bool HasSelectionEdgeRisk => HasEdgeTouchingText || HasLikelyEdgeFragment;
}

public sealed record OcrLine(string Text, float Confidence, OcrQuadrilateral Bounds);

public sealed record OcrQuadrilateral(OcrPoint TopLeft, OcrPoint TopRight, OcrPoint BottomRight, OcrPoint BottomLeft);

public sealed record OcrPoint(float X, float Y);

public sealed class ModelUnavailableException(string message) : Exception(message);
