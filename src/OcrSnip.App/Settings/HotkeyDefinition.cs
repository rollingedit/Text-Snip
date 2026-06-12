using System.Text.Json.Serialization;

namespace OcrSnip.App.Settings;

public sealed record HotkeyDefinition(
    [property: JsonPropertyName("modifiers")] HotkeyModifiers Modifiers,
    [property: JsonPropertyName("key")] int Key)
{
    public static HotkeyDefinition Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'O');

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        parts.Add(((char)Key).ToString().ToUpperInvariant());
        return string.Join("+", parts);
    }
}

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004
}
