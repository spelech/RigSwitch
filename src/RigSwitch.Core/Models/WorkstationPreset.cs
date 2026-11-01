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
    /// Gets or sets the spatial 2D placements for monitors enabled in this preset.
    /// </summary>
    public List<MonitorPlacement> MonitorPlacements { get; set; } = [];

    /// <summary>
    /// Gets or sets the ordered priority list of playback audio endpoint device identifiers.
    /// </summary>
    public List<string> AudioPriorityIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ordered priority list of microphone / capture endpoint device identifiers.
    /// </summary>
    public List<string> MicrophonePriorityIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the primary audio endpoint device identifier.
    /// </summary>
    public string PrimaryAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secondary audio endpoint device identifier.
    /// </summary>
    public string SecondaryAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fallback audio endpoint device identifier.
    /// </summary>
    public string FallbackAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary microphone / capture endpoint device identifier.
    /// </summary>
    public string PrimaryMicrophoneId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secondary microphone / capture endpoint device identifier.
    /// </summary>
    public string SecondaryMicrophoneId { get; set; } = string.Empty;

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

    /// <summary>
    /// Returns the ordered list of preferred playback audio endpoint identifiers, falling back to legacy Primary/Secondary/Fallback fields when <see cref="AudioPriorityIds"/> is empty.
    /// </summary>
    public IReadOnlyList<string> GetEffectiveAudioPriorityList() =>
        ResolveRanking(AudioPriorityIds, PrimaryAudioId, SecondaryAudioId, FallbackAudioId);

    /// <summary>
    /// Returns the ordered list of preferred microphone endpoint identifiers, falling back to legacy Primary/Secondary/Fallback fields when <see cref="MicrophonePriorityIds"/> is empty.
    /// </summary>
    public IReadOnlyList<string> GetEffectiveMicrophonePriorityList() =>
        ResolveRanking(MicrophonePriorityIds, PrimaryMicrophoneId, SecondaryMicrophoneId, FallbackMicrophoneId);

    /// <summary>
    /// Synchronizes the legacy <see cref="PrimaryAudioId"/>, <see cref="SecondaryAudioId"/>, and <see cref="FallbackAudioId"/> fields from <see cref="AudioPriorityIds"/>.
    /// </summary>
    public void SyncLegacyAudioFieldsFromPriorityList()
    {
        var list = GetEffectiveAudioPriorityList();
        AudioPriorityIds = [.. list];
        PrimaryAudioId = list.Count > 0 ? list[0] : string.Empty;
        SecondaryAudioId = list.Count > 1 ? list[1] : string.Empty;
        FallbackAudioId = list.Count > 2 ? list[2] : string.Empty;
    }

    /// <summary>
    /// Synchronizes the legacy <see cref="PrimaryMicrophoneId"/>, <see cref="SecondaryMicrophoneId"/>, and <see cref="FallbackMicrophoneId"/> fields from <see cref="MicrophonePriorityIds"/>.
    /// </summary>
    public void SyncLegacyMicrophoneFieldsFromPriorityList()
    {
        var list = GetEffectiveMicrophonePriorityList();
        MicrophonePriorityIds = [.. list];
        PrimaryMicrophoneId = list.Count > 0 ? list[0] : string.Empty;
        SecondaryMicrophoneId = list.Count > 1 ? list[1] : string.Empty;
        FallbackMicrophoneId = list.Count > 2 ? list[2] : string.Empty;
    }

    private static List<string> ResolveRanking(List<string>? explicitList, params string?[] legacy)
    {
        IEnumerable<string?> source = explicitList is { Count: > 0 } ? explicitList : legacy;
        return source
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

