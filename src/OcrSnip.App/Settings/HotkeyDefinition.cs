using System.Text.Json.Serialization;

namespace OcrSnip.App.Settings;

public sealed record HotkeyDefinition(
    [property: JsonPropertyName("modifiers")] HotkeyModifiers Modifiers,
    [property: JsonPropertyName("key")] int Key)
{
    public static HotkeyDefinition Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'O');

    public static bool TryParse(string text, out HotkeyDefinition hotkey)
    {
        hotkey = Default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        int? key = null;
        foreach (var rawPart in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawPart.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                rawPart.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Control;
            }
            else if (rawPart.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Shift;
            }
            else if (rawPart.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Alt;
            }
            else if (rawPart.Length == 1 && char.IsLetterOrDigit(rawPart[0]))
            {
                key = char.ToUpperInvariant(rawPart[0]);
            }
            else
            {
                return false;
            }
        }

        if (modifiers == HotkeyModifiers.None || key is null)
        {
            return false;
        }

        hotkey = new HotkeyDefinition(modifiers, key.Value);
        return true;
    }

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
