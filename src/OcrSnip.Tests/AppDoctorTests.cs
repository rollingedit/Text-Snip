using OcrSnip.App;

namespace OcrSnip.Tests;

public sealed class AppDoctorTests
{
    [Fact]
    public void BuildReport_IncludesInstallStateAndMissingFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OcrSnipDoctorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var report = AppDoctor.BuildReport(directory);

            Assert.Contains("Text Snip Doctor", report);
            Assert.Contains("App directory:", report);
            Assert.Contains("Settings path:", report);
            Assert.Contains("Diagnostics log:", report);
            Assert.Contains("VC++ x64 runtime:", report);
            Assert.Contains("Models", report);
            Assert.Contains("Native dependencies", report);
            Assert.Contains("MISSING:", report);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
