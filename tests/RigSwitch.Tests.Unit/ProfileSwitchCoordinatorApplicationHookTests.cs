namespace RigSwitch.Tests.Unit;

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Core.Services;
using Xunit;

public sealed class ProfileSwitchCoordinatorApplicationHookTests
{
    private const string DeskMonitorId = "MSI4DD0";
    private const string RigMonitorId = "AUS3438";

    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly ISettingsStorageService _settingsService;
    private readonly IApplicationLifecycleHookService _appHookService;
    private readonly UserSettings _settings;

    public ProfileSwitchCoordinatorApplicationHookTests()
    {
        _displayService = Substitute.For<IDisplayConfigurationService>();
        _audioDirector = Substitute.For<IAudioEndpointDirector>();
        _settingsService = Substitute.For<ISettingsStorageService>();
        _appHookService = Substitute.For<IApplicationLifecycleHookService>();

        _settings = new UserSettings
        {
            LastActiveProfile = ProfileMode.Desk,
            DeskMonitorId = DeskMonitorId,
            RigMonitorId = RigMonitorId
        };

        _settings.DeskPresets[0].TargetMonitorId = DeskMonitorId;
        _settings.DeskPresets[0].ApplicationHooks =
        [
            new PresetApplicationHook { Id = "desk-hook-1", ExecutablePath = @"C:\Apps\DeskApp.exe", CloseOnSwitchAway = true }
        ];

        _settings.RigPresets[0].TargetMonitorId = RigMonitorId;
        _settings.RigPresets[0].ApplicationHooks =
        [
            new PresetApplicationHook { Id = "rig-hook-1", ExecutablePath = @"C:\Apps\PitHouse.exe", CloseOnSwitchAway = true }
        ];

        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(_settings);

        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DisplayDeviceInfo>
            {
                new(DeskMonitorId, @"\\.\DISPLAY1", "MPG341CQPX OLED", "NVIDIA RTX 4070 Ti", true, true),
                new(RigMonitorId, @"\\.\DISPLAY2", "VG34VQL3A", "NVIDIA RTX 4070 Ti", false, false)
            });

        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AudioEndpointInfo>());
    }

    [Fact]
    public async Task SwitchProfileAsync_InvokesCloseHooksOnPreviousPresetAndLaunchHooksOnNewPreset()
    {
        // Arrange
        using var coordinator = new ProfileSwitchCoordinator(
            _displayService,
            _audioDirector,
            _settingsService,
            ProfileMode.Desk,
            _appHookService);

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert
        Assert.True(result);

        Received.InOrder(() =>
        {
            _displayService.ApplySingleDisplayTopologyAsync(RigMonitorId, DeskMonitorId, Arg.Any<CancellationToken>());
            _appHookService.CloseHooksForPresetAsync(_settings.DeskPresets[0], Arg.Any<CancellationToken>());
            _appHookService.LaunchHooksForPresetAsync(_settings.RigPresets[0], Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SwitchToPresetAsync_WithinSameProfile_ClosesOutgoingPresetHooksAndLaunchesIncomingPresetHooks()
    {
        // Arrange
        var preset0 = _settings.DeskPresets[0];
        var preset1 = _settings.DeskPresets[1];
        preset1.TargetMonitorId = DeskMonitorId;
        preset1.ApplicationHooks =
        [
            new PresetApplicationHook { Id = "desk-hook-2", ExecutablePath = @"C:\Apps\CodingIDE.exe" }
        ];

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService,
            _audioDirector,
            _settingsService,
            ProfileMode.Desk,
            _appHookService);

        // Act
        var result = await coordinator.SwitchToPresetAsync(ProfileMode.Desk, 1);

        // Assert
        Assert.True(result);

        Received.InOrder(() =>
        {
            _appHookService.CloseHooksForPresetAsync(preset0, Arg.Any<CancellationToken>());
            _appHookService.LaunchHooksForPresetAsync(preset1, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenHookServiceThrows_ContinuesSwitchGracefully()
    {
        // Arrange
        _appHookService.CloseHooksForPresetAsync(Arg.Any<WorkstationPreset>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Process close failure"));
        _appHookService.LaunchHooksForPresetAsync(Arg.Any<WorkstationPreset>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Process launch failure"));

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService,
            _audioDirector,
            _settingsService,
            ProfileMode.Desk,
            _appHookService);

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert - hardware switch succeeds despite hook error
        Assert.True(result);
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
    }
}
