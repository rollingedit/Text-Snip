using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OcrSnip.App;
using OcrSnip.App.Settings;
using OcrSnip.Ocr;

namespace OcrSnip.Tests;

public sealed class SnipWorkflowTests
{
    [Fact]
    public async Task StartSnipAsync_CopiesRecognizedTextAndShowsCopiedToast()
    {
        var fakes = new Fakes { OcrResult = new OcrResult("hello", []) };
        var workflow = CreateWorkflow(fakes);

        await workflow.StartSnipAsync();

        Assert.Equal("hello", fakes.CopiedText);
        Assert.Contains("Drag to select text", fakes.Toasts);
        Assert.Contains("Reading...", fakes.Toasts);
        Assert.Contains("Copied", fakes.Toasts);
    }

    [Fact]
    public async Task StartSnipAsync_WarnsWhenCopiedTextHasSelectionEdgeRisk()
    {
        var diagnostics = new OcrDiagnostics(HasEdgeTouchingText: true, HasLikelyEdgeFragment: false);
        var fakes = new Fakes { OcrResult = new OcrResult("hello", [], diagnostics) };
        var workflow = CreateWorkflow(fakes);

        await workflow.StartSnipAsync();

        Assert.Equal("hello", fakes.CopiedText);
        Assert.Contains("Copied - check selection edges", fakes.Toasts);
        Assert.DoesNotContain("Copied", fakes.Toasts);
    }

    [Fact]
    public async Task StartSnipAsync_ShowsNoTextWhenOcrResultIsBlank()
    {
        var fakes = new Fakes { OcrResult = new OcrResult(" ", []) };
        var workflow = CreateWorkflow(fakes);

        await workflow.StartSnipAsync();

        Assert.Null(fakes.CopiedText);
        Assert.Contains("Drag to select text", fakes.Toasts);
        Assert.Contains("No text found", fakes.Toasts);
    }

    [Fact]
    public async Task StartSnipAsync_PresentsResultWhenClipboardIsBusy()
    {
        var fakes = new Fakes { OcrResult = new OcrResult("fallback", []), ClipboardSucceeds = false };
        var workflow = CreateWorkflow(fakes);

        await workflow.StartSnipAsync();

        Assert.Equal("fallback", fakes.PresentedText);
        Assert.Contains("Clipboard busy - text opened", fakes.Toasts);
    }

    [Fact]
    public async Task StartSnipAsync_PresentsDiagnosticsWhenOcrThrows()
    {
        var fakes = new Fakes { OcrException = new DllNotFoundException("OpenCvSharpExtern.dll") };
        var workflow = CreateWorkflow(fakes);

        await workflow.StartSnipAsync();

        Assert.Null(fakes.CopiedText);
        Assert.Contains("OCR failed - details opened", fakes.Toasts);
        Assert.Contains("DllNotFoundException", fakes.PresentedText);
        Assert.Contains("OpenCvSharpExtern.dll", fakes.PresentedText);
    }

    [Fact]
    public async Task StartSnipAsync_HonorsToastDisabledAndMapsOcrOptions()
    {
        var fakes = new Fakes { OcrResult = new OcrResult("code", []) };
        var settings = new AppSettings
        {
            ToastEnabled = false,
            SmallTextBoost = SmallTextBoost.Scale300,
            CopyMode = CopyMode.Code
        };
        var workflow = CreateWorkflow(fakes, settings);

        await workflow.StartSnipAsync();

        Assert.Empty(fakes.Toasts);
        Assert.Equal(SmallTextBoostMode.Scale300, fakes.Ocr.Options.SmallTextBoost);
        Assert.Equal(OcrCopyMode.Code, fakes.Ocr.Options.CopyMode);
    }

    [Fact]
    public async Task StartSnipAsync_ShowsCanceledWhenSelectionIsMissing()
    {
        var fakes = new Fakes { Selection = null };
        var workflow = CreateWorkflow(fakes);

        await workflow.StartSnipAsync();

        Assert.Null(fakes.CopiedText);
        Assert.Contains("Drag to select text", fakes.Toasts);
        Assert.Contains("Snip canceled", fakes.Toasts);
    }

    private static SnipWorkflow CreateWorkflow(Fakes fakes, AppSettings? settings = null)
    {
        return new SnipWorkflow(
            new SettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json")),
            settings ?? new AppSettings(),
            fakes.Ocr,
            fakes,
            fakes,
            fakes,
            fakes,
            fakes);
    }

    private sealed class Fakes : IWorkflowOcrEngine, ISnipSelectionService, IScreenCaptureService, IClipboardWriter, IToastService, IResultPresenter
    {
        public FakeOcr Ocr { get; } = new();
        public OcrResult OcrResult { get => Ocr.Result; set => Ocr.Result = value; }
        public Exception? OcrException { get => Ocr.Exception; set => Ocr.Exception = value; }
        public Int32Rect? Selection { get; set; } = new(0, 0, 10, 10);
        public bool ClipboardSucceeds { get; set; } = true;
        public string? CopiedText { get; private set; }
        public string? PresentedText { get; private set; }
        public List<string> Toasts { get; } = [];
        public OcrOptions Options => Ocr.Options;
        public Task<OcrResult> RecognizeAsync(BitmapSource crop, CancellationToken cancellationToken) => Ocr.RecognizeAsync(crop, cancellationToken);
        public void Unload() => Ocr.Unload();
        public Int32Rect? SelectRectangle() => Selection;
        public Task<BitmapSource> CaptureAsync(Int32Rect rectangle) => Task.FromResult(CreateBitmap());
        public bool TrySetText(string text, out Exception? error)
        {
            error = ClipboardSucceeds ? null : new InvalidOperationException("busy");
            if (ClipboardSucceeds)
            {
                CopiedText = text;
            }

            return ClipboardSucceeds;
        }
        public void ShowMessage(string message) => Toasts.Add(message);
        public void ShowResult(string text) => PresentedText = text;

        private static BitmapSource CreateBitmap()
        {
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
            bitmap.Freeze();
            return bitmap;
        }
    }

    private sealed class FakeOcr : IWorkflowOcrEngine
    {
        public OcrOptions Options { get; } = new();
        public OcrResult Result { get; set; } = new("text", []);
        public Exception? Exception { get; set; }
        public bool Unloaded { get; private set; }
        public Task<OcrResult> RecognizeAsync(BitmapSource crop, CancellationToken cancellationToken)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
        public void Unload() => Unloaded = true;
    }
}
