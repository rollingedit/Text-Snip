using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using OcrSnip.App.Clipboard;
using OcrSnip.App.Hotkeys;
using OcrSnip.App.Settings;
using OcrSnip.App.Tray;
using OcrSnip.Ocr;

namespace OcrSnip.App;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private TrayIconService? _trayIcon;
    private HotkeyService? _hotkeyService;
    private SnipWorkflow? _workflow;
    private OcrEngine? _ocrEngine;

    protected override void OnStartup(StartupEventArgs e)
    {
        DpiAwareness.TryEnablePerMonitorV2();
        base.OnStartup(e);

        if (SelfTestCommand.TryRun(e.Args, Shutdown))
        {
            return;
        }

        _mutex = new Mutex(true, "Global\\OcrSnip_1B2F1F57_13E7_4F40_9E7D_Resident", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();
        StartupRegistration.Apply(settings.LaunchAtLogin);
        _ocrEngine = new OcrEngine(ModelPaths.FromAppBaseDirectory(AppContext.BaseDirectory));
        _workflow = new SnipWorkflow(settingsStore, settings, _ocrEngine);
        _hotkeyService = new HotkeyService(settings.Hotkey, () => _ = _workflow.StartSnipAsync());
        _trayIcon = new TrayIconService(_workflow, () => Shutdown());
        _trayIcon.Show();

        if (!_hotkeyService.TryRegister())
        {
            _workflow.ShowHotkeyConflict();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _hotkeyService?.Dispose();
        _ocrEngine?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

file static class SelfTestCommand
{
    public static bool TryRun(string[] args, Action shutdown)
    {
        if (args.Length == 0)
        {
            return false;
        }

        _ = RunAsync(args, shutdown);
        return true;
    }

    private static async Task RunAsync(string[] args, Action shutdown)
    {
        try
        {
            switch (args[0])
            {
                case "--self-test-ocr":
                    await RunOcrClipboardTestAsync(args).ConfigureAwait(true);
                    break;
                case "--self-test-startup":
                    RunStartupTest();
                    break;
            }
        }
        finally
        {
            shutdown();
        }
    }

    private static async Task RunOcrClipboardTestAsync(string[] args)
    {
        if (args.Length < 2 || !File.Exists(args[1]))
        {
            Environment.ExitCode = 2;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(args[1]));
        bitmap.EndInit();
        bitmap.Freeze();

        using var engine = new OcrEngine(ModelPaths.FromAppBaseDirectory(AppContext.BaseDirectory));
        var result = await engine.RecognizeAsync(bitmap, CancellationToken.None).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(result.Text))
        {
            Environment.ExitCode = 3;
            return;
        }

        Environment.ExitCode = ClipboardService.TrySetText(result.Text, out _) ? 0 : 4;
    }

    private static void RunStartupTest()
    {
        StartupRegistration.Apply(true);
        if (!StartupRegistration.IsRegistered(Environment.ProcessPath))
        {
            Environment.ExitCode = 5;
            return;
        }

        StartupRegistration.Apply(false);
        Environment.ExitCode = StartupRegistration.IsRegistered(Environment.ProcessPath) ? 6 : 0;
    }
}

file static class DpiAwareness
{
    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    public static void TryEnablePerMonitorV2()
    {
        try
        {
            SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        }
        catch
        {
            // Manifest DPI awareness is the primary path; this is best effort for older hosts.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
}
