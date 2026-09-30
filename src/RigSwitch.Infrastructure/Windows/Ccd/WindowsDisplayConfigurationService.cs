namespace RigSwitch.Infrastructure.Windows.Ccd;

using System.ComponentModel;
using System.Runtime.InteropServices;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Implements <see cref="IDisplayConfigurationService"/> using Windows Connecting and Configuring Displays (CCD) APIs.
/// </summary>
public sealed class WindowsDisplayConfigurationService : IDisplayConfigurationService
{
    private readonly INativeCcdProvider _ccdProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsDisplayConfigurationService"/> class.
    /// </summary>
    /// <param name="ccdProvider">Optional native CCD provider abstraction. Defaults to <see cref="WindowsNativeCcdProvider"/>.</param>
    public WindowsDisplayConfigurationService(INativeCcdProvider? ccdProvider = null)
    {
        _ccdProvider = ccdProvider ?? new WindowsNativeCcdProvider();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DisplayDeviceInfo>> EnumerateDisplaysAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int queryResult = _ccdProvider.QueryDisplayConfig(QueryDisplayFlags.QDC_ALL_PATHS, out var paths, out var modes);
        if (queryResult != 0)
        {
            throw new Win32Exception(queryResult, $"QueryDisplayConfig failed with error {queryResult}");
        }

        var displaysByMonitorId = new Dictionary<string, DisplayDeviceInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var devName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                    adapterId = path.targetInfo.adapterId,
                    id = path.targetInfo.id
                }
            };

            if (_ccdProvider.GetTargetDeviceName(ref devName) != 0)
            {
                continue;
            }

            string monitorId = ExtractMonitorId(devName);
            if (string.IsNullOrWhiteSpace(monitorId))
            {
                continue;
            }

            bool isPathActive = (path.flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE) != 0;
            bool isPathPrimary = false;

            if (isPathActive &&
                path.sourceInfo.modeInfoIdx != NativeCcdApi.DISPLAYCONFIG_PATH_MODE_IDX_INVALID &&
                path.sourceInfo.modeInfoIdx < modes.Length)
            {
                var mode = modes[path.sourceInfo.modeInfoIdx];
                if (mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                {
                    isPathPrimary = mode.modeInfo.sourceMode.position.x == 0 &&
                                    mode.modeInfo.sourceMode.position.y == 0;
                }
            }

            string friendlyName = !string.IsNullOrWhiteSpace(devName.monitorFriendlyDeviceName)
                ? devName.monitorFriendlyDeviceName
                : monitorId;

            string devicePath = devName.monitorDevicePath ?? string.Empty;

            if (displaysByMonitorId.TryGetValue(monitorId, out var existing))
            {
                displaysByMonitorId[monitorId] = new DisplayDeviceInfo(
                    monitorId: monitorId,
                    devicePath: string.IsNullOrWhiteSpace(existing.DevicePath) ? devicePath : existing.DevicePath,
                    friendlyName: string.IsNullOrWhiteSpace(existing.FriendlyName) ? friendlyName : existing.FriendlyName,
                    displayAdapter: existing.DisplayAdapter,
                    isActive: existing.IsActive || isPathActive,
                    isPrimary: existing.IsPrimary || isPathPrimary);
            }
            else
            {
                displaysByMonitorId[monitorId] = new DisplayDeviceInfo(
                    monitorId: monitorId,
                    devicePath: devicePath,
                    friendlyName: friendlyName,
                    displayAdapter: string.Empty,
                    isActive: isPathActive,
                    isPrimary: isPathPrimary);
            }
        }

        IReadOnlyList<DisplayDeviceInfo> result = displaysByMonitorId.Values.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task ApplyDisplayTopologyAsync(
        IReadOnlyList<string> targetMonitorIds,
        IReadOnlyList<string>? inactiveMonitorIds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetMonitorIds);
        if (targetMonitorIds.Count == 0)
        {
            throw new ArgumentException("At least one target monitor ID must be provided.", nameof(targetMonitorIds));
        }

        cancellationToken.ThrowIfCancellationRequested();

        int queryResult = _ccdProvider.QueryDisplayConfig(QueryDisplayFlags.QDC_ALL_PATHS, out var paths, out var modes);
        if (queryResult != 0)
        {
            throw new Win32Exception(queryResult, $"QueryDisplayConfig failed with error {queryResult}");
        }

        var pathTargetInfos = new (string MonitorId, string DevicePath, string FriendlyName)[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            var devName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                    adapterId = paths[i].targetInfo.adapterId,
                    id = paths[i].targetInfo.id
                }
            };

            if (_ccdProvider.GetTargetDeviceName(ref devName) == 0)
            {
                string monId = ExtractMonitorId(devName);
                pathTargetInfos[i] = (monId, devName.monitorDevicePath ?? string.Empty, devName.monitorFriendlyDeviceName ?? string.Empty);
            }
            else
            {
                pathTargetInfos[i] = (string.Empty, string.Empty, string.Empty);
            }
        }

        var targetIndices = new List<int>();
        foreach (var targetId in targetMonitorIds)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            int matchIdx = -1;
            for (int i = 0; i < paths.Length; i++)
            {
                var (monId, devPath, friendly) = pathTargetInfos[i];
                if (MatchesMonitor(targetId, monId, devPath, friendly))
                {
                    if (matchIdx == -1 || (paths[i].flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE) != 0)
                    {
                        matchIdx = i;
                    }
                }
            }

            if (matchIdx == -1)
            {
                throw new InvalidOperationException($"Target monitor '{targetId}' was not found on system.");
            }

            if (!targetIndices.Contains(matchIdx))
            {
                targetIndices.Add(matchIdx);
            }
        }

        if (targetIndices.Count == 0)
        {
            throw new InvalidOperationException("None of the specified target monitors were found on the system.");
        }

        var clonedPaths = (DISPLAYCONFIG_PATH_INFO[])paths.Clone();
        var clonedModes = (DISPLAYCONFIG_MODE_INFO[])modes.Clone();

        // Step 1: Disable all paths by default or matching inactives
        for (int i = 0; i < clonedPaths.Length; i++)
        {
            if (targetIndices.Contains(i))
            {
                continue;
            }

            if (inactiveMonitorIds != null && inactiveMonitorIds.Count > 0)
            {
                var (monId, devPath, friendly) = pathTargetInfos[i];
                bool isInactive = inactiveMonitorIds.Any(inact => MatchesMonitor(inact, monId, devPath, friendly));
                if (isInactive)
                {
                    clonedPaths[i].flags = 0;
                }
            }
            else
            {
                // Clear active flag for non-target paths if inactive list not specified
                clonedPaths[i].flags = 0;
            }
        }

        // Step 2: Enable target paths and arrange linearly (first target at (0,0) as primary)
        int currentX = 0;
        for (int tIdx = 0; tIdx < targetIndices.Count; tIdx++)
        {
            int pIndex = targetIndices[tIdx];
            clonedPaths[pIndex].flags = NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE;

            uint modeIdx = clonedPaths[pIndex].sourceInfo.modeInfoIdx;
            if (modeIdx != NativeCcdApi.DISPLAYCONFIG_PATH_MODE_IDX_INVALID && modeIdx < clonedModes.Length)
            {
                clonedModes[modeIdx].modeInfo.sourceMode.position = new POINTL { x = currentX, y = 0 };

                // If width is known and positive, advance currentX; otherwise increment by default width (1920)
                int width = (int)clonedModes[modeIdx].modeInfo.sourceMode.width;
                currentX += width > 0 ? width : 1920;
            }
        }

        var flags = SetDisplayConfigFlags.SDC_APPLY |
                    SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE |
                    SetDisplayConfigFlags.SDC_ALLOW_CHANGES |
                    SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG;

        int setResult = _ccdProvider.SetDisplayConfig(clonedPaths, clonedModes, flags);
        if (setResult != 0)
        {
            throw new Win32Exception(setResult, $"SetDisplayConfig failed with error {setResult}");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ApplySingleDisplayTopologyAsync(
        string targetMonitorId,
        string? inactiveMonitorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetMonitorId);
        var targetList = new[] { targetMonitorId };
        var inactiveList = string.IsNullOrWhiteSpace(inactiveMonitorId) ? null : new[] { inactiveMonitorId };
        return ApplyDisplayTopologyAsync(targetList, inactiveList, cancellationToken);
    }

    private static string ExtractMonitorId(in DISPLAYCONFIG_TARGET_DEVICE_NAME targetName)
    {
        if (!string.IsNullOrWhiteSpace(targetName.monitorDevicePath))
        {
            var parts = targetName.monitorDevicePath.Split('#');
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                return parts[1].Trim();
            }
        }

        if (targetName.edidManufactureId != 0)
        {
            ushort beMfg = (ushort)(((targetName.edidManufactureId & 0xFF) << 8) | ((targetName.edidManufactureId >> 8) & 0xFF));
            char c1 = (char)('A' + ((beMfg >> 10) & 0x1F) - 1);
            char c2 = (char)('A' + ((beMfg >> 5) & 0x1F) - 1);
            char c3 = (char)('A' + (beMfg & 0x1F) - 1);
            if (char.IsLetter(c1) && char.IsLetter(c2) && char.IsLetter(c3))
            {
                return $"{c1}{c2}{c3}{targetName.edidProductCodeId:X4}";
            }
        }

        if (!string.IsNullOrWhiteSpace(targetName.monitorDevicePath))
        {
            return targetName.monitorDevicePath.Trim();
        }

        return string.Empty;
    }

    private static bool MatchesMonitor(string expectedId, string monitorId, string devicePath, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(expectedId))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(monitorId) &&
            string.Equals(monitorId, expectedId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(devicePath) &&
            devicePath.Contains(expectedId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(friendlyName) &&
            string.Equals(friendlyName, expectedId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
