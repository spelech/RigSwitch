namespace RigSwitch.Infrastructure.Windows.Ccd;

/// <summary>
/// Defines a testable abstraction over Windows Connecting and Configuring Displays (CCD) APIs.
/// </summary>
public interface INativeCcdProvider
{
    /// <summary>
    /// Retrieves information about all possible display paths and modes for the specified query flags.
    /// </summary>
    /// <param name="flags">The query flags specifying which paths and modes to retrieve.</param>
    /// <param name="paths">The retrieved array of display paths.</param>
    /// <param name="modes">The retrieved array of display mode information.</param>
    /// <returns>A Win32 error code (0 for ERROR_SUCCESS).</returns>
    int QueryDisplayConfig(QueryDisplayFlags flags, out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes);

    /// <summary>
    /// Retrieves display device information, such as target friendly name and device path.
    /// </summary>
    /// <param name="targetName">The structure to populate with display target device information.</param>
    /// <returns>A Win32 error code (0 for ERROR_SUCCESS).</returns>
    int GetTargetDeviceName(ref DISPLAYCONFIG_TARGET_DEVICE_NAME targetName);

    /// <summary>
    /// Modifies the display topology, source modes, and target modes.
    /// </summary>
    /// <param name="paths">The array of display paths to configure.</param>
    /// <param name="modes">The array of mode information to apply.</param>
    /// <param name="flags">Flags specifying configuration behavior and database persistence.</param>
    /// <returns>A Win32 error code (0 for ERROR_SUCCESS).</returns>
    int SetDisplayConfig(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes, SetDisplayConfigFlags flags);
}
