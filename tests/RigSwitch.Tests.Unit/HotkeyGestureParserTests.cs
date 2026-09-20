namespace RigSwitch.Tests.Unit;

using RigSwitch.Core.Services;
using Xunit;

public sealed class HotkeyGestureParserTests
{
    [Theory]
    [InlineData(true, true, false, false, 0x53, "Ctrl+Alt+S")]
    [InlineData(true, false, true, false, 0x31, "Ctrl+Shift+1")]
    [InlineData(false, true, false, false, 0x7B, "Alt+F12")]
    [InlineData(false, false, true, true, 0x70, "Shift+Win+F1")]
    [InlineData(true, true, true, true, 0x41, "Ctrl+Alt+Shift+Win+A")]
    [InlineData(true, false, false, false, 0x20, "Ctrl+Space")]
    [InlineData(false, true, false, false, 0x0D, "Alt+Enter")]
    [InlineData(true, true, false, false, 0x2E, "Ctrl+Alt+Delete")]
    [InlineData(true, false, false, false, 0x65, "Ctrl+NumPad5")]
    [InlineData(true, false, false, false, 0x6B, "Ctrl+Add")]
    [InlineData(true, false, false, false, 0xBB, "Ctrl++")]
    public void FormatHotkey_ValidModifiersAndKey_ReturnsCanonicalString(
        bool ctrl,
        bool alt,
        bool shift,
        bool win,
        int virtualKey,
        string expected)
    {
        string? result = HotkeyGestureParser.FormatHotkey(ctrl, alt, shift, win, virtualKey);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(false, false, false, false, 0x53)] // No modifiers
    [InlineData(true, false, false, false, 0x11)]  // VK_CONTROL
    [InlineData(false, true, false, false, 0x12)]  // VK_MENU (Alt)
    [InlineData(false, false, true, false, 0x10)]  // VK_SHIFT
    [InlineData(false, false, false, true, 0x5B)]  // VK_LWIN
    [InlineData(false, false, false, true, 0x5C)]  // VK_RWIN
    [InlineData(true, false, false, false, 0xA2)]  // VK_LCONTROL
    [InlineData(true, false, false, false, 0xA3)]  // VK_RCONTROL
    [InlineData(false, true, false, false, 0xA4)]  // VK_LMENU
    [InlineData(false, true, false, false, 0xA5)]  // VK_RMENU
    [InlineData(false, false, true, false, 0xA0)]  // VK_LSHIFT
    [InlineData(false, false, true, false, 0xA1)]  // VK_RSHIFT
    [InlineData(true, false, false, false, 0)]     // Invalid VK (0)
    [InlineData(true, true, false, false, -1)]    // Negative VK
    public void FormatHotkey_InvalidInputs_ReturnsNull(
        bool ctrl,
        bool alt,
        bool shift,
        bool win,
        int virtualKey)
    {
        string? result = HotkeyGestureParser.FormatHotkey(ctrl, alt, shift, win, virtualKey);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("Ctrl+Alt+S")]
    [InlineData("Ctrl+Shift+1")]
    [InlineData("Alt+F12")]
    [InlineData("Shift+Win+F1")]
    [InlineData("Ctrl+Space")]
    [InlineData("Ctrl+Alt+Delete")]
    [InlineData("Ctrl+NumPad5")]
    [InlineData("Ctrl++")]
    [InlineData("ctrl+alt+s")]
    [InlineData("ctrl + alt + s")]
    [InlineData("ALT+CTRL+S")]
    [InlineData("control+menu+s")]
    [InlineData("windows+shift+f1")]
    public void IsValidHotkey_ValidCombinations_ReturnsTrue(string hotkey)
    {
        Assert.True(HotkeyGestureParser.IsValidHotkey(hotkey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]
    [InlineData("Alt")]
    [InlineData("Shift")]
    [InlineData("Win")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Alt+Shift")]
    [InlineData("A")]
    [InlineData("F12")]
    [InlineData("1")]
    [InlineData("Space")]
    [InlineData("Ctrl+Alt+S+D")]
    [InlineData("Ctrl+InvalidKeyName")]
    [InlineData("Ctrl+")]
    public void IsValidHotkey_InvalidCombinations_ReturnsFalse(string? hotkey)
    {
        Assert.False(HotkeyGestureParser.IsValidHotkey(hotkey));
    }

    [Theory]
    [InlineData("ctrl+alt+s", "Ctrl+Alt+S")]
    [InlineData("ctrl + alt + s", "Ctrl+Alt+S")]
    [InlineData("alt+ctrl+s", "Ctrl+Alt+S")]
    [InlineData("control+menu+s", "Ctrl+Alt+S")]
    [InlineData("ctrl+shift+1", "Ctrl+Shift+1")]
    [InlineData("alt+f12", "Alt+F12")]
    [InlineData("win+shift+f1", "Shift+Win+F1")]
    [InlineData("super+alt+d", "Alt+Win+D")]
    [InlineData("ctrl+numpad5", "Ctrl+NumPad5")]
    [InlineData("ctrl + +", "Ctrl++")]
    [InlineData("ctrl++", "Ctrl++")]
    [InlineData("ctrl+alt+del", "Ctrl+Alt+Delete")]
    [InlineData("ctrl+esc", "Ctrl+Escape")]
    [InlineData("alt+space", "Alt+Space")]
    public void NormalizeHotkey_ValidRawCombinations_ReturnsCanonicalForm(string raw, string expected)
    {
        string result = HotkeyGestureParser.NormalizeHotkey(raw);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]
    [InlineData("A")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+NonExistentKey")]
    public void NormalizeHotkey_InvalidInputs_ThrowsArgumentException(string? raw)
    {
        Assert.ThrowsAny<ArgumentException>(() => HotkeyGestureParser.NormalizeHotkey(raw!));
    }
}
