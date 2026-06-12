using System.Runtime.InteropServices;
using System.Windows.Interop;
using OcrSnip.App.Settings;

namespace OcrSnip.App.Hotkeys;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 100;
    private readonly Action _callback;
    private readonly HotkeyDefinition _definition;
    private readonly HwndSource _source;
    private bool _registered;

    public HotkeyService(HotkeyDefinition definition, Action callback)
    {
        _definition = definition;
        _callback = callback;
        var parameters = new HwndSourceParameters("OcrSnipHotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public bool TryRegister()
    {
        _registered = RegisterHotKey(_source.Handle, HotkeyId, (uint)_definition.Modifiers, (uint)_definition.Key);
        return _registered;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _callback();
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
