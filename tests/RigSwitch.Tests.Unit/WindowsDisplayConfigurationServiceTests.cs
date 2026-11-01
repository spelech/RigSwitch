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
    public async Task ApplyDisplayTopologyAsync_MultiMonitor_ArrangesTargetsLinearlyAndDisablesInactive()
    {
        // Arrange - 3 displays: Path 0 is Desk (MSI4DD0), Path 1 is Rig Left (AUS3438), Path 2 is Rig Right (AUS3439)
        var paths = new DISPLAYCONFIG_PATH_INFO[3];

        paths[0].flags = NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE;
        paths[0].targetInfo.id = 101;
        paths[0].sourceInfo.modeInfoIdx = 0;

        paths[1].flags = 0;
        paths[1].targetInfo.id = 102;
        paths[1].sourceInfo.modeInfoIdx = 1;

        paths[2].flags = 0;
        paths[2].targetInfo.id = 103;
        paths[2].sourceInfo.modeInfoIdx = 2;

        var modes = new DISPLAYCONFIG_MODE_INFO[3];
        modes[0].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[0].modeInfo.sourceMode.width = 1920;
        modes[1].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[1].modeInfo.sourceMode.width = 2560;
        modes[2].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[2].modeInfo.sourceMode.width = 2560;

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
                target.monitorFriendlyDeviceName = "Desk Display";
                target.monitorDevicePath = @"\\?\DISPLAY#MSI4DD0#1";
            }
            else if (target.header.id == 102)
            {
                target.monitorFriendlyDeviceName = "Rig Left";
                target.monitorDevicePath = @"\\?\DISPLAY#AUS3438#2";
            }
            else if (target.header.id == 103)
            {
                target.monitorFriendlyDeviceName = "Rig Right";
                target.monitorDevicePath = @"\\?\DISPLAY#AUS3439#3";
            }
            x[0] = target;
            return 0;
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
        await service.ApplyDisplayTopologyAsync(["AUS3438", "AUS3439"], ["MSI4DD0"]);

        // Assert
        Assert.NotNull(capturedPaths);
        Assert.NotNull(capturedModes);

        // Path 0 (Desk) should be disabled
        Assert.Equal(0u, capturedPaths[0].flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE);

        // Path 1 (Rig Left) and Path 2 (Rig Right) should be active
        Assert.Equal(NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE, capturedPaths[1].flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE);
        Assert.Equal(NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE, capturedPaths[2].flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE);

        // Positions: Rig Left at (0,0), Rig Right at (2560,0)
        Assert.Equal(0, capturedModes[1].modeInfo.sourceMode.position.x);
        Assert.Equal(2560, capturedModes[2].modeInfo.sourceMode.position.x);
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

    [Fact]
    public async Task GetHdrInfoAsync_ReturnsCorrectHdrMetadataWhenSupported()
    {
        // Arrange
        SetupSingleDisplayPaths("MSI4DD0", targetId: 101);

        var dummyColor = Arg.Any<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
        _ccdProvider.GetAdvancedColorInfo(ref dummyColor).Returns(x =>
        {
            var info = (DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO)x[0];
            info.value = 0x3; // advancedColorSupported (bit 0) | advancedColorEnabled (bit 1)
            info.colorEncoding = DISPLAYCONFIG_COLOR_ENCODING.DISPLAYCONFIG_COLOR_ENCODING_RGB;
            info.bitsPerColorChannel = 10;
            x[0] = info;
            return 0;
        });

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act
        var result = await service.GetHdrInfoAsync("MSI4DD0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MSI4DD0", result.MonitorId);
        Assert.True(result.SupportsHdr);
        Assert.True(result.IsHdrEnabled);
        Assert.Equal(RigSwitch.Core.Enums.DisplayColorEncoding.Rgb, result.ColorEncoding);
        Assert.Equal(10, result.BitsPerColorChannel);
    }

    [Fact]
    public async Task SetHdrStateAsync_WhenAlreadyInDesiredState_PerformsNoOpWithoutCallingNativeSet()
    {
        // Arrange (Monitor already has HDR enabled)
        SetupSingleDisplayPaths("MSI4DD0", targetId: 101);

        var dummyColor = Arg.Any<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
        _ccdProvider.GetAdvancedColorInfo(ref dummyColor).Returns(x =>
        {
            var info = (DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO)x[0];
            info.value = 0x3; // Supported & Enabled
            x[0] = info;
            return 0;
        });

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act (Request to Enable HDR when already enabled)
        await service.SetHdrStateAsync("MSI4DD0", enableHdr: true);

        // Assert: Neither native setter should be called (flicker-free optimization!)
        var dummyHdr = Arg.Any<DISPLAYCONFIG_SET_HDR_STATE>();
        _ccdProvider.DidNotReceiveWithAnyArgs().SetHdrState(ref dummyHdr);
        var dummyAdv = Arg.Any<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>();
        _ccdProvider.DidNotReceiveWithAnyArgs().SetAdvancedColorState(ref dummyAdv);
    }

    [Fact]
    public async Task SetHdrStateAsync_ModernWin11_CallsSetHdrStateType16()
    {
        // Arrange (Monitor supports HDR but currently disabled)
        SetupSingleDisplayPaths("MSI4DD0", targetId: 101);

        var dummyColor = Arg.Any<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
        _ccdProvider.GetAdvancedColorInfo(ref dummyColor).Returns(x =>
        {
            var info = (DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO)x[0];
            info.value = 0x1; // Supported, but NOT enabled
            x[0] = info;
            return 0;
        });

        var dummyHdr = Arg.Any<DISPLAYCONFIG_SET_HDR_STATE>();
        _ccdProvider.SetHdrState(ref dummyHdr).Returns(0); // Success with type 16

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act
        await service.SetHdrStateAsync("MSI4DD0", enableHdr: true);

        // Assert
        _ccdProvider.Received(1).SetHdrState(ref Arg.Is<DISPLAYCONFIG_SET_HDR_STATE>(h => h.enableHdr && h.header.id == 101));
        var dummyAdv = Arg.Any<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>();
        _ccdProvider.DidNotReceiveWithAnyArgs().SetAdvancedColorState(ref dummyAdv);
    }

    [Fact]
    public async Task SetHdrStateAsync_WhenType16FailsWithNotSupported_FallsBackToSetAdvancedColorStateType10()
    {
        // Arrange
        SetupSingleDisplayPaths("MSI4DD0", targetId: 101);

        var dummyColor = Arg.Any<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
        _ccdProvider.GetAdvancedColorInfo(ref dummyColor).Returns(x =>
        {
            var info = (DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO)x[0];
            info.value = 0x1; // Supported, not enabled
            x[0] = info;
            return 0;
        });

        // Type 16 returns ERROR_NOT_SUPPORTED (50)
        var dummyHdr = Arg.Any<DISPLAYCONFIG_SET_HDR_STATE>();
        _ccdProvider.SetHdrState(ref dummyHdr).Returns(50);

        var dummyAdv = Arg.Any<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>();
        _ccdProvider.SetAdvancedColorState(ref dummyAdv).Returns(0); // Fallback succeeds

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act
        await service.SetHdrStateAsync("MSI4DD0", enableHdr: true);

        // Assert: Fell back to type 10
        _ccdProvider.Received(1).SetHdrState(ref Arg.Any<DISPLAYCONFIG_SET_HDR_STATE>());
        _ccdProvider.Received(1).SetAdvancedColorState(ref Arg.Is<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(a => a.enableAdvancedColor && a.header.id == 101));
    }

    [Fact]
    public async Task SetHdrStateAsync_WhenMonitorDoesNotSupportHdr_ThrowsNotSupportedException()
    {
        // Arrange
        SetupSingleDisplayPaths("MSI4DD0", targetId: 101);

        var dummyColor = Arg.Any<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
        _ccdProvider.GetAdvancedColorInfo(ref dummyColor).Returns(x =>
        {
            var info = (DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO)x[0];
            info.value = 0x0; // Not supported
            x[0] = info;
            return 0;
        });

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.SetHdrStateAsync("MSI4DD0", enableHdr: true));
    }

    [Fact]
    public async Task SetHdrStateAsync_WhenMonitorNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        SetupSingleDisplayPaths("MSI4DD0", targetId: 101);

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetHdrStateAsync("NON_EXISTENT_MONITOR", enableHdr: true));
    }

    private void SetupSingleDisplayPaths(string monitorId, uint targetId)
    {
        var paths = new DISPLAYCONFIG_PATH_INFO[1];
        paths[0].flags = NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE;
        paths[0].targetInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[0].targetInfo.id = targetId;
        paths[0].sourceInfo.adapterId = new LUID { LowPart = 1, HighPart = 0 };
        paths[0].sourceInfo.id = 0;
        paths[0].sourceInfo.modeInfoIdx = 0;

        var modes = new DISPLAYCONFIG_MODE_INFO[1];
        modes[0].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[0].id = 0;
        modes[0].adapterId = new LUID { LowPart = 1, HighPart = 0 };
        modes[0].modeInfo.sourceMode.position = new POINTL { x = 0, y = 0 };

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
            target.monitorFriendlyDeviceName = "Display " + monitorId;
            target.monitorDevicePath = $@"\\?\DISPLAY#{monitorId}#5&91ee1f9&0&UID4355#{{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}}";
            x[0] = target;
            return 0;
        });
    }
}
