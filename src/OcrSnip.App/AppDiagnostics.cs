using System.IO;

namespace OcrSnip.App;

public static class AppDiagnostics
{
    private static readonly object SyncRoot = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OcrSnip",
        "logs",
        "diagnostics.log");

    public static void LogException(string context, Exception exception)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:O} {context}{Environment.NewLine}{OcrFailureDiagnostics.Format(exception)}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never break OCR or app startup.
        }
    }
}
