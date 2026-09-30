namespace RigSwitch.Core.Models;

using RigSwitch.Core.Enums;

/// <summary>
/// Configures target volume percentage and mute state for an audio endpoint in a workstation preset.
/// </summary>
public sealed record AudioVolumeSettings
{
    private int _volumePercent = 50;

    /// <summary>
    /// Gets or sets the volume behavior mode (Retain existing or apply Custom level).
    /// </summary>
    public PresetVolumeBehavior Mode { get; set; } = PresetVolumeBehavior.Retain;

    /// <summary>
    /// Gets or sets the target volume percentage, clamped to [0, 100].
    /// </summary>
    public int VolumePercent
    {
        get => _volumePercent;
        set => _volumePercent = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the audio endpoint should be muted.
    /// </summary>
    public bool IsMuted { get; set; }
}
