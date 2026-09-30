namespace RigSwitch.Core.Enums;

/// <summary>
/// Specifies the native HDR (High Dynamic Range / Advanced Color) switching behavior for a preset.
/// </summary>
public enum PresetHdrMode
{
    /// <summary>
    /// Retain the display's current Windows HDR state without alteration.
    /// </summary>
    Retain = 0,

    /// <summary>
    /// Enable native Windows HDR (Advanced Color) on the target display(s).
    /// </summary>
    Enable = 1,

    /// <summary>
    /// Disable native Windows HDR (Advanced Color) on the target display(s).
    /// </summary>
    Disable = 2,
}
