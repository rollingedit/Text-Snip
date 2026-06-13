using System.Windows;
using System.Windows.Media.Imaging;
using OcrSnip.App.Capture;
using OcrSnip.App.Clipboard;
using OcrSnip.App.Overlay;
using OcrSnip.App.Settings;
using OcrSnip.App.Toasts;
using OcrSnip.Ocr;

namespace OcrSnip.App;

public sealed class SnipWorkflow
{
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly IWorkflowOcrEngine _ocrEngine;
    private readonly ISnipSelectionService _selection;
    private readonly IScreenCaptureService _capture;
    private readonly IClipboardWriter _clipboard;
    private readonly IToastService _toast;
    private readonly IResultPresenter _resultPresenter;
    private readonly Func<HotkeyDefinition, bool>? _applyHotkey;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly System.Threading.Timer _unloadTimer;

    public SnipWorkflow(SettingsStore settingsStore, AppSettings settings, OcrEngine ocrEngine, ISnipSelectionService? selection = null, Func<HotkeyDefinition, bool>? applyHotkey = null)
        : this(
            settingsStore,
            settings,
            new OcrEngineAdapter(ocrEngine),
            selection ?? new OverlaySelectionService(),
            new GdiScreenCaptureService(),
            new WpfClipboardWriter(),
            new ToastWindowService(),
            new ResultWindowPresenter(),
            applyHotkey)
    {
    }

    public SnipWorkflow(
        SettingsStore settingsStore,
        AppSettings settings,
        IWorkflowOcrEngine ocrEngine,
        ISnipSelectionService selection,
        IScreenCaptureService capture,
        IClipboardWriter clipboard,
        IToastService toast,
        IResultPresenter resultPresenter,
        Func<HotkeyDefinition, bool>? applyHotkey = null)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _ocrEngine = ocrEngine;
        _selection = selection;
        _capture = capture;
        _clipboard = clipboard;
        _toast = toast;
        _resultPresenter = resultPresenter;
        _applyHotkey = applyHotkey;
        _unloadTimer = new System.Threading.Timer(_ => _ocrEngine.Unload());
    }

    public async Task StartSnipAsync()
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            _unloadTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            ShowToast("Drag to select text");
            var selection = _selection.SelectRectangle();
            if (selection is null)
            {
                ShowToast("Snip canceled");
                return;
            }

            ShowToast("Reading...");
            ApplyOcrOptions();
            var bitmap = await _capture.CaptureAsync(selection.Value).ConfigureAwait(true);
            var result = await _ocrEngine.RecognizeAsync(bitmap, CancellationToken.None).ConfigureAwait(true);
            ScheduleUnload();
            if (string.IsNullOrWhiteSpace(result.Text))
            {
                ShowToast("No text found");
                return;
            }

            if (_clipboard.TrySetText(result.Text, out _))
            {
                ShowToast(GetCopiedMessage(result));
                return;
            }

            ShowToast("Clipboard busy - text opened");
            _resultPresenter.ShowResult(result.Text);
        }
        catch (ModelUnavailableException ex)
        {
            AppDiagnostics.LogException("OCR model unavailable.", ex);
            ShowToast("OCR model missing - details opened");
            _resultPresenter.ShowResult(OcrFailureDiagnostics.Format(ex));
        }
        catch (Exception ex)
        {
            _ocrEngine.Unload();
            AppDiagnostics.LogException("Snip workflow failed.", ex);
            ShowToast("OCR failed - details opened");
            _resultPresenter.ShowResult(OcrFailureDiagnostics.Format(ex));
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ShowSettings()
    {
        var window = new SettingsWindow(_settingsStore, _settings, _applyHotkey);
        window.Show();
        window.Activate();
    }

    public void ShowHotkeyConflict()
    {
        ShowToast("Hotkey already in use");
        ShowSettings();
    }

    private void ShowToast(string message)
    {
        if (_settings.ToastEnabled)
        {
            _toast.ShowMessage(message);
        }
    }

    private static string GetCopiedMessage(OcrResult result)
    {
        return result.Diagnostics?.HasSelectionEdgeRisk == true
            ? "Copied - check selection edges"
            : "Copied";
    }

    private void ScheduleUnload()
    {
        var dueTime = _settings.MemoryMode switch
        {
            MemoryMode.LowMemory => TimeSpan.FromSeconds(60),
            MemoryMode.Balanced => TimeSpan.FromMinutes(10),
            MemoryMode.Performance => Timeout.InfiniteTimeSpan,
            _ => TimeSpan.FromMinutes(10)
        };
        _unloadTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
    }

    private void ApplyOcrOptions()
    {
        _ocrEngine.Options.SmallTextBoost = _settings.SmallTextBoost switch
        {
            SmallTextBoost.Off => SmallTextBoostMode.Off,
            SmallTextBoost.Scale150 => SmallTextBoostMode.Scale150,
            SmallTextBoost.Scale200 => SmallTextBoostMode.Scale200,
            SmallTextBoost.Scale300 => SmallTextBoostMode.Scale300,
            _ => SmallTextBoostMode.Auto
        };
        _ocrEngine.Options.CopyMode = _settings.CopyMode switch
        {
            CopyMode.Code => OcrCopyMode.Code,
            CopyMode.Smart => OcrCopyMode.Smart,
            _ => OcrCopyMode.Raw
        };
    }
}

public interface IWorkflowOcrEngine
{
    OcrOptions Options { get; }
    Task<OcrResult> RecognizeAsync(BitmapSource crop, CancellationToken cancellationToken);
    void Unload();
}

public interface ISnipSelectionService
{
    Int32Rect? SelectRectangle();
}

public interface IScreenCaptureService
{
    Task<BitmapSource> CaptureAsync(Int32Rect rectangle);
}

public interface IClipboardWriter
{
    bool TrySetText(string text, out Exception? error);
}

public interface IToastService
{
    void ShowMessage(string message);
}

public interface IResultPresenter
{
    void ShowResult(string text);
}

file sealed class OcrEngineAdapter(OcrEngine engine) : IWorkflowOcrEngine
{
    public OcrOptions Options => engine.Options;
    public Task<OcrResult> RecognizeAsync(BitmapSource crop, CancellationToken cancellationToken) => engine.RecognizeAsync(crop, cancellationToken);
    public void Unload() => engine.Unload();
}

file sealed class OverlaySelectionService : ISnipSelectionService
{
    public Int32Rect? SelectRectangle() => OverlayWindow.SelectRectangle();
}

public sealed class FixedSelectionService(Int32Rect rectangle) : ISnipSelectionService
{
    public Int32Rect? SelectRectangle() => rectangle;
}

file sealed class GdiScreenCaptureService : IScreenCaptureService
{
    public Task<BitmapSource> CaptureAsync(Int32Rect rectangle) => ScreenCapture.CaptureAsync(rectangle);
}

file sealed class WpfClipboardWriter : IClipboardWriter
{
    public bool TrySetText(string text, out Exception? error) => ClipboardService.TrySetText(text, out error);
}

file sealed class ToastWindowService : IToastService
{
    public void ShowMessage(string message) => ToastWindow.ShowMessage(message);
}

file sealed class ResultWindowPresenter : IResultPresenter
{
    public void ShowResult(string text) => ResultWindow.ShowResult(text);
}
