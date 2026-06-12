using System.Windows;
using System.Windows.Controls;

namespace OcrSnip.App.Clipboard;

public sealed class ResultWindow : Window
{
    private ResultWindow(string text)
    {
        Title = "OCR Snip Result";
        Width = 600;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new DockPanel { Margin = new Thickness(12) };
        var copy = new System.Windows.Controls.Button { Content = "Copy", Height = 32, MinWidth = 96, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        DockPanel.SetDock(copy, Dock.Bottom);
        var box = new System.Windows.Controls.TextBox { Text = text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, IsReadOnly = true };
        copy.Click += (_, _) => ClipboardService.TrySetText(text, out _);
        panel.Children.Add(copy);
        panel.Children.Add(box);
        Content = panel;
    }

    public static void ShowResult(string text)
    {
        var window = new ResultWindow(text);
        window.Show();
        window.Activate();
    }
}
