namespace RigSwitch.Tests.Unit;

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Core.Services;
using RigSwitch.Infrastructure.Windows.Ccd;
using Xunit;

public sealed class ReviewFixesTests
{
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly ISettingsStorageService _settingsService;
    private readonly INativeCcdProvider _ccdProvider;

    public ReviewFixesTests()
    {
        _displayService = Substitute.For<IDisplayConfigurationService>();
        _audioDirector = Substitute.For<IAudioEndpointDirector>();
        _settingsService = Substitute.For<ISettingsStorageService>();
        _ccdProvider = Substitute.For<INativeCcdProvider>();
    }

    [Fact]
    public async Task AudioResolution_Desk_WhenSettingsContainBareGuid_ResolvesToPrefixedNativeId()
    {
        // Arrange
        const string bareGuid = "{D2B56B79-F353-4FB0-81B6-ECEF8E95E57A}";
        const string nativeId = "{0.0.0.00000000}.{D2B56B79-F353-4FB0-81B6-ECEF8E95E57A}";

        var settings = new UserSettings
        {
            DeskMonitorId = "MSI4DD0",
            DeskPrimaryAudioId = bareGuid
        };
        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns([new DisplayDeviceInfo("MSI4DD0", @"\\.\DISPLAY1", "MPG341CQPX", "RTX", true, true)]);

        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns([new AudioEndpointInfo(nativeId, "Speakers", "Realtek", DevicePresenceState.Active, false, false)]);

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.SimRig);

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.Desk);

        // Assert
        Assert.True(result);
        await _audioDirector.Received(1)
            .SetDefaultPlaybackEndpointAsync(nativeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AudioResolution_SimRig_WhenSettingsContainBareGuid_ResolvesToPrefixedNativeId()
    {
        // Arrange
        const string bareGuid = "{3CF792EA-E074-4D66-A8A1-9C4F1A8B204F}";
        const string nativeId = "{0.0.0.00000000}.{3CF792EA-E074-4D66-A8A1-9C4F1A8B204F}";

        var settings = new UserSettings
        {
            RigMonitorId = "AUS3438",
            RigPrimaryAudioId = bareGuid
        };
        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns([new DisplayDeviceInfo("AUS3438", @"\\.\DISPLAY2", "VG34VQL3A", "RTX", true, true)]);

        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns([new AudioEndpointInfo(nativeId, "Headset", "Realtek", DevicePresenceState.Active, false, false)]);

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert
        Assert.True(result);
        await _audioDirector.Received(1)
            .SetDefaultPlaybackEndpointAsync(nativeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AudioVisibilitySync_WhenSyncThrows_AllowsProfileSwitchToSucceed()
    {
        // Arrange
        var settings = new UserSettings
        {
            RigMonitorId = "AUS3438",
            RigPrimaryAudioId = "{3CF792EA-E074-4D66-A8A1-9C4F1A8B204F}",
            HiddenAudioEndpointIds = ["{0.0.0.00000000}.{HIDDEN-1}"]
        };
        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns([new DisplayDeviceInfo("AUS3438", @"\\.\DISPLAY2", "VG34VQL3A", "RTX", true, true)]);

        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns([new AudioEndpointInfo("{3CF792EA-E074-4D66-A8A1-9C4F1A8B204F}", "Headset", "Realtek", DevicePresenceState.Active, false, false)]);

        _audioDirector.SyncHiddenEndpointsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Registry write access denied"));

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert
        Assert.True(result);
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
        await _settingsService.Received(1).SaveSettingsAsync(
            Arg.Is<UserSettings>(s => s.LastActiveProfile == ProfileMode.SimRig),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MonitorReachability_WhenTargetMatchesFriendlyNameOrDevicePath_AllowsSwitch()
    {
        // Arrange
        var settings = new UserSettings
        {
            DeskMonitorId = "MPG341CQPX OLED", // matches friendly name
            DeskPrimaryAudioId = "audio-1"
        };
        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns([new DisplayDeviceInfo("MSI4DD0", @"\\.\DISPLAY1", "MPG341CQPX OLED", "RTX", true, true)]);

        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns([new AudioEndpointInfo("audio-1", "Speakers", "Realtek", DevicePresenceState.Active, true, false)]);

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.SimRig);

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.Desk);

        // Assert
        Assert.True(result);
        Assert.Equal(ProfileMode.Desk, coordinator.CurrentProfile);
    }

    [Fact]
    public void DisplayDeviceInfo_Matches_IdentifiesDisplayCorrectly()
    {
        // Arrange
        var display = new DisplayDeviceInfo(
            "MSI4DD0",
            @"\\?\DISPLAY#MSI4DD0#5&91ee1f9&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
            "MPG341CX OLED",
            "RTX 4070",
            true,
            true);

        // Assert
        Assert.True(display.Matches("MSI4DD0"));
        Assert.True(display.Matches("msi4dd0"));
        Assert.True(display.Matches("MPG341CX OLED"));
        Assert.True(display.Matches("mpg341cx oled"));
        Assert.True(display.Matches("UID4355"));
        Assert.False(display.Matches("AUS3438"));
        Assert.False(display.Matches(null));
        Assert.False(display.Matches("   "));
    }

    [Fact]
    public void Coordinator_SetCurrentProfile_UpdatesCurrentProfileDirectly()
    {
        // Arrange
        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        // Act
        coordinator.SetCurrentProfile(ProfileMode.SimRig);

        // Assert
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
    }

    [Fact]
    public async Task WindowsDisplayConfig_WhenInactiveMatchesTarget_TargetIndexRemainsActive()
    {
        // Arrange
        var paths = new DISPLAYCONFIG_PATH_INFO[2];
        paths[0].flags = NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE;
        paths[0].targetInfo.id = 101;
        paths[0].sourceInfo.modeInfoIdx = 0;

        paths[1].flags = 0;
        paths[1].targetInfo.id = 102;
        paths[1].sourceInfo.modeInfoIdx = 1;

        var modes = new DISPLAYCONFIG_MODE_INFO[2];
        modes[0].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
        modes[0].modeInfo.sourceMode.position = new POINTL { x = 0, y = 0 };
        modes[1].infoType = DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
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

        _ccdProvider.GetTargetDeviceName(ref Arg.Any<DISPLAYCONFIG_TARGET_DEVICE_NAME>())
            .Returns(x =>
            {
                var target = (DISPLAYCONFIG_TARGET_DEVICE_NAME)x[0];
                if (target.header.id == 101)
                {
                    target.monitorFriendlyDeviceName = "MSI4DD0";
                    target.monitorDevicePath = @"\\?\DISPLAY#MSI4DD0#UID1";
                    x[0] = target;
                    return 0;
                }
                if (target.header.id == 102)
                {
                    target.monitorFriendlyDeviceName = "AUS3438";
                    target.monitorDevicePath = @"\\?\DISPLAY#AUS3438#UID2";
                    x[0] = target;
                    return 0;
                }
                return 87;
            });

        DISPLAYCONFIG_PATH_INFO[]? capturedPaths = null;
        _ccdProvider.SetDisplayConfig(
            Arg.Do<DISPLAYCONFIG_PATH_INFO[]>(p => capturedPaths = (DISPLAYCONFIG_PATH_INFO[])p.Clone()),
            Arg.Any<DISPLAYCONFIG_MODE_INFO[]>(),
            Arg.Any<SetDisplayConfigFlags>())
            .Returns(0);

        var service = new WindowsDisplayConfigurationService(_ccdProvider);

        // Act - Pass MSI4DD0 as both target and inactive
        await service.ApplySingleDisplayTopologyAsync("MSI4DD0", "MSI4DD0");

        // Assert - Path 0 should NOT have been cleared because of 'if (i == targetIndex) continue;'
        Assert.NotNull(capturedPaths);
        Assert.Equal(NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE, capturedPaths[0].flags & NativeCcdApi.DISPLAYCONFIG_PATH_ACTIVE);
    }

    [Fact]
    public void MainSettingsWindow_CanInstantiateAndShow()
    {
        RunInSta(() =>
        {
            var coordinator = Substitute.For<IProfileSwitchCoordinator>();
            var trayIconService = new RigSwitch.App.Services.TrayIconService(coordinator, _settingsService);
            var vm = new RigSwitch.App.ViewModels.MainSettingsViewModel(

                coordinator,
                _settingsService,
                _displayService,
                _audioDirector,
                Substitute.For<IGlobalHotkeyService>(),
                trayIconService);

            var window = new RigSwitch.App.Views.MainSettingsWindow(vm);
            Assert.NotNull(window);
            window.Show();
            window.SetExplicitShutdown();
            window.Close();
        });
    }


    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}

