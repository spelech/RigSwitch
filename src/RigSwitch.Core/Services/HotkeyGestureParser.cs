namespace RigSwitch.Core.Services;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Provides parsing, formatting, and validation for keyboard hotkey gestures.
/// </summary>
public static class HotkeyGestureParser
{
    private static readonly Dictionary<int, string> CanonicalKeyNames = new()
    {
        [0x08] = "Backspace",
        [0x09] = "Tab",
        [0x0D] = "Enter",
        [0x13] = "Pause",
        [0x14] = "CapsLock",
        [0x1B] = "Escape",
        [0x20] = "Space",
        [0x21] = "PageUp",
        [0x22] = "PageDown",
        [0x23] = "End",
        [0x24] = "Home",
        [0x25] = "Left",
        [0x26] = "Up",
        [0x27] = "Right",
        [0x28] = "Down",
        [0x2C] = "PrintScreen",
        [0x2D] = "Insert",
        [0x2E] = "Delete",
        [0x6A] = "Multiply",
        [0x6B] = "Add",
        [0x6C] = "Separator",
        [0x6D] = "Subtract",
        [0x6E] = "Decimal",
        [0x6F] = "Divide",
        [0x90] = "NumLock",
        [0x91] = "ScrollLock",
        [0xBA] = ";",
        [0xBB] = "+",
        [0xBC] = ",",
        [0xBD] = "-",
        [0xBE] = ".",
        [0xBF] = "/",
        [0xC0] = "`",
        [0xDB] = "[",
        [0xDC] = "\\",
        [0xDD] = "]",
        [0xDE] = "'"
    };

    private static readonly Dictionary<string, int> KeyNameToVirtualKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Backspace"] = 0x08,
        ["Back"] = 0x08,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Return"] = 0x0D,
        ["Pause"] = 0x13,
        ["Break"] = 0x13,
        ["CapsLock"] = 0x14,
        ["Capital"] = 0x14,
        ["Escape"] = 0x1B,
        ["Esc"] = 0x1B,
        ["Space"] = 0x20,
        ["PageUp"] = 0x21,
        ["PgUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["PgDn"] = 0x22,
        ["End"] = 0x23,
        ["Home"] = 0x24,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["PrintScreen"] = 0x2C,
        ["PrtScn"] = 0x2C,
        ["Snapshot"] = 0x2C,
        ["Insert"] = 0x2D,
        ["Ins"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Del"] = 0x2E,
        ["Multiply"] = 0x6A,
        ["Add"] = 0x6B,
        ["Plus"] = 0xBB,
        ["+"] = 0xBB,
        ["Separator"] = 0x6C,
        ["Subtract"] = 0x6D,
        ["Minus"] = 0xBD,
        ["-"] = 0xBD,
        ["Decimal"] = 0x6E,
        ["Divide"] = 0x6F,
        ["NumLock"] = 0x90,
        ["ScrollLock"] = 0x91,
        ["Scroll"] = 0x91,
        [";"] = 0xBA,
        [","] = 0xBC,
        ["."] = 0xBE,
        ["/"] = 0xBF,
        ["`"] = 0xC0,
        ["~"] = 0xC0,
        ["["] = 0xDB,
        ["\\"] = 0xDC,
        ["]"] = 0xDD,
        ["'"] = 0xDE
    };

    /// <summary>
    /// Formats a canonical hotkey string given individual modifier flags and a virtual key code.
    /// Returns <c>null</c> if no modifier is set, or if the virtual key is invalid or a modifier key.
    /// </summary>
    public static string? FormatHotkey(bool ctrl, bool alt, bool shift, bool win, int virtualKey)
    {
        if (!ctrl && !alt && !shift && !win)
        {
            return null;
        }

        if (IsModifierVirtualKey(virtualKey))
        {
            return null;
        }

        string? keyName = GetCanonicalKeyName(virtualKey);
        if (keyName == null)
        {
            return null;
        }

        var parts = new List<string>(5);
        if (ctrl)
        {
            parts.Add("Ctrl");
        }

        if (alt)
        {
            parts.Add("Alt");
        }

        if (shift)
        {
            parts.Add("Shift");
        }

        if (win)
        {
            parts.Add("Win");
        }

        parts.Add(keyName);
        return string.Join("+", parts);
    }

    /// <summary>
    /// Checks whether the given string can be parsed into at least 1 modifier plus 1 valid primary key.
    /// </summary>
    public static bool IsValidHotkey(string? hotkeyString)
    {
        return TryParse(hotkeyString, out bool ctrl, out bool alt, out bool shift, out bool win, out int vk)
            && (ctrl || alt || shift || win)
            && vk != 0;
    }

    /// <summary>
    /// Normalizes a hotkey string into its canonical representation (e.g. "ctrl + alt + s" -> "Ctrl+Alt+S").
    /// Throws <see cref="ArgumentException"/> if the string cannot be parsed into a valid hotkey.
    /// </summary>
    public static string NormalizeHotkey(string rawHotkey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawHotkey);

        if (!TryParse(rawHotkey, out bool ctrl, out bool alt, out bool shift, out bool win, out int vk)
            || !(ctrl || alt || shift || win)
            || vk == 0)
        {
            throw new ArgumentException($"Invalid hotkey combination: '{rawHotkey}'.", nameof(rawHotkey));
        }

        return FormatHotkey(ctrl, alt, shift, win, vk)
            ?? throw new ArgumentException($"Unable to format hotkey: '{rawHotkey}'.", nameof(rawHotkey));
    }

    /// <summary>
    /// Attempts to parse a hotkey string into modifier flags and a virtual key code.
    /// </summary>
    public static bool TryParse(
        string? hotkeyString,
        out bool ctrl,
        out bool alt,
        out bool shift,
        out bool win,
        out int virtualKey)
    {
        ctrl = false;
        alt = false;
        shift = false;
        win = false;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return false;
        }

        var tokens = SplitTokens(hotkeyString);
        if (tokens.Count < 2)
        {
            return false;
        }

        bool hasKey = false;
        int parsedVk = 0;

        foreach (string token in tokens)
        {
            if (TryMapModifier(token, out bool isCtrl, out bool isAlt, out bool isShift, out bool isWin))
            {
                if (isCtrl)
                {
                    ctrl = true;
                }

                if (isAlt)
                {
                    alt = true;
                }

                if (isShift)
                {
                    shift = true;
                }

                if (isWin)
                {
                    win = true;
                }
            }
            else
            {
                if (hasKey)
                {
                    // Multiple non-modifier keys are not supported
                    return false;
                }

                if (!TryMapVirtualKey(token, out parsedVk) || IsModifierVirtualKey(parsedVk))
                {
                    return false;
                }

                hasKey = true;
            }
        }

        if (!hasKey || (!ctrl && !alt && !shift && !win))
        {
            return false;
        }

        virtualKey = parsedVk;
        return true;
    }

    /// <summary>
    /// Checks if a virtual key code corresponds to a standalone modifier key.
    /// </summary>
    public static bool IsModifierVirtualKey(int vk)
    {
        return vk switch
        {
            0x10 or 0x11 or 0x12 => true,       // VK_SHIFT, VK_CONTROL, VK_MENU
            0x5B or 0x5C => true,               // VK_LWIN, VK_RWIN
            0xA0 or 0xA1 => true,               // VK_LSHIFT, VK_RSHIFT
            0xA2 or 0xA3 => true,               // VK_LCONTROL, VK_RCONTROL
            0xA4 or 0xA5 => true,               // VK_LMENU, VK_RMENU
            _ => false
        };
    }

    private static string? GetCanonicalKeyName(int vk)
    {
        if (vk is >= 0x41 and <= 0x5A) // 'A'-'Z'
        {
            return ((char)vk).ToString();
        }

        if (vk is >= 0x30 and <= 0x39) // '0'-'9'
        {
            return ((char)vk).ToString();
        }

        if (vk is >= 0x70 and <= 0x87) // F1-F24
        {
            return $"F{vk - 0x70 + 1}";
        }

        if (vk is >= 0x60 and <= 0x69) // NumPad0-NumPad9
        {
            return $"NumPad{vk - 0x60}";
        }

        if (CanonicalKeyNames.TryGetValue(vk, out string? name))
        {
            return name;
        }

        return null;
    }

    private static bool TryMapModifier(
        string token,
        out bool isCtrl,
        out bool isAlt,
        out bool isShift,
        out bool isWin)
    {
        isCtrl = false;
        isAlt = false;
        isShift = false;
        isWin = false;

        switch (token.ToUpperInvariant())
        {
            case "CTRL":
            case "CONTROL":
                isCtrl = true;
                return true;

            case "ALT":
            case "MENU":
                isAlt = true;
                return true;

            case "SHIFT":
                isShift = true;
                return true;

            case "WIN":
            case "WINDOWS":
            case "SUPER":
                isWin = true;
                return true;

            default:
                return false;
        }
    }

    private static bool TryMapVirtualKey(string token, out int vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (token.Length == 1)
        {
            char c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                vk = c;
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                vk = c;
                return true;
            }
        }

        if ((token.StartsWith('F') || token.StartsWith('f')) && token.Length is >= 2 and <= 3)
        {
            if (int.TryParse(token.AsSpan(1), out int fNumber) && fNumber is >= 1 and <= 24)
            {
                vk = 0x70 + (fNumber - 1);
                return true;
            }
        }

        if (token.StartsWith("numpad", StringComparison.OrdinalIgnoreCase) && token.Length == 7)
        {
            if (char.IsDigit(token[6]))
            {
                vk = 0x60 + (token[6] - '0');
                return true;
            }
        }

        if (KeyNameToVirtualKey.TryGetValue(token, out int mappedVk))
        {
            vk = mappedVk;
            return true;
        }

        return false;
    }

    private static List<string> SplitTokens(string input)
    {
        var tokens = new List<string>();
        string trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            return tokens;
        }

        bool endsWithPlusKey = false;
        if (trimmed.EndsWith('+'))
        {
            int lastPlus = trimmed.Length - 1;
            int prevPlus = trimmed.LastIndexOf('+', lastPlus - 1);
            if (prevPlus >= 0 && trimmed.Substring(prevPlus + 1, lastPlus - prevPlus - 1).Trim().Length == 0)
            {
                endsWithPlusKey = true;
                trimmed = trimmed[..prevPlus].Trim();
            }
        }

        var parts = trimmed.Split('+', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            string t = part.Trim();
            if (t.Length > 0)
            {
                tokens.Add(t);
            }
        }

        if (endsWithPlusKey)
        {
            tokens.Add("+");
        }

        return tokens;
    }
}
