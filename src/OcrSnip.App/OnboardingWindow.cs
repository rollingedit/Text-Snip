using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OcrSnip.App.Settings;

namespace OcrSnip.App;

public sealed class OnboardingWindow : Window
{
    public OnboardingWindow(AppSettings settings, Func<Task> startSnip, Action showSettings)
    {
        Title = "OCR Snip";
        Width = 420;
        Height = 260;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Topmost = true;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "OCR Snip is running",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var body = new TextBlock
        {
            Text = $"Press {settings.Hotkey} or use Start snip. OCR text is copied to the clipboard.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 18)
        };
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var status = new TextBlock
        {
            Text = "The tray icon stays available for settings and exit.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray
        };
        Grid.SetRow(status, 2);
        root.Children.Add(status);

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var start = new System.Windows.Controls.Button { Content = "Start snip", MinWidth = 96, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
        start.Click += async (_, _) =>
        {
            Hide();
            try
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                await startSnip().ConfigureAwait(true);
                Close();
            }
            catch (Exception ex)
            {
                Show();
                Activate();
                System.Windows.MessageBox.Show(OcrFailureDiagnostics.Format(ex), "OCR Snip failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        buttons.Children.Add(start);

        var settingsButton = new System.Windows.Controls.Button { Content = "Settings", MinWidth = 88, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
        settingsButton.Click += (_, _) => showSettings();
        buttons.Children.Add(settingsButton);

        var close = new System.Windows.Controls.Button { Content = "Keep running", MinWidth = 104, Height = 32 };
        close.Click += (_, _) => Close();
        buttons.Children.Add(close);

        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);
        Content = root;
    }
}
