namespace OcrSnip.App.Settings;

public sealed class AppSettings
{
    public HotkeyDefinition Hotkey { get; set; } = HotkeyDefinition.Default;
    public MemoryMode MemoryMode { get; set; } = MemoryMode.Balanced;
    public SmallTextBoost SmallTextBoost { get; set; } = SmallTextBoost.Auto;
    public CopyMode CopyMode { get; set; } = CopyMode.Raw;
    public bool ToastEnabled { get; set; } = true;
    public bool LaunchAtLogin { get; set; } = true;
}

public enum MemoryMode
{
    LowMemory,
    Balanced,
    Performance
}

public enum SmallTextBoost
{
    Auto,
    Off,
    Scale150,
    Scale200,
    Scale300
}

public enum CopyMode
{
    Raw,
    Smart,
    Code
}
