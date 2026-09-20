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
    /// Gets or sets the target monitor EDID/hardware identifier for the Desk profile.
    /// </summary>
    public string DeskMonitorId { get; set; } = "MSI4DD0";

    /// <summary>
    /// Gets or sets the target monitor EDID/hardware identifier for the Sim Rig profile.
    /// </summary>
    public string RigMonitorId { get; set; } = "AUS3438";

    /// <summary>
    /// Gets or sets the primary audio playback endpoint GUID identifier for the Desk profile.
    /// </summary>
    public string DeskPrimaryAudioId { get; set; } = "{D2B56B79-F353-4FB0-81B6-ECEF8E95E57A}";

    /// <summary>
    /// Gets or sets the fallback audio playback endpoint GUID identifier for the Desk profile.
    /// </summary>
    public string DeskFallbackAudioId { get; set; } = "{EE0329B0-FA5C-4731-B0F1-8E188AB441DC}";

    /// <summary>
    /// Gets or sets the primary audio playback endpoint GUID identifier for the Sim Rig profile.
    /// </summary>
    public string RigPrimaryAudioId { get; set; } = "{3CF792EA-E074-4D66-A8A1-9C4F1A8B204F}";

    /// <summary>
    /// Gets or sets user-defined friendly name overrides keyed by device identifier.
    /// </summary>
    public Dictionary<string, string> CustomDeviceNames { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of audio endpoint GUID identifiers to hide or disable.
    /// </summary>
    public List<string> HiddenAudioEndpointIds { get; set; } =
    [
        "{4C84EB15-46E9-47BE-BC9B-BB93C43FFCA8}", // SteelSeries Sonar - Gaming
        "{E32080F3-4D16-48D8-934B-13ED01E7D0E6}", // SteelSeries Sonar - Chat
        "{5EB8AEAD-C887-4D10-8816-CCB6878A3F40}", // SteelSeries Sonar - Media
        "{E4620821-5066-4BE1-A337-5A303AA1950A}", // SteelSeries Sonar - Aux
        "{B95DCEBE-55B7-4BB6-BF9F-EEEA0C634D12}", // SteelSeries Sonar - Microphone
        "{AF3E716C-A16E-49C0-80DD-9E474E054079}", // Headphones (Oculus Virtual Audio Device)
        "{21F218E9-3A8C-4783-B529-D45EABD58BAB}"  // Speakers (Steam Streaming Speakers)
    ];

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
