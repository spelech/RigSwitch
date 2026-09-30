namespace RigSwitch.Core.Models;

using RigSwitch.Core.Enums;

/// <summary>
/// Represents a customizable hardware and software configuration preset within a workstation profile.
/// </summary>
public sealed record WorkstationPreset
{
    /// <summary>
    /// Gets the unique identifier for this preset.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the user-friendly display name of this preset.
    /// </summary>
    public string Name { get; set; } = "Default Preset";

    private List<string> _targetMonitorIds = [];

    /// <summary>
    /// Gets or sets the list of target monitor hardware identifiers or EDIDs assigned to this preset.
    /// </summary>
    public List<string> TargetMonitorIds
    {
        get => _targetMonitorIds;
        set => _targetMonitorIds = value ?? [];
    }

    /// <summary>
    /// Gets or sets the primary target monitor hardware identifier or EDID for backwards compatibility.
    /// </summary>
    public string TargetMonitorId
    {
        get => _targetMonitorIds.Count > 0 ? _targetMonitorIds[0] : string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            int existingIndex = _targetMonitorIds.FindIndex(id => string.Equals(id, value, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                _targetMonitorIds.RemoveAt(existingIndex);
                _targetMonitorIds.Insert(0, value);
            }
            else if (_targetMonitorIds.Count > 0)
            {
                _targetMonitorIds[0] = value;
            }
            else
            {
                _targetMonitorIds.Add(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the primary audio endpoint device identifier.
    /// </summary>
    public string PrimaryAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fallback audio endpoint device identifier.
    /// </summary>
    public string FallbackAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary microphone / capture endpoint device identifier.
    /// </summary>
    public string PrimaryMicrophoneId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fallback microphone / capture endpoint device identifier.
    /// </summary>
    public string FallbackMicrophoneId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target playback audio volume and mute settings for this preset.
    /// </summary>
    public AudioVolumeSettings PlaybackVolume { get; set; } = new();

    /// <summary>
    /// Gets or sets the target microphone input volume and mute settings for this preset.
    /// </summary>
    public AudioVolumeSettings MicrophoneVolume { get; set; } = new();

    /// <summary>
    /// Gets or sets the native Windows HDR (Advanced Color) switching mode for this preset.
    /// </summary>
    public PresetHdrMode HdrMode { get; set; } = PresetHdrMode.Retain;

    /// <summary>
    /// Gets or sets the direct hotkey combination used to activate this preset.
    /// </summary>
    public string DirectHotkey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets application file paths to launch upon activating this preset.
    /// </summary>
    public List<string> LaunchApplicationPaths { get; set; } = [];

    /// <summary>
    /// Gets or sets application lifecycle hooks configured for this preset.
    /// </summary>
    public List<PresetApplicationHook> ApplicationHooks { get; set; } = [];
}

