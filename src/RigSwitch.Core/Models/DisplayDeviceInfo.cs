namespace RigSwitch.Core.Models;

/// <summary>
/// Represents descriptive and state information about a display monitor.
/// </summary>
public sealed record DisplayDeviceInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayDeviceInfo"/> record.
    /// </summary>
    public DisplayDeviceInfo()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayDeviceInfo"/> record with specified properties.
    /// </summary>
    /// <param name="monitorId">The EDID or hardware monitor identifier (e.g., "MSI4DD0", "AUS3438").</param>
    /// <param name="devicePath">The Win32 device interface path or device instance identifier.</param>
    /// <param name="friendlyName">The human-readable display product name.</param>
    /// <param name="displayAdapter">The graphics adapter name driving this display.</param>
    /// <param name="isActive">Whether the display is currently active in the Windows display topology.</param>
    /// <param name="isPrimary">Whether the display is currently designated as the primary display.</param>
    public DisplayDeviceInfo(
        string monitorId,
        string devicePath,
        string friendlyName,
        string displayAdapter,
        bool isActive,
        bool isPrimary)
    {
        MonitorId = monitorId;
        DevicePath = devicePath;
        FriendlyName = friendlyName;
        DisplayAdapter = displayAdapter;
        IsActive = isActive;
        IsPrimary = isPrimary;
    }

    /// <summary>
    /// Gets the EDID or hardware monitor identifier (e.g., "MSI4DD0", "AUS3438").
    /// </summary>
    public string MonitorId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Win32 device interface path or device instance identifier.
    /// </summary>
    public string DevicePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable display product name (e.g., "MPG341CX OLED", "VG34VQL3A").
    /// </summary>
    public string FriendlyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the graphics adapter name driving this display (e.g., "NVIDIA GeForce RTX 4070 Ti SUPER").
    /// </summary>
    public string DisplayAdapter { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this display is currently active in the Windows display topology.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets a value indicating whether this display is currently the primary display.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Checks whether this display device matches the specified monitor identifier, device path, or friendly name.
    /// </summary>
    /// <param name="identifier">The identifier, device path, or friendly name to match against.</param>
    /// <returns><c>true</c> if this display matches the identifier; otherwise, <c>false</c>.</returns>
    public bool Matches(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(MonitorId) &&
            string.Equals(MonitorId, identifier, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(DevicePath) &&
            (string.Equals(DevicePath, identifier, StringComparison.OrdinalIgnoreCase) ||
             DevicePath.Contains(identifier, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(FriendlyName) &&
            string.Equals(FriendlyName, identifier, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
