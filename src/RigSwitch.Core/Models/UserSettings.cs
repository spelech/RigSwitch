namespace RigSwitch.Core.Models;

using RigSwitch.Core.Enums;

/// <summary>
/// Represents persisted user configuration and device mappings for RigSwitch.
/// </summary>
public sealed record UserSettings
{
    private string _deskMonitorId = string.Empty;
    private string _rigMonitorId = string.Empty;
    private string _deskPrimaryAudioId = string.Empty;
    private string _deskFallbackAudioId = string.Empty;
    private string _rigPrimaryAudioId = string.Empty;

    /// <summary>
    /// Gets or sets the workstation profile that was last activated.
    /// </summary>
    public ProfileMode LastActiveProfile { get; set; } = ProfileMode.Desk;

    /// <summary>
    /// Gets or sets the zero-based index of the currently active preset for the Desk profile.
    /// </summary>
    public int ActiveDeskPresetIndex { get; set; }

    /// <summary>
    /// Gets or sets the zero-based index of the currently active preset for the Sim Rig profile.
    /// </summary>
    public int ActiveRigPresetIndex { get; set; }

    /// <summary>
    /// Gets or sets the list of configuration presets for the Desk profile.
    /// </summary>
    public List<WorkstationPreset> DeskPresets { get; set; } =
    [
        new() { Name = "Work / Primary" },
        new() { Name = "Media / Casual" },
        new() { Name = "Clean Desk" }
    ];

    /// <summary>
    /// Gets or sets the list of configuration presets for the Sim Rig profile.
    /// </summary>
    public List<WorkstationPreset> RigPresets { get; set; } =
    [
        new() { Name = "GT3 / Circuit" },
        new() { Name = "Rally / Drift" },
        new() { Name = "Flight / Space" }
    ];

    /// <summary>
    /// Gets or sets the target monitor EDID or hardware identifier for the active Desk preset.
    /// </summary>
    public string DeskMonitorId
    {
        get => DeskPresets is { Count: > 0 } ? GetActivePreset(ProfileMode.Desk).TargetMonitorId : _deskMonitorId;
        set
        {
            _deskMonitorId = value ?? string.Empty;
            if (DeskPresets is { Count: > 0 })
            {
                GetActivePreset(ProfileMode.Desk).TargetMonitorId = _deskMonitorId;
            }
        }
    }

    /// <summary>
    /// Gets or sets the target monitor EDID or hardware identifier for the active Sim Rig preset.
    /// </summary>
    public string RigMonitorId
    {
        get => RigPresets is { Count: > 0 } ? GetActivePreset(ProfileMode.SimRig).TargetMonitorId : _rigMonitorId;
        set
        {
            _rigMonitorId = value ?? string.Empty;
            if (RigPresets is { Count: > 0 })
            {
                GetActivePreset(ProfileMode.SimRig).TargetMonitorId = _rigMonitorId;
            }
        }
    }

    /// <summary>
    /// Gets or sets the primary audio playback endpoint GUID identifier for the active Desk preset.
    /// </summary>
    public string DeskPrimaryAudioId
    {
        get => DeskPresets is { Count: > 0 } ? GetActivePreset(ProfileMode.Desk).PrimaryAudioId : _deskPrimaryAudioId;
        set
        {
            _deskPrimaryAudioId = value ?? string.Empty;
            if (DeskPresets is { Count: > 0 })
            {
                GetActivePreset(ProfileMode.Desk).PrimaryAudioId = _deskPrimaryAudioId;
            }
        }
    }

    /// <summary>
    /// Gets or sets the fallback audio playback endpoint GUID identifier for the active Desk preset.
    /// </summary>
    public string DeskFallbackAudioId
    {
        get => DeskPresets is { Count: > 0 } ? GetActivePreset(ProfileMode.Desk).FallbackAudioId : _deskFallbackAudioId;
        set
        {
            _deskFallbackAudioId = value ?? string.Empty;
            if (DeskPresets is { Count: > 0 })
            {
                GetActivePreset(ProfileMode.Desk).FallbackAudioId = _deskFallbackAudioId;
            }
        }
    }

    /// <summary>
    /// Gets or sets the primary audio playback endpoint GUID identifier for the active Sim Rig preset.
    /// </summary>
    public string RigPrimaryAudioId
    {
        get => RigPresets is { Count: > 0 } ? GetActivePreset(ProfileMode.SimRig).PrimaryAudioId : _rigPrimaryAudioId;
        set
        {
            _rigPrimaryAudioId = value ?? string.Empty;
            if (RigPresets is { Count: > 0 })
            {
                GetActivePreset(ProfileMode.SimRig).PrimaryAudioId = _rigPrimaryAudioId;
            }
        }
    }

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

    /// <summary>
    /// Gets the currently active workstation preset for the specified profile mode safely.
    /// </summary>
    /// <param name="mode">The workstation profile mode.</param>
    /// <returns>The active <see cref="WorkstationPreset"/>.</returns>
    public WorkstationPreset GetActivePreset(ProfileMode mode)
    {
        var list = mode == ProfileMode.Desk ? DeskPresets : RigPresets;
        var idx = mode == ProfileMode.Desk ? ActiveDeskPresetIndex : ActiveRigPresetIndex;

        if (list == null || list.Count == 0)
        {
            var fallback = new WorkstationPreset();
            if (list == null)
            {
                list = [fallback];
                if (mode == ProfileMode.Desk)
                {
                    DeskPresets = list;
                }
                else
                {
                    RigPresets = list;
                }
            }
            else
            {
                list.Add(fallback);
            }

            return fallback;
        }

        return (idx >= 0 && idx < list.Count) ? list[idx] : list[0];
    }
}
