using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace OcrSnip.App.Toasts;

public sealed class ToastWindow : Window
{
    private ToastWindow(string message)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        Content = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 26, 26, 26)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 9, 14, 9),
            Child = new TextBlock { Text = message, Foreground = System.Windows.Media.Brushes.White }
        };
        Loaded += (_, _) =>
        {
            Left = SystemParameters.WorkArea.Right - ActualWidth - 24;
            Top = SystemParameters.WorkArea.Bottom - ActualHeight - 24;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.7) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Close();
            };
            timer.Start();
        };
    }

    public static void ShowMessage(string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var toast = new ToastWindow(message);
            toast.Show();
        });
    }
}
