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
                case "--self-test-hotkey":
                    await RunHotkeyTestAsync().ConfigureAwait(true);
                    break;
                case "--self-test-hotkey-listener":
                    await RunHotkeyListenerTestAsync().ConfigureAwait(true);
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

    private static async Task RunHotkeyTestAsync()
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var hotkey = new HotkeyService(HotkeyDefinition.Default, () => received.TrySetResult());
        if (!hotkey.TryRegister())
        {
            Environment.ExitCode = 7;
            return;
        }

        HotkeySelfTestInput.SendCtrlShiftO();
        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(true);
        Environment.ExitCode = completed == received.Task ? 0 : 8;
    }

    private static async Task RunHotkeyListenerTestAsync()
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var hotkey = new HotkeyService(HotkeyDefinition.Default, () => received.TrySetResult());
        if (!hotkey.TryRegister())
        {
            Environment.ExitCode = 10;
            return;
        }

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
        if (completed != received.Task)
        {
            Environment.ExitCode = 11;
            return;
        }

        Environment.ExitCode = ClipboardService.TrySetText("OCR_SNIP_HOTKEY_OK", out _) ? 0 : 12;
    }
}

file static class HotkeySelfTestInput
{
    public static void SendCtrlShiftO()
    {
        var inputs = new Input[6];
        inputs[0].Type = 1;
        inputs[0].Union.Keyboard.VirtualKey = 0x11;
        inputs[1].Type = 1;
        inputs[1].Union.Keyboard.VirtualKey = 0x10;
        inputs[2].Type = 1;
        inputs[2].Union.Keyboard.VirtualKey = 0x4F;
        inputs[3].Type = 1;
        inputs[3].Union.Keyboard.VirtualKey = 0x4F;
        inputs[3].Union.Keyboard.Flags = 0x0002;
        inputs[4].Type = 1;
        inputs[4].Union.Keyboard.VirtualKey = 0x10;
        inputs[4].Union.Keyboard.Flags = 0x0002;
        inputs[5].Type = 1;
        inputs[5].Union.Keyboard.VirtualKey = 0x11;
        inputs[5].Union.Keyboard.Flags = 0x0002;

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            Environment.ExitCode = 9;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParamLow;
        public ushort ParamHigh;
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
