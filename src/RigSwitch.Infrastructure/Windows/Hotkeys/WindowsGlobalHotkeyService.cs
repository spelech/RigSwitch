namespace RigSwitch.Infrastructure.Windows.Hotkeys;

using System.Collections.Concurrent;
using RigSwitch.Core.Interfaces;

/// <summary>
/// Implements <see cref="IGlobalHotkeyService"/> backed by an <see cref="INativeHotkeyProvider"/>.
/// </summary>
public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private static readonly Dictionary<string, uint> NamedKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Return"] = 0x0D,
        ["Esc"] = 0x1B,
        ["Escape"] = 0x1B,
        ["Backspace"] = 0x08,
        ["Back"] = 0x08,
        ["Delete"] = 0x2E,
        ["Del"] = 0x2E,
        ["Insert"] = 0x2D,
        ["Ins"] = 0x2D,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PgUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["PgDn"] = 0x22,
        ["Up"] = 0x26,
        ["Down"] = 0x28,
        ["Left"] = 0x25,
        ["Right"] = 0x27,
        ["Pause"] = 0x13,
        ["Break"] = 0x13,
        ["CapsLock"] = 0x14,
        ["Capital"] = 0x14,
        ["NumLock"] = 0x90,
        ["ScrollLock"] = 0x91,
        ["Scroll"] = 0x91,
        ["PrintScreen"] = 0x2C,
        ["PrtScn"] = 0x2C,
        ["Snapshot"] = 0x2C,
        ["NumPad0"] = 0x60,
        ["NumPad1"] = 0x61,
        ["NumPad2"] = 0x62,
        ["NumPad3"] = 0x63,
        ["NumPad4"] = 0x64,
        ["NumPad5"] = 0x65,
        ["NumPad6"] = 0x66,
        ["NumPad7"] = 0x67,
        ["NumPad8"] = 0x68,
        ["NumPad9"] = 0x69,
        ["Multiply"] = 0x6A,
        ["Add"] = 0x6B,
        ["Separator"] = 0x6C,
        ["Subtract"] = 0x6D,
        ["Decimal"] = 0x6E,
        ["Divide"] = 0x6F
    };

    private readonly INativeHotkeyProvider _provider;
    private readonly bool _ownsProvider;
    private readonly ConcurrentDictionary<int, HotkeyRegistration> _registrations = new();
    private readonly ConcurrentDictionary<string, int> _hotkeyToIdMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    private int _nextId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsGlobalHotkeyService"/> class.
    /// </summary>
    /// <param name="provider">Optional native hotkey provider abstraction. Defaults to <see cref="WindowsNativeHotkeyProvider"/>.</param>
    public WindowsGlobalHotkeyService(INativeHotkeyProvider? provider = null)
    {
        _ownsProvider = provider == null;
        _provider = provider ?? new WindowsNativeHotkeyProvider();
        _provider.HotkeyPressed += OnHotkeyPressed;
    }

    /// <inheritdoc/>
    public bool RegisterHotkey(string hotkeyString, Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(callback);

        if (!TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
        {
            return false;
        }

        lock (_syncLock)
        {
            // If already registered under this hotkey string, unregister previous first
            if (_hotkeyToIdMap.TryGetValue(hotkeyString, out int existingId))
            {
                _provider.UnregisterHotKey(existingId);
                _registrations.TryRemove(existingId, out _);
                _hotkeyToIdMap.TryRemove(hotkeyString, out _);
            }

            int id = Interlocked.Increment(ref _nextId);
            bool success = _provider.RegisterHotKey(id, modifiers, vk);
            if (!success)
            {
                return false;
            }

            var registration = new HotkeyRegistration(id, hotkeyString, modifiers, vk, callback);
            _registrations[id] = registration;
            _hotkeyToIdMap[hotkeyString] = id;
            return true;
        }
    }

    /// <inheritdoc/>
    public void UnregisterHotkey(string hotkeyString)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return;
        }

        lock (_syncLock)
        {
            if (_hotkeyToIdMap.TryRemove(hotkeyString, out int id))
            {
                _registrations.TryRemove(id, out _);
                _provider.UnregisterHotKey(id);
            }
        }
    }

    /// <inheritdoc/>
    public void UnregisterAll()
    {
        lock (_syncLock)
        {
            foreach (int id in _registrations.Keys)
            {
                _provider.UnregisterHotKey(id);
            }

            _registrations.Clear();
            _hotkeyToIdMap.Clear();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _provider.HotkeyPressed -= OnHotkeyPressed;

        UnregisterAll();

        if (_ownsProvider)
        {
            _provider.Dispose();
        }
    }

    /// <summary>
    /// Parses a string representation of a hotkey into modifier flags and virtual key code.
    /// </summary>
    /// <param name="hotkeyString">The textual hotkey string (e.g. "Ctrl+Alt+S").</param>
    /// <param name="modifiers">The parsed modifier flags.</param>
    /// <param name="virtualKey">The parsed virtual key code.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParseHotkey(string? hotkeyString, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return false;
        }

        string trimmed = hotkeyString.Trim();
        var tokens = SplitTokens(trimmed);

        if (tokens.Count == 0)
        {
            return false;
        }

        bool hasKey = false;
        uint parsedModifiers = 0;
        uint parsedVk = 0;

        foreach (string token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (TryMapModifier(token, out uint mod))
            {
                parsedModifiers |= mod;
            }
            else
            {
                if (hasKey)
                {
                    // Only one non-modifier primary key is supported
                    return false;
                }

                if (!TryMapVirtualKey(token, out parsedVk))
                {
                    return false;
                }

                hasKey = true;
            }
        }

        if (!hasKey)
        {
            return false;
        }

        modifiers = parsedModifiers;
        virtualKey = parsedVk;
        return true;
    }

    private static List<string> SplitTokens(string input)
    {
        var tokens = new List<string>();
        int i = 0;
        int len = input.Length;

        while (i < len)
        {
            int plusIndex = input.IndexOf('+', i);
            if (plusIndex == -1)
            {
                string lastToken = input[i..].Trim();
                if (lastToken.Length > 0)
                {
                    tokens.Add(lastToken);
                }

                break;
            }

            if (plusIndex == i)
            {
                // Plus character itself as a key (e.g. "Ctrl++")
                tokens.Add("+");
                i = plusIndex + 1;
                continue;
            }

            string token = input[i..plusIndex].Trim();
            if (token.Length > 0)
            {
                tokens.Add(token);
            }

            i = plusIndex + 1;
        }

        return tokens;
    }

    private static bool TryMapModifier(string token, out uint modifier)
    {
        switch (token.ToUpperInvariant())
        {
            case "CTRL":
            case "CONTROL":
                modifier = NativeHotkeyApi.MOD_CONTROL;
                return true;

            case "ALT":
            case "MENU":
                modifier = NativeHotkeyApi.MOD_ALT;
                return true;

            case "SHIFT":
                modifier = NativeHotkeyApi.MOD_SHIFT;
                return true;

            case "WIN":
            case "WINDOWS":
            case "SUPER":
                modifier = NativeHotkeyApi.MOD_WIN;
                return true;

            case "NOREPEAT":
                modifier = NativeHotkeyApi.MOD_NOREPEAT;
                return true;

            default:
                modifier = 0;
                return false;
        }
    }

    private static bool TryMapVirtualKey(string token, out uint vk)
    {
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

            switch (c)
            {
                case ' ':
                    vk = 0x20;
                    return true;
                case '+':
                    vk = 0xBB;
                    return true;
                case ',':
                    vk = 0xBC;
                    return true;
                case '-':
                    vk = 0xBD;
                    return true;
                case '.':
                    vk = 0xBE;
                    return true;
                case '/':
                    vk = 0xBF;
                    return true;
                case ';':
                    vk = 0xBA;
                    return true;
                case '[':
                    vk = 0xDB;
                    return true;
                case '\\':
                    vk = 0xDC;
                    return true;
                case ']':
                    vk = 0xDD;
                    return true;
                case '\'':
                    vk = 0xDE;
                    return true;
                case '`':
                case '~':
                    vk = 0xC0;
                    return true;
            }
        }

        if ((token.StartsWith('F') || token.StartsWith('f')) && token.Length is >= 2 and <= 3)
        {
            if (int.TryParse(token.AsSpan(1), out int fNumber) && fNumber is >= 1 and <= 24)
            {
                vk = (uint)(0x70 + (fNumber - 1));
                return true;
            }
        }

        if (NamedKeyMap.TryGetValue(token, out uint mappedVk))
        {
            vk = mappedVk;
            return true;
        }

        vk = 0;
        return false;
    }

    private void OnHotkeyPressed(int id)
    {
        if (_registrations.TryGetValue(id, out var registration))
        {
            try
            {
                registration.Callback();
            }
            catch
            {
                // Never crash the service from subscriber callback errors
            }
        }
    }

    private sealed record HotkeyRegistration(
        int Id,
        string HotkeyString,
        uint Modifiers,
        uint VirtualKey,
        Action Callback);
}
