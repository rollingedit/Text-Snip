using System.Windows;
using OcrSnip.App.Capture;
using OcrSnip.App.Clipboard;
using OcrSnip.App.Overlay;
using OcrSnip.App.Settings;
using OcrSnip.App.Toasts;
using OcrSnip.Ocr;

namespace OcrSnip.App;

public sealed class SnipWorkflow(SettingsStore settingsStore, AppSettings settings, OcrEngine ocrEngine)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly System.Threading.Timer _unloadTimer = new(_ => ocrEngine.Unload());

    public async Task StartSnipAsync()
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            var selection = OverlayWindow.SelectRectangle();
            if (selection is null)
            {
                return;
            }

            ShowToast("Reading...");
            ApplyOcrOptions();
            var bitmap = await ScreenCapture.CaptureAsync(selection.Value).ConfigureAwait(true);
            var result = await ocrEngine.RecognizeAsync(bitmap, CancellationToken.None).ConfigureAwait(true);
            ScheduleUnload();
            if (string.IsNullOrWhiteSpace(result.Text))
            {
                ShowToast("No text found");
                return;
            }

            if (ClipboardService.TrySetText(result.Text, out _))
            {
                ShowToast("Copied");
                return;
            }

            ShowToast("Clipboard busy - text opened");
            ResultWindow.ShowResult(result.Text);
        }
        catch (ModelUnavailableException ex)
        {
            ShowToast(ex.Message);
        }
        catch (Exception)
        {
            ocrEngine.Unload();
            ShowToast("OCR failed");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ShowSettings()
    {
        var window = new SettingsWindow(settingsStore, settings);
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
        if (settings.ToastEnabled)
        {
            ToastWindow.ShowMessage(message);
        }
    }

    private void ScheduleUnload()
    {
        var dueTime = settings.MemoryMode switch
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
        ocrEngine.Options.SmallTextBoost = settings.SmallTextBoost switch
        {
            SmallTextBoost.Off => SmallTextBoostMode.Off,
            SmallTextBoost.Scale150 => SmallTextBoostMode.Scale150,
            SmallTextBoost.Scale200 => SmallTextBoostMode.Scale200,
            SmallTextBoost.Scale300 => SmallTextBoostMode.Scale300,
            _ => SmallTextBoostMode.Auto
        };
        ocrEngine.Options.CopyMode = settings.CopyMode switch
        {
            CopyMode.Code => OcrCopyMode.Code,
            CopyMode.Smart => OcrCopyMode.Smart,
            _ => OcrCopyMode.Raw
        };
    }
}
