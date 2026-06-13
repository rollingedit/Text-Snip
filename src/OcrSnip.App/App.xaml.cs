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
    private EventWaitHandle? _showWindowEvent;
    private RegisteredWaitHandle? _showWindowRegistration;
    private TrayIconService? _trayIcon;
    private HotkeyService? _hotkeyService;
    private SnipWorkflow? _workflow;
    private OcrEngine? _ocrEngine;
    private AppSettings? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        DpiAwareness.TryEnablePerMonitorV2();
        base.OnStartup(e);

        _mutex = new Mutex(true, SingleInstanceActivation.ResidentMutexName, out var createdNew);
        if (!createdNew)
        {
            SingleInstanceActivation.SignalExistingInstance();
            Shutdown();
            return;
        }

        _showWindowEvent = SingleInstanceActivation.CreateShowWindowEvent();
        _showWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showWindowEvent,
            (_, _) => Dispatcher.Invoke(ShowStatusWindow),
            null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settingsStore = new SettingsStore();
        var isFirstRun = !settingsStore.Exists;
        var settings = settingsStore.Load();
        _settings = settings;
        if (isFirstRun)
        {
            settingsStore.Save(settings);
        }

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
        else if (!e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase))
        {
            ShowStatusWindow();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _hotkeyService?.Dispose();
        _ocrEngine?.Dispose();
        _showWindowRegistration?.Unregister(null);
        _showWindowEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void ShowStatusWindow()
    {
        if (_workflow is null || _settings is null)
        {
            return;
        }

        foreach (Window window in Windows)
        {
            if (window is OnboardingWindow)
            {
                window.Show();
                window.Activate();
                return;
            }
        }

        var onboarding = new OnboardingWindow(
            _settings,
            () => _ = _workflow.StartSnipAsync(),
            _workflow.ShowSettings);
        onboarding.Show();
        onboarding.Activate();
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
