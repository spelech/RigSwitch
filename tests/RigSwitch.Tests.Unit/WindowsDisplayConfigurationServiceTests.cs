namespace RigSwitch.Tests.Unit;

using System.ComponentModel;
using NSubstitute;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Windows.Ccd;
using Xunit;

public sealed class WindowsDisplayConfigurationServiceTests
{
    private readonly INativeCcdProvider _ccdProvider;

    public WindowsDisplayConfigurationServiceTests()
    {
        _ccdProvider = Substitute.For<INativeCcdProvider>();
    }

    [Fact]
    public async Task EnumerateDisplaysAsync_ReturnsBothActiveAndInactiveMonitors()
    {
        // Arrange
        var paths = new DISPLAYCONFIG_PATH_INFO[2];

        // Path 0: Active monitor (MSI4DD0)
        paths[0].flags = NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE;
        paths[0].targetInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[0].targetInfo.id = 101;
        paths[0].sourceInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[0].sourceInfo.id = 0;
        paths[0].sourceInfo.modeInfoIdx = 0;

        // Path 1: Inactive monitor (AUS3438)
        paths[1].flags = 0;
        paths[1].targetInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[1].targetInfo.id = 102;
        paths[1].sourceInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[1].sourceInfo.id = 1;
        paths[1].sourceInfo.modeInfoIdx = 1;

        var modes = new DISPLAYCONFIG_MODE_INFO[2];
        modes[0].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[0].id = 0;
        modes[0].adapterId = new LUID { LowPart = 1, HighPart = 0 };
        modes[0].modeInfo.sourceMode.position = new POINTL { x = 0, y = 0 };

        modes[1].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[1].id = 1;
        modes[1].adapterId = new LUID { LowPart = 1, HighPart = 0 };
        modes[1].modeInfo.sourceMode.position = new POINTL { x = 3440, y = 0 };

        _ccdProvider.QueryDisplayConfig(
            QueryDisplayFlags.QDC_ALL_PATHS,
            out Arg.Any<DISPLAYCONFIG_PATH_INFO[]>(),
            out Arg.Any<DISPLAYCONFIG_MODE_INFO[]>())
            .Returns(x =>
            {
                x[1] = paths;
                x[2] = modes;
                return 0;
            });

        var dummyTarget = Arg.Any<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        _ccdProvider.GetTargetDeviceName(ref dummyTarget).Returns(x =>
        {
            var target = (DISPLAYCONFIG_TARGET_DEVICE_NAME)x[0];
            if (target.header.id == 101)
            {
                target.monitorFriendlyDeviceName = "MPG341CX OLED";
                target.monitorDevicePath = @"\\?\DISPLAY#MSI4DD0#5&91ee1f9&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
                x[0] = target;
                return 0;
            }

            if (target.header.id == 102)
            {
                target.monitorFriendlyDeviceName = "VG34VQL3A";
                target.monitorDevicePath = @"\\?\DISPLAY#AUS3438#5&91ee1f9&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
                x[0] = target;
                return 0;
            }

            return 87; // ERROR_INVALID_PARAMETER
        });

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act
        var displays = await service.EnumerateDisplaysAsync();

        // Assert
        Assert.NotNull(displays);
        Assert.Equal(2, displays.Count);

        var msiDisplay = displays.FirstOrDefault(d => d.MonitorId == "MSI4DD0");
        Assert.NotNull(msiDisplay);
        Assert.Equal("MPG341CX OLED", msiDisplay.FriendlyName);
        Assert.True(msiDisplay.IsActive);
        Assert.True(msiDisplay.IsPrimary);

        var asusDisplay = displays.FirstOrDefault(d => d.MonitorId == "AUS3438");
        Assert.NotNull(asusDisplay);
        Assert.Equal("VG34VQL3A", asusDisplay.FriendlyName);
        Assert.False(asusDisplay.IsActive);
        Assert.False(asusDisplay.IsPrimary);
    }

    [Fact]
    public async Task ApplySingleDisplayTopologyAsync_EnablesTargetAndDisablesInactive()
    {
        // Arrange
        var paths = new DISPLAYCONFIG_PATH_INFO[2];

        // Path 0: Currently active desk monitor (MSI4DD0) - should be disabled
        paths[0].flags = NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE;
        paths[0].targetInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[0].targetInfo.id = 101;
        paths[0].sourceInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[0].sourceInfo.id = 0;
        paths[0].sourceInfo.modeInfoIdx = 0;

        // Path 1: Currently inactive rig monitor (AUS3438) - should be enabled and made primary
        paths[1].flags = 0;
        paths[1].targetInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[1].targetInfo.id = 102;
        paths[1].sourceInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[1].sourceInfo.id = 1;
        paths[1].sourceInfo.modeInfoIdx = 1;

        var modes = new DISPLAYCONFIG_MODE_INFO[2];
        modes[0].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[0].id = 0;
        modes[0].adapterId = new LUID { LowPart = 1, HighPart = 0 };
        modes[0].modeInfo.sourceMode.position = new POINTL { x = 0, y = 0 };

        modes[1].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[1].id = 1;
        modes[1].adapterId = new LUID { LowPart = 1, HighPart = 0 };
        modes[1].modeInfo.sourceMode.position = new POINTL { x = 1920, y = 0 };

        _ccdProvider.QueryDisplayConfig(
            QueryDisplayFlags.QDC_ALL_PATHS,
            out Arg.Any<DISPLAYCONFIG_PATH_INFO[]>(),
            out Arg.Any<DISPLAYCONFIG_MODE_INFO[]>())
            .Returns(x =>
            {
                x[1] = (DISPLAYCONFIG_PATH_INFO[])paths.Clone();
                x[2] = (DISPLAYCONFIG_MODE_INFO[])modes.Clone();
                return 0;
            });

        var dummyTarget = Arg.Any<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        _ccdProvider.GetTargetDeviceName(ref dummyTarget).Returns(x =>
        {
            var target = (DISPLAYCONFIG_TARGET_DEVICE_NAME)x[0];
            if (target.header.id == 101)
            {
                target.monitorFriendlyDeviceName = "MPG341CX OLED";
                target.monitorDevicePath = @"\\?\DISPLAY#MSI4DD0#5&91ee1f9&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
                x[0] = target;
                return 0;
            }

            if (target.header.id == 102)
            {
                target.monitorFriendlyDeviceName = "VG34VQL3A";
                target.monitorDevicePath = @"\\?\DISPLAY#AUS3438#5&91ee1f9&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
                x[0] = target;
                return 0;
            }

            return 87;
        });

        DISPLAYCONFIG_PATH_INFO[]? capturedPaths = null;
        DISPLAYCONFIG_MODE_INFO[]? capturedModes = null;
        SetDisplayConfigFlags capturedFlags = 0;

        _ccdProvider.SetDisplayConfig(
            Arg.Do<DISPLAYCONFIG_PATH_INFO[]>(p => capturedPaths = (DISPLAYCONFIG_PATH_INFO[])p.Clone()),
            Arg.Do<DISPLAYCONFIG_MODE_INFO[]>(m => capturedModes = (DISPLAYCONFIG_MODE_INFO[])m.Clone()),
            Arg.Do<SetDisplayConfigFlags>(f => capturedFlags = f))
            .Returns(0);

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act
        await service.ApplySingleDisplayTopologyAsync("AUS3438", "MSI4DD0");

        // Assert
        Assert.NotNull(capturedPaths);
        Assert.NotNull(capturedModes);

        // Path 0 (MSI4DD0) should now be inactive
        Assert.Equal(0u, capturedPaths[0].flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE);

        // Path 1 (AUS3438) should now be active
        Assert.Equal(NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE, capturedPaths[1].flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE);

        // Path 1's source mode position should be (0, 0)
        uint rigModeIdx = capturedPaths[1].sourceInfo.modeInfoIdx;
        Assert.Equal(0, capturedModes[rigModeIdx].modeInfo.sourceMode.position.x);
        Assert.Equal(0, capturedModes[rigModeIdx].modeInfo.sourceMode.position.y);

        // Assert expected flags passed to SetDisplayConfig
        var expectedFlags = SetDisplayConfigFlags.SDC_APPLY |
                            SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE |
                            SetDisplayConfigFlags.SDC_ALLOW_CHANGES |
                            SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG;
        Assert.Equal(expectedFlags, capturedFlags);
    }

    [Fact]
    public async Task ApplySingleDisplayTopologyAsync_WhenTargetNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var paths = new DISPLAYCONFIG_PATH_INFO[1];
        paths[0].flags = NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE;
        paths[0].targetInfo.id = 101;

        var modes = new DISPLAYCONFIG_MODE_INFO[1];

        _ccdProvider.QueryDisplayConfig(
            QueryDisplayFlags.QDC_ALL_PATHS,
            out Arg.Any<DISPLAYCONFIG_PATH_INFO[]>(),
            out Arg.Any<DISPLAYCONFIG_MODE_INFO[]>())
            .Returns(x =>
            {
                x[1] = paths;
                x[2] = modes;
                return 0;
            });

        var dummyTarget = Arg.Any<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        _ccdProvider.GetTargetDeviceName(ref dummyTarget).Returns(x =>
        {
            var target = (DISPLAYCONFIG_TARGET_DEVICE_NAME)x[0];
            target.monitorFriendlyDeviceName = "MPG341CX OLED";
            target.monitorDevicePath = @"\\?\DISPLAY#MSI4DD0#5&91ee1f9&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
            x[0] = target;
            return 0;
        });

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplySingleDisplayTopologyAsync("NON_EXISTENT_MONITOR", null));

        Assert.Equal("Target monitor 'NON_EXISTENT_MONITOR' was not found on system.", ex.Message);
    }

    [Fact]
    public async Task ApplySingleDisplayTopologyAsync_WhenSetDisplayConfigFails_ThrowsWin32Exception()
    {
        // Arrange
        var paths = new DISPLAYCONFIG_PATH_INFO[1];
        paths[0].flags = 0;
        paths[0].targetInfo.id = 101;
        paths[0].sourceInfo.modeInfoIdx = 0;

        var modes = new DISPLAYCONFIG_MODE_INFO[1];
        modes[0].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[0].modeInfo.sourceMode.position = new POINTL { x = 100, y = 100 };

        _ccdProvider.QueryDisplayConfig(
            QueryDisplayFlags.QDC_ALL_PATHS,
            out Arg.Any<DISPLAYCONFIG_PATH_INFO[]>(),
            out Arg.Any<DISPLAYCONFIG_MODE_INFO[]>())
            .Returns(x =>
            {
                x[1] = paths;
                x[2] = modes;
                return 0;
            });

        var dummyTarget = Arg.Any<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        _ccdProvider.GetTargetDeviceName(ref dummyTarget).Returns(x =>
        {
            var target = (DISPLAYCONFIG_TARGET_DEVICE_NAME)x[0];
            target.monitorFriendlyDeviceName = "MPG341CX OLED";
            target.monitorDevicePath = @"\\?\DISPLAY#MSI4DD0#5&91ee1f9&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
            x[0] = target;
            return 0;
        });

        const int ERROR_ACCESS_DENIED = 5;
        _ccdProvider.SetDisplayConfig(
            Arg.Any<DISPLAYCONFIG_PATH_INFO[]>(),
            Arg.Any<DISPLAYCONFIG_MODE_INFO[]>(),
            Arg.Any<SetDisplayConfigFlags>())
            .Returns(ERROR_ACCESS_DENIED);

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Win32Exception>(() =>
            service.ApplySingleDisplayTopologyAsync("MSI4DD0", null));

        Assert.Equal(ERROR_ACCESS_DENIED, ex.NativeErrorCode);
        Assert.Contains("SetDisplayConfig failed with error 5", ex.Message);
    }

    [Fact]
    public async Task ApplySingleDisplayTopologyAsync_WhenInactiveMonitorIdNull_OnlyEnablesTarget()
    {
        // Arrange
        var paths = new DISPLAYCONFIG_PATH_INFO[2];
        paths[0].flags = 0;
        paths[0].targetInfo.id = 101;
        paths[0].sourceInfo.modeInfoIdx = 0;

        paths[1].flags = 0;
        paths[1].targetInfo.id = 102;
        paths[1].sourceInfo.modeInfoIdx = 1;

        var modes = new DISPLAYCONFIG_MODE_INFO[2];
        modes[0].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[0].modeInfo.sourceMode.position = new POINTL { x = 200, y = 200 };

        modes[1].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;

        _ccdProvider.QueryDisplayConfig(
            QueryDisplayFlags.QDC_ALL_PATHS,
            out Arg.Any<DISPLAYCONFIG_PATH_INFO[]>(),
            out Arg.Any<DISPLAYCONFIG_MODE_INFO[]>())
            .Returns(x =>
            {
                x[1] = (DISPLAYCONFIG_PATH_INFO[])paths.Clone();
                x[2] = (DISPLAYCONFIG_MODE_INFO[])modes.Clone();
                return 0;
            });

        var dummyTarget = Arg.Any<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        _ccdProvider.GetTargetDeviceName(ref dummyTarget).Returns(x =>
        {
            var target = (DISPLAYCONFIG_TARGET_DEVICE_NAME)x[0];
            if (target.header.id == 101)
            {
                target.monitorFriendlyDeviceName = "MPG341CX OLED";
                target.monitorDevicePath = @"\\?\DISPLAY#MSI4DD0#5&91ee1f9&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
                x[0] = target;
                return 0;
            }

            if (target.header.id == 102)
            {
                target.monitorFriendlyDeviceName = "VG34VQL3A";
                target.monitorDevicePath = @"\\?\DISPLAY#AUS3438#5&91ee1f9&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
                x[0] = target;
                return 0;
            }

            return 87;
        });

        DISPLAYCONFIG_PATH_INFO[]? capturedPaths = null;
        DISPLAYCONFIG_MODE_INFO[]? capturedModes = null;

        _ccdProvider.SetDisplayConfig(
            Arg.Do<DISPLAYCONFIG_PATH_INFO[]>(p => capturedPaths = (DISPLAYCONFIG_PATH_INFO[])p.Clone()),
            Arg.Do<DISPLAYCONFIG_MODE_INFO[]>(m => capturedModes = (DISPLAYCONFIG_MODE_INFO[])m.Clone()),
            Arg.Any<SetDisplayConfigFlags>())
            .Returns(0);

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act
        await service.ApplySingleDisplayTopologyAsync("MSI4DD0", inactiveMonitorId: null);

        // Assert
        Assert.NotNull(capturedPaths);
        Assert.NotNull(capturedModes);
        Assert.Equal(NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE, capturedPaths[0].flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE);
        Assert.Equal(0u, capturedPaths[1].flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE);
        Assert.Equal(0, capturedModes[0].modeInfo.sourceMode.position.x);
        Assert.Equal(0, capturedModes[0].modeInfo.sourceMode.position.y);
    }

    [Fact]
    public async Task EnumerateDisplaysAsync_WhenQueryFails_ThrowsWin32Exception()
    {
        // Arrange
        const int ERROR_GEN_FAILURE = 31;
        _ccdProvider.QueryDisplayConfig(
            QueryDisplayFlags.QDC_ALL_PATHS,
            out Arg.Any<DISPLAYCONFIG_PATH_INFO[]>(),
            out Arg.Any<DISPLAYCONFIG_MODE_INFO[]>())
            .Returns(x =>
            {
                x[1] = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
                x[2] = Array.Empty<DISPLAYCONFIG_MODE_INFO>();
                return ERROR_GEN_FAILURE;
            });

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Win32Exception>(() => service.EnumerateDisplaysAsync());
        Assert.Equal(ERROR_GEN_FAILURE, ex.NativeErrorCode);
    }

    [Fact]
    public async Task EnumerateDisplaysAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = new WindowsDisplayConfigurationService(_ccdProvider);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.EnumerateDisplaysAsync(cts.Token));
    }

    [Fact]
    public async Task ApplySingleDisplayTopologyAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = new WindowsDisplayConfigurationService(_ccdProvider);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ApplySingleDisplayTopologyAsync("MSI4DD0", "AUS3438", cts.Token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ApplySingleDisplayTopologyAsync_WhenTargetMonitorIdNullOrWhitespace_ThrowsArgumentException(string? invalidTargetId)
    {
        // Arrange
        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.ApplySingleDisplayTopologyAsync(invalidTargetId!, null));
    }
}
