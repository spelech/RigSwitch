namespace RigSwitch.Core.Models;

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

    /// <summary>
    /// Gets or sets the target monitor hardware identifier or EDID.
    /// </summary>
    public string TargetMonitorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary audio endpoint device identifier.
    /// </summary>
    public string PrimaryAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fallback audio endpoint device identifier.
    /// </summary>
    public string FallbackAudioId { get; set; } = string.Empty;

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

