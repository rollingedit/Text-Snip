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

    [Theory]
    [InlineData("Ctrl+Shift+O", HotkeyModifiers.Control | HotkeyModifiers.Shift, 'O')]
    [InlineData("control + alt + 9", HotkeyModifiers.Control | HotkeyModifiers.Alt, '9')]
    public void TryParse_AcceptsSupportedHotkeys(string text, HotkeyModifiers modifiers, int key)
    {
        Assert.True(HotkeyDefinition.TryParse(text, out var hotkey));
        Assert.Equal(modifiers, hotkey.Modifiers);
        Assert.Equal(key, hotkey.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Win+O")]
    [InlineData("Ctrl+Shift+F12")]
    public void TryParse_RejectsUnsupportedHotkeys(string text)
    {
        Assert.False(HotkeyDefinition.TryParse(text, out _));
    }
}
