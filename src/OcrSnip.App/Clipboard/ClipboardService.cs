using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Text;

namespace OcrSnip.App.Clipboard;

public static class ClipboardService
{
    private static readonly Lazy<HwndSource> ClipboardOwner = new(CreateClipboardOwner);

    public static bool TrySetText(string text, out Exception? error)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var previousText = TryReadExistingText();
            if (TrySetUnicodeTextNative(text, out error, out var clipboardChanged))
            {
                return true;
            }

            lastError = error;
            if (clipboardChanged && previousText is not null)
            {
                TrySetUnicodeTextNative(previousText, out _, out _);
            }

            try
            {
                System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText);
                System.Windows.Clipboard.Flush();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            Thread.Sleep(50);
        }

        error = lastError ?? new InvalidOperationException("Clipboard busy.");
        return false;
    }

    private static string? TryReadExistingText()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText)
                ? System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySetUnicodeTextNative(string text, out Exception? error, out bool clipboardChanged)
    {
        nint handle = 0;
        nint locked = 0;
        clipboardChanged = false;
        try
        {
            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            handle = NativeMethods.GlobalAlloc(NativeMethods.GmemMoveable | NativeMethods.GmemZeroInit, (nuint)bytes.Length);
            if (handle == 0)
            {
                error = new InvalidOperationException("GlobalAlloc failed.");
                return false;
            }

            locked = NativeMethods.GlobalLock(handle);
            if (locked == 0)
            {
                error = new InvalidOperationException("GlobalLock failed.");
                return false;
            }

            Marshal.Copy(bytes, 0, locked, bytes.Length);
            NativeMethods.GlobalUnlock(handle);
            locked = 0;

            if (!NativeMethods.OpenClipboard(ClipboardOwner.Value.Handle))
            {
                error = new InvalidOperationException("OpenClipboard failed.");
                return false;
            }

            try
            {
                if (!NativeMethods.EmptyClipboard())
                {
                    error = new InvalidOperationException("EmptyClipboard failed.");
                    return false;
                }

                clipboardChanged = true;
                if (NativeMethods.SetClipboardData(NativeMethods.CfUnicodeText, handle) == 0)
                {
                    error = new InvalidOperationException("SetClipboardData failed.");
                    return false;
                }

                handle = 0;
                error = null;
                return true;
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
        finally
        {
            if (locked != 0)
            {
                NativeMethods.GlobalUnlock(handle);
            }

            if (handle != 0)
            {
                NativeMethods.GlobalFree(handle);
            }
        }
    }

    private static HwndSource CreateClipboardOwner()
    {
        var parameters = new HwndSourceParameters("TextSnipClipboardOwner")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        };
        return new HwndSource(parameters);
    }
}

file static partial class NativeMethods
{
    public const uint CfUnicodeText = 13;
    public const uint GmemMoveable = 0x0002;
    public const uint GmemZeroInit = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalUnlock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint GlobalFree(nint hMem);
}
