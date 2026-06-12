using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
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
