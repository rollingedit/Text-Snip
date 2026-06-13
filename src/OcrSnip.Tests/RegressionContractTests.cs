namespace OcrSnip.Tests;

public sealed class RegressionContractTests
{
    [Fact]
    public void ClipboardNativeWriter_UsesRealOwnerWindow()
    {
        var source = File.ReadAllText(GetRepoPath("src", "OcrSnip.App", "Clipboard", "ClipboardService.cs"));

        Assert.Contains("new HwndSourceParameters(\"TextSnipClipboardOwner\")", source);
        Assert.Contains("OpenClipboard(ClipboardOwner.Value.Handle)", source);
        Assert.Contains("TryReadExistingText", source);
        Assert.Contains("clipboardChanged && previousText is not null", source);
        Assert.DoesNotContain("OpenClipboard(0)", source);
        Assert.DoesNotContain("OpenClipboard(IntPtr.Zero)", source);
    }

    [Fact]
    public void HotkeyConflictSettingsPath_CanApplyAlternateHotkey()
    {
        var settingsWindow = File.ReadAllText(GetRepoPath("src", "OcrSnip.App", "Settings", "SettingsWindow.cs"));
        var app = File.ReadAllText(GetRepoPath("src", "OcrSnip.App", "App.xaml.cs"));

        Assert.Contains("Func<HotkeyDefinition, bool>? applyHotkey", settingsWindow);
        Assert.Contains("HotkeyDefinition.TryParse", settingsWindow);
        Assert.Contains("Hotkey already in use", settingsWindow);
        Assert.Contains("ReconfigureHotkey", app);
        Assert.Contains("new HotkeyService(definition", app);
    }

    [Fact]
    public void OcrUnload_IsSynchronizedWithRecognition()
    {
        var source = File.ReadAllText(GetRepoPath("src", "OcrSnip.Ocr", "OcrEngine.cs"));

        Assert.Contains("lock (_lock)", source);
        Assert.Contains("public Task<OcrResult> RecognizeAsync", source);
        Assert.Contains("public void Unload()", source);
    }

    [Fact]
    public void ResultWindowCopyFailure_IsVisible()
    {
        var source = File.ReadAllText(GetRepoPath("src", "OcrSnip.App", "Clipboard", "ResultWindow.cs"));

        Assert.Contains("Copy failed:", source);
        Assert.Contains("box.SelectAll()", source);
    }

    [Fact]
    public void ShipReadiness_DoesNotAutoSatisfyExternalGatesFromLocalReports()
    {
        var source = File.ReadAllText(GetRepoPath("tools", "verify-ship-readiness.ps1"));

        Assert.DoesNotContain("Get-CurrentMachineEvidence", source);
        Assert.DoesNotContain("compatibility-report.txt shows", source);
        Assert.DoesNotContain("theme-validation.txt shows", source);
    }

    private static string GetRepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OcrSnip.slnx")))
            {
                return Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root.");
    }
}
