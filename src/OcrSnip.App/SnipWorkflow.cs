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

            ToastWindow.ShowMessage("Reading...");
            var bitmap = await ScreenCapture.CaptureAsync(selection.Value).ConfigureAwait(true);
            var result = await ocrEngine.RecognizeAsync(bitmap, CancellationToken.None).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(result.Text))
            {
                ToastWindow.ShowMessage("No text found");
                return;
            }

            if (ClipboardService.TrySetText(result.Text, out _))
            {
                ToastWindow.ShowMessage("Copied");
                return;
            }

            ToastWindow.ShowMessage("Clipboard busy - text opened");
            ResultWindow.ShowResult(result.Text);
        }
        catch (ModelUnavailableException ex)
        {
            ToastWindow.ShowMessage(ex.Message);
        }
        catch (Exception)
        {
            ocrEngine.Unload();
            ToastWindow.ShowMessage("OCR failed");
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
        ToastWindow.ShowMessage("Hotkey already in use");
        ShowSettings();
    }
}
