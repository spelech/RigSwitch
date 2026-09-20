namespace RigSwitch.Core.Models;

using RigSwitch.Core.Enums;

/// <summary>
/// Represents persisted user configuration and device mappings for RigSwitch.
/// </summary>
public sealed record UserSettings
{
    /// <summary>
    /// Gets or sets the workstation profile that was last activated.
    /// </summary>
    public ProfileMode LastActiveProfile { get; set; } = ProfileMode.Desk;

    /// <summary>
    /// Gets or sets the target monitor EDID or hardware identifier for the Desk profile.
    /// </summary>
    public string DeskMonitorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target monitor EDID or hardware identifier for the Sim Rig profile.
    /// </summary>
    public string RigMonitorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary audio playback endpoint GUID identifier for the Desk profile.
    /// </summary>
    public string DeskPrimaryAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fallback audio playback endpoint GUID identifier for the Desk profile.
    /// </summary>
    public string DeskFallbackAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary audio playback endpoint GUID identifier for the Sim Rig profile.
    /// </summary>
    public string RigPrimaryAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets user-defined friendly name overrides keyed by device identifier.
    /// </summary>
    public Dictionary<string, string> CustomDeviceNames { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of audio endpoint GUID identifiers to hide or disable.
    /// </summary>
    public List<string> HiddenAudioEndpointIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the hotkey combination used to toggle between Desk and Sim Rig profiles.
    /// </summary>
    public string ToggleHotkey { get; set; } = "Ctrl+Alt+S";

    /// <summary>
    /// Gets or sets the hotkey combination used to directly activate the Desk profile.
    /// </summary>
    public string DeskHotkey { get; set; } = "Ctrl+Alt+D";

    /// <summary>
    /// Gets or sets the hotkey combination used to directly activate the Sim Rig profile.
    /// </summary>
    public string RigHotkey { get; set; } = "Ctrl+Alt+R";

    /// <summary>
    /// Gets or sets a value indicating whether the application starts minimized to the Windows system tray.
    /// </summary>
    public bool StartMinimizedToTray { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Windows toast notifications are displayed on profile changes.
    /// </summary>
    public bool ShowToastNotifications { get; set; } = true;
}
