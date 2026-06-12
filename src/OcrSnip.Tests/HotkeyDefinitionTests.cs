using OcrSnip.App.Settings;

namespace OcrSnip.Tests;

public sealed class HotkeyDefinitionTests
{
    [Fact]
    public void DefaultHotkey_IsCtrlShiftO()
    {
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, HotkeyDefinition.Default.Modifiers);
        Assert.Equal('O', HotkeyDefinition.Default.Key);
        Assert.Equal("Ctrl+Shift+O", HotkeyDefinition.Default.ToString());
    }
}
