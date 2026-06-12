using System.Windows;
using System.Windows.Controls;

namespace OcrSnip.App.Settings;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(SettingsStore store, AppSettings settings)
    {
        Title = "OCR Snip Settings";
        Width = 360;
        Height = 240;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = $"Hotkey: {settings.Hotkey}", Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "Memory mode" });
        var memory = new System.Windows.Controls.ComboBox { ItemsSource = Enum.GetValues<MemoryMode>(), SelectedItem = settings.MemoryMode, Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(memory);
        panel.Children.Add(new TextBlock { Text = "Small text boost" });
        var boost = new System.Windows.Controls.ComboBox { ItemsSource = Enum.GetValues<SmallTextBoost>(), SelectedItem = settings.SmallTextBoost, Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(boost);
        var toast = new System.Windows.Controls.CheckBox { Content = "Show toast", IsChecked = settings.ToastEnabled, Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(toast);
        var save = new System.Windows.Controls.Button { Content = "Save", Height = 32, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, MinWidth = 96 };
        save.Click += (_, _) =>
        {
            settings.MemoryMode = (MemoryMode)memory.SelectedItem;
            settings.SmallTextBoost = (SmallTextBoost)boost.SelectedItem;
            settings.ToastEnabled = toast.IsChecked == true;
            store.Save(settings);
            Close();
        };
        panel.Children.Add(save);
        Content = panel;
    }
}
