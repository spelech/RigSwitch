namespace RigSwitch.App.ViewModels;

/// <summary>
/// View model representing a hardware device and its user-defined custom friendly name.
/// </summary>
public sealed class DeviceNicknameItemViewModel : ViewModelBase
{
    private string _customNickname = string.Empty;

    /// <summary>
    /// Gets the unique hardware or endpoint identifier of the device.
    /// </summary>
    public string DeviceId { get; }

    /// <summary>
    /// Gets the device category description (e.g. "Monitor" or "Audio Playback").
    /// </summary>
    public string DeviceType { get; }

    /// <summary>
    /// Gets the manufacturer or default Windows friendly name of the device.
    /// </summary>
    public string HardwareName { get; }

    /// <summary>
    /// Gets or sets the custom friendly nickname configured by the user.
    /// </summary>
    public string CustomNickname
    {
        get => _customNickname;
        set => SetProperty(ref _customNickname, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceNicknameItemViewModel"/> class.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="deviceType">The device type description.</param>
    /// <param name="hardwareName">The default hardware name.</param>
    /// <param name="customNickname">The user nickname.</param>
    public DeviceNicknameItemViewModel(
        string deviceId,
        string deviceType,
        string hardwareName,
        string customNickname)
    {
        DeviceId = deviceId;
        DeviceType = deviceType;
        HardwareName = hardwareName;
        CustomNickname = customNickname;
    }
}
