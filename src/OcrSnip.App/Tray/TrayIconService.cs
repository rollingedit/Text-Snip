using System.Drawing;
using System.Windows.Forms;

namespace OcrSnip.App.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon? _icon;

    public TrayIconService(SnipWorkflow workflow, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Start snip", null, (_, _) => _ = workflow.StartSnipAsync());
        menu.Items.Add("Settings", null, (_, _) => workflow.ShowSettings());
        menu.Items.Add("About", null, (_, _) => MessageBox.Show("OCR Snip\nLocal CPU OCR snipping.", "OCR Snip"));
        menu.Items.Add("Exit", null, (_, _) => exit());

        _icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon ?? SystemIcons.Application,
            Text = "OCR Snip",
            ContextMenuStrip = menu,
            Visible = false
        };
        _notifyIcon.DoubleClick += (_, _) => _ = workflow.StartSnipAsync();
    }

    public void Show() => _notifyIcon.Visible = true;

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
    }
}
