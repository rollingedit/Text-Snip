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
        _workflow = new SnipWorkflow(settingsStore, settings, _ocrEngine, ValidationSelection.TryParse(e.Args));
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

file static class ValidationSelection
{
    public static ISnipSelectionService? TryParse(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!args[index].Equals("--validation-selection", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = args[index + 1].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 4 ||
                !int.TryParse(parts[0], out var x) ||
                !int.TryParse(parts[1], out var y) ||
                !int.TryParse(parts[2], out var width) ||
                !int.TryParse(parts[3], out var height) ||
                width <= 0 ||
                height <= 0)
            {
                return null;
            }

            return new FixedSelectionService(new Int32Rect(x, y, width, height));
        }

        return null;
    }
}

file static class SelfTestCommand
{
    public static bool TryRun(string[] args, Action shutdown)
    {
        if (args.Length == 0 || !IsSelfTestCommand(args[0]))
        {
            return false;
        }

        _ = RunAsync(args, shutdown);
        return true;
    }

    private static bool IsSelfTestCommand(string command)
    {
        return command is "--self-test-ocr" or "--self-test-startup" or "--self-test-hotkey" or "--self-test-hotkey-listener";
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
            Environment.Exit(Environment.ExitCode);
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

        var focusWindow = CreateHotkeySelfTestFocusWindow();
        try
        {
            focusWindow.Show();
            focusWindow.Activate();
            await Task.Delay(TimeSpan.FromMilliseconds(150)).ConfigureAwait(true);

            for (var attempt = 0; attempt < 5 && !received.Task.IsCompleted; attempt++)
            {
                HotkeySelfTestInput.SendCtrlShiftO();
                await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromMilliseconds(500))).ConfigureAwait(true);
            }

            Environment.ExitCode = received.Task.IsCompleted ? 0 : 8;
        }
        finally
        {
            focusWindow.Close();
        }
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

    private static Window CreateHotkeySelfTestFocusWindow()
    {
        return new Window
        {
            Width = 1,
            Height = 1,
            Left = SystemParameters.VirtualScreenLeft,
            Top = SystemParameters.VirtualScreenTop,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Topmost = true,
            Opacity = 0.01
        };
    }
}

file static class HotkeySelfTestInput
{
    public static void SendCtrlShiftO()
    {
        var inputs = new Input[12];
        inputs[0] = Key(0x12, keyUp: true);
        inputs[1] = Key(0x10, keyUp: true);
        inputs[2] = Key(0x11, keyUp: true);
        inputs[3] = Key(0x11);
        inputs[4] = Key(0x10);
        inputs[5] = Key(0x4F);
        inputs[6] = Key(0x4F, keyUp: true);
        inputs[7] = Key(0x10, keyUp: true);
        inputs[8] = Key(0x11, keyUp: true);
        inputs[9] = Key(0x12, keyUp: true);
        inputs[10] = Key(0x10, keyUp: true);
        inputs[11] = Key(0x11, keyUp: true);

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            Environment.ExitCode = 9;
        }
    }

    private static Input Key(ushort virtualKey, bool keyUp = false)
    {
        var input = new Input { Type = 1 };
        input.Union.Keyboard.VirtualKey = virtualKey;
        input.Union.Keyboard.Flags = keyUp ? 0x0002u : 0;
        return input;
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
