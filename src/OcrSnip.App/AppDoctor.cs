using System.IO;
using System.Text;
using Microsoft.Win32;
using OcrSnip.Ocr;

namespace OcrSnip.App;

public static class AppDoctor
{
    private static readonly string[] NativeDependencies =
    [
        "onnxruntime.dll",
        "OpenCvSharpExtern.dll"
    ];

    public static string BuildReport(string appBaseDirectory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Text Snip Doctor");
        builder.AppendLine();
        builder.AppendLine($"App directory: {appBaseDirectory}");
        builder.AppendLine($"Settings path: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OcrSnip", "settings.json")}");
        builder.AppendLine($"Diagnostics log: {AppDiagnostics.LogPath}");
        builder.AppendLine();

        AppendRuntime(builder);
        AppendFiles(builder, "Models", ModelPaths.FromAppBaseDirectory(appBaseDirectory).AllFiles);
        AppendFiles(builder, "Native dependencies", NativeDependencies.Select(name => Path.Combine(appBaseDirectory, name)));

        return builder.ToString();
    }

    private static void AppendRuntime(StringBuilder builder)
    {
        builder.AppendLine("Runtime");
        builder.AppendLine($"- .NET: {Environment.Version}");
        builder.AppendLine($"- OS: {Environment.OSVersion.VersionString}");
        builder.AppendLine($"- VC++ x64 runtime: {GetVCRuntimeStatus()}");
        builder.AppendLine();
    }

    private static void AppendFiles(StringBuilder builder, string title, IEnumerable<string> paths)
    {
        builder.AppendLine(title);
        foreach (var path in paths)
        {
            builder.AppendLine(File.Exists(path)
                ? $"- OK: {path}"
                : $"- MISSING: {path}");
        }

        builder.AppendLine();
    }

    private static string GetVCRuntimeStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
            var version = key?.GetValue("Version") as string;
            return string.IsNullOrWhiteSpace(version) ? "missing or not registered" : version;
        }
        catch (Exception ex)
        {
            return $"unknown ({ex.GetType().Name})";
        }
    }
}
