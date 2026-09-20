namespace RigSwitch.Core.Enums;

/// <summary>
/// Represents the presence or connection state of an audio endpoint device,
/// corresponding to Windows CoreAudio MMDevice DEVICE_STATE_* flags.
/// </summary>
[Flags]
public enum DevicePresenceState
{
    /// <summary>
    /// The audio endpoint device is active and available for use (DEVICE_STATE_ACTIVE = 1).
    /// </summary>
    Active = 1,

    /// <summary>
    /// The audio endpoint device is disabled (DEVICE_STATE_DISABLED = 2).
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// The audio endpoint device is not present (DEVICE_STATE_NOTPRESENT = 4).
    /// </summary>
    NotPresent = 4,

    /// <summary>
    /// The audio endpoint device is unplugged from its jack or connection (DEVICE_STATE_UNPLUGGED = 8).
    /// </summary>
    Unplugged = 8
}
