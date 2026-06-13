using System.Windows;

namespace OcrSnip.App.Clipboard;

public static class ClipboardService
{
    public static bool TrySetText(string text, out Exception? error)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText);
                System.Windows.Clipboard.Flush();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Thread.Sleep(50);
            }
        }

        error = lastError ?? new InvalidOperationException("Clipboard busy.");
        return false;
    }
}
