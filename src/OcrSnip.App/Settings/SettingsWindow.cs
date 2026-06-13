using System.Windows;
using System.Windows.Controls;

namespace OcrSnip.App.Settings;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(SettingsStore store, AppSettings settings, Func<HotkeyDefinition, bool>? applyHotkey = null)
    {
        Title = "Text Snip Settings";
        Width = 360;
        Height = 400;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Hotkey" });
        var hotkey = new System.Windows.Controls.TextBox { Text = settings.Hotkey.ToString(), Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(hotkey);
        panel.Children.Add(new TextBlock { Text = "Memory mode" });
        var memory = new System.Windows.Controls.ComboBox { ItemsSource = Enum.GetValues<MemoryMode>(), SelectedItem = settings.MemoryMode, Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(memory);
        panel.Children.Add(new TextBlock { Text = "Small text boost" });
        var boost = new System.Windows.Controls.ComboBox { ItemsSource = Enum.GetValues<SmallTextBoost>(), SelectedItem = settings.SmallTextBoost, Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(boost);
        panel.Children.Add(new TextBlock { Text = "Copy mode" });
        var copyMode = new System.Windows.Controls.ComboBox { ItemsSource = Enum.GetValues<CopyMode>(), SelectedItem = settings.CopyMode, Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(copyMode);
        var toast = new System.Windows.Controls.CheckBox { Content = "Show toast", IsChecked = settings.ToastEnabled, Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(toast);
        var launchAtLogin = new System.Windows.Controls.CheckBox { Content = "Launch at startup", IsChecked = settings.LaunchAtLogin, Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(launchAtLogin);
        var status = new TextBlock { Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(status);
        var save = new System.Windows.Controls.Button { Content = "Save", Height = 32, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, MinWidth = 96 };
        save.Click += (_, _) =>
        {
            if (!HotkeyDefinition.TryParse(hotkey.Text, out var parsedHotkey))
            {
                status.Text = "Hotkey must look like Win+Shift+O.";
                return;
            }

            if (parsedHotkey != settings.Hotkey && applyHotkey is not null && !applyHotkey(parsedHotkey))
            {
                status.Text = $"Hotkey already in use: {parsedHotkey}";
                return;
            }

            settings.Hotkey = parsedHotkey;
            settings.MemoryMode = (MemoryMode)memory.SelectedItem;
            settings.SmallTextBoost = (SmallTextBoost)boost.SelectedItem;
            settings.CopyMode = (CopyMode)copyMode.SelectedItem;
            settings.ToastEnabled = toast.IsChecked == true;
            settings.LaunchAtLogin = launchAtLogin.IsChecked == true;
            store.Save(settings);
            StartupRegistration.Apply(settings.LaunchAtLogin);
            Close();
        };
        panel.Children.Add(save);
        Content = panel;
    }
}
