namespace RigSwitch.Infrastructure.Windows.Hotkeys;

/// <summary>
/// Abstraction for registering and listening to system-level hotkey events.
/// </summary>
public interface INativeHotkeyProvider : IDisposable
{
    /// <summary>
    /// Registers a system-wide hotkey with the specified identifier, modifiers, and virtual key code.
    /// </summary>
    /// <param name="id">A unique identifier for the hotkey.</param>
    /// <param name="modifiers">Key modifier flags (e.g. MOD_CONTROL, MOD_ALT).</param>
    /// <param name="vk">The virtual key code.</param>
    /// <returns><c>true</c> if registration succeeded; otherwise, <c>false</c>.</returns>
    bool RegisterHotKey(int id, uint modifiers, uint vk);

    /// <summary>
    /// Unregisters a previously registered system-wide hotkey.
    /// </summary>
    /// <param name="id">The unique identifier of the hotkey to unregister.</param>
    /// <returns><c>true</c> if unregistration succeeded; otherwise, <c>false</c>.</returns>
    bool UnregisterHotKey(int id);

    /// <summary>
    /// Occurs when a registered hotkey is pressed.
    /// The event argument is the hotkey identifier.
    /// </summary>
    event Action<int>? HotkeyPressed;
}
