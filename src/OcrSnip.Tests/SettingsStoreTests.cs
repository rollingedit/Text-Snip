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
}
