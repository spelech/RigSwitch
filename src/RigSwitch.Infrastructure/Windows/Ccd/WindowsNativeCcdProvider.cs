namespace RigSwitch.Infrastructure.Windows.Ccd;

/// <summary>
/// Default implementation of <see cref="INativeCcdProvider"/> that invokes Windows CCD P/Invoke functions.
/// </summary>
public sealed class WindowsNativeCcdProvider : INativeCcdProvider
{
    private const int ErrorInsufficientBuffer = 122;

    /// <inheritdoc/>
    public int QueryDisplayConfig(QueryDisplayFlags flags, out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes)
    {
        int error = 0;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            error = NativeCcdApi.GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount);
            if (error != 0)
            {
                paths = [];
                modes = [];
                return error;
            }

            paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            error = NativeCcdApi.QueryDisplayConfig(flags, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (error == 0)
            {
                if (pathCount < paths.Length)
                {
                    Array.Resize(ref paths, (int)pathCount);
                }

                if (modeCount < modes.Length)
                {
                    Array.Resize(ref modes, (int)modeCount);
                }

                return 0;
            }

            if (error != ErrorInsufficientBuffer)
            {
                paths = [];
                modes = [];
                return error;
            }
        }

        paths = [];
        modes = [];
        return error;
    }

    /// <inheritdoc/>
    public int GetTargetDeviceName(ref DISPLAYCONFIG_TARGET_DEVICE_NAME targetName)
    {
        return NativeCcdApi.DisplayConfigGetDeviceInfo(ref targetName);
    }

    /// <inheritdoc/>
    public int SetDisplayConfig(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes, SetDisplayConfigFlags flags)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(modes);

        return NativeCcdApi.SetDisplayConfig(
            (uint)paths.Length,
            paths,
            (uint)modes.Length,
            modes,
            flags);
    }
}
