namespace RigSwitch.Core.Enums;

/// <summary>
/// Specifies the active workstation hardware profile mode.
/// </summary>
public enum ProfileMode
{
    /// <summary>
    /// Desk workstation profile (MSI OLED display active, Creative Pebble V3 primary audio).
    /// </summary>
    Desk,

    /// <summary>
    /// Sim rig workstation profile (Asus ultrawide display active, rig audio routing).
    /// </summary>
    SimRig
}
