namespace RigSwitch.Core.Interfaces;

/// <summary>
/// Provides registration and dispatching services for global system hotkeys.
/// </summary>
public interface IGlobalHotkeyService
{
    /// <summary>
    /// Registers a global system hotkey combination and binds it to a callback action.
    /// </summary>
    /// <param name="hotkeyString">The textual representation of the hotkey combination (e.g., "Ctrl+Alt+S").</param>
    /// <param name="callback">The callback action to execute when the hotkey is pressed.</param>
    /// <returns><c>true</c> if the hotkey was successfully registered; otherwise, <c>false</c>.</returns>
    bool RegisterHotkey(string hotkeyString, Action callback);

    /// <summary>
    /// Unregisters a previously registered global hotkey combination.
    /// </summary>
    /// <param name="hotkeyString">The textual representation of the hotkey combination to unregister.</param>
    void UnregisterHotkey(string hotkeyString);

    /// <summary>
    /// Unregisters all currently registered global hotkeys.
    /// </summary>
    void UnregisterAll();
}
