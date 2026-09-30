namespace RigSwitch.Core.Models;

using RigSwitch.Core.Enums;

/// <summary>
/// Configures target volume percentage and mute state for an audio endpoint in a workstation preset.
/// </summary>
public sealed record AudioVolumeSettings
{
    private int _volumePercent = 50;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioVolumeSettings"/> class.
    /// </summary>
    public AudioVolumeSettings()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioVolumeSettings"/> class with specified settings.
    /// </summary>
    /// <param name="mode">Volume behavior mode.</param>
    /// <param name="volumePercent">Target volume percentage [0, 100].</param>
    /// <param name="isMuted">Whether muted.</param>
    public AudioVolumeSettings(PresetVolumeBehavior mode, int volumePercent, bool isMuted = false)
    {
        Mode = mode;
        VolumePercent = volumePercent;
        IsMuted = isMuted;
    }

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
