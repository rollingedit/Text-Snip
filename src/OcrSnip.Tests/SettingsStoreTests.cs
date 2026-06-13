using OcrSnip.App.Settings;

namespace OcrSnip.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OcrSnipTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        var store = new SettingsStore(path);
        var settings = new AppSettings
        {
            LaunchAtLogin = false,
            ToastEnabled = false,
            MemoryMode = MemoryMode.LowMemory,
            SmallTextBoost = SmallTextBoost.Scale200,
            CopyMode = CopyMode.Code
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.False(loaded.LaunchAtLogin);
        Assert.False(loaded.ToastEnabled);
        Assert.Equal(MemoryMode.LowMemory, loaded.MemoryMode);
        Assert.Equal(SmallTextBoost.Scale200, loaded.SmallTextBoost);
        Assert.Equal(CopyMode.Code, loaded.CopyMode);
    }

    [Fact]
    public void Exists_TracksSettingsFilePresence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OcrSnipTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        var store = new SettingsStore(path);

        Assert.False(store.Exists);

        store.Save(new AppSettings());

        Assert.True(store.Exists);
    }

    [Fact]
    public void Load_MigratesLegacyDefaultHotkeyToWinShiftO()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OcrSnipTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, """
            {
              "Hotkey": {
                "modifiers": 6,
                "key": 79
              },
              "MemoryMode": 1,
              "SmallTextBoost": 0,
              "CopyMode": 0,
              "ToastEnabled": true,
              "LaunchAtLogin": true
            }
            """);
        var store = new SettingsStore(path);

        var loaded = store.Load();

        Assert.Equal(HotkeyModifiers.Windows | HotkeyModifiers.Shift, loaded.Hotkey.Modifiers);
        Assert.Equal('O', loaded.Hotkey.Key);
    }
}
