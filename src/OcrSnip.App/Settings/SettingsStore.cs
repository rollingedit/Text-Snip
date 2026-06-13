using System.IO;
using System.Text.Json;
using OcrSnip.App;

namespace OcrSnip.App.Settings;

public sealed class SettingsStore
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsStore(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OcrSnip",
            "settings.json");
    }

    public string SettingsPath { get; }

    public bool Exists => File.Exists(SettingsPath);

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), _jsonOptions) ?? new AppSettings();
            MigrateLegacyDefaultHotkey(settings);
            return settings;
        }
        catch (Exception ex)
        {
            PreserveCorruptSettings(ex);
            return new AppSettings();
        }
    }

    private void PreserveCorruptSettings(Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            var backup = Path.Combine(directory, $"settings.corrupt.{DateTimeOffset.Now:yyyyMMddHHmmss}.json");
            File.Move(SettingsPath, backup, overwrite: false);
            AppDiagnostics.LogException("Settings file was corrupt and was preserved.", exception);
        }
        catch
        {
            // Settings recovery must never block startup.
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, _jsonOptions));
    }

    private static void MigrateLegacyDefaultHotkey(AppSettings settings)
    {
        if (settings.Hotkey.Modifiers == (HotkeyModifiers.Control | HotkeyModifiers.Shift) &&
            settings.Hotkey.Key == 'O')
        {
            settings.Hotkey = HotkeyDefinition.Default;
        }
    }
}
