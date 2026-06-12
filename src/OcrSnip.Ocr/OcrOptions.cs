namespace OcrSnip.Ocr;

public sealed class OcrOptions
{
    public SmallTextBoostMode SmallTextBoost { get; set; } = SmallTextBoostMode.Auto;
    public OcrCopyMode CopyMode { get; set; } = OcrCopyMode.Raw;
}

public enum SmallTextBoostMode
{
    Auto,
    Off,
    Scale150,
    Scale200,
    Scale300
}

public enum OcrCopyMode
{
    Raw,
    Smart,
    Code
}
