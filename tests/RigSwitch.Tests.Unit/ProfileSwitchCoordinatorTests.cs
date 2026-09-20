namespace RigSwitch.Tests.Unit;

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Events;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Core.Services;
using Xunit;

public sealed class ProfileSwitchCoordinatorTests
{
    private const string DeskMonitorId = "MSI4DD0";
    private const string RigMonitorId = "AUS3438";
    private const string DeskPrimaryAudioId = "{D2B56B79-F353-4FB0-81B6-ECEF8E95E57A}";
    private const string DeskFallbackAudioId = "{EE0329B0-FA5C-4731-B0F1-8E188AB441DC}";
    private const string RigPrimaryAudioId = "{3CF792EA-E074-4D66-A8A1-9C4F1A8B204F}";

    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly ISettingsStorageService _settingsService;
    private readonly UserSettings _defaultSettings;

    public ProfileSwitchCoordinatorTests()
    {
        _displayService = Substitute.For<IDisplayConfigurationService>();
        _audioDirector = Substitute.For<IAudioEndpointDirector>();
        _settingsService = Substitute.For<ISettingsStorageService>();

        _defaultSettings = new UserSettings
        {
            LastActiveProfile = ProfileMode.Desk,
            DeskMonitorId = DeskMonitorId,
            RigMonitorId = RigMonitorId,
            DeskPrimaryAudioId = DeskPrimaryAudioId,
            DeskFallbackAudioId = DeskFallbackAudioId,
            RigPrimaryAudioId = RigPrimaryAudioId,
            HiddenAudioEndpointIds = ["{HIDDEN-1}", "{HIDDEN-2}"]
        };

        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(_defaultSettings);

        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DisplayDeviceInfo>
            {
                new(DeskMonitorId, @"\\.\DISPLAY1", "MPG341CQPX OLED", "NVIDIA RTX 4070 Ti", true, true),
                new(RigMonitorId, @"\\.\DISPLAY2", "VG34VQL3A", "NVIDIA RTX 4070 Ti", false, false)
            });

        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AudioEndpointInfo>
            {
                new(DeskPrimaryAudioId, "Speakers (Pebble V3)", "Realtek Audio", DevicePresenceState.Active, true, false),
                new(DeskFallbackAudioId, "MSI MPG341CQPX", "NVIDIA High Definition Audio", DevicePresenceState.Active, false, false),
                new(RigPrimaryAudioId, "VG34VQL3A", "NVIDIA High Definition Audio", DevicePresenceState.Active, false, false)
            });
    }

    [Fact]
    public void Constructor_WithNullArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProfileSwitchCoordinator(null!, _audioDirector, _settingsService));
        Assert.Throws<ArgumentNullException>(() =>
            new ProfileSwitchCoordinator(_displayService, null!, _settingsService));
        Assert.Throws<ArgumentNullException>(() =>
            new ProfileSwitchCoordinator(_displayService, _audioDirector, null!));
    }

    [Fact]
    public async Task SwitchProfileAsync_DeskToRig_ExecutesDisplayThenAudio()
    {
        // Arrange
        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        ProfileChangedEventArgs? receivedEvent = null;
        coordinator.ProfileChanged += (_, args) => receivedEvent = args;

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert
        Assert.True(result);
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
        Assert.NotNull(receivedEvent);
        Assert.Equal(ProfileMode.Desk, receivedEvent.PreviousProfile);
        Assert.Equal(ProfileMode.SimRig, receivedEvent.NewProfile);
        Assert.True(receivedEvent.Success);
        Assert.Null(receivedEvent.ErrorMessage);

        Received.InOrder(() =>
        {
            _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>());
            _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>());
            _displayService.ApplySingleDisplayTopologyAsync(RigMonitorId, DeskMonitorId, Arg.Any<CancellationToken>());
            _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>());
            _audioDirector.SetDefaultPlaybackEndpointAsync(RigPrimaryAudioId, Arg.Any<CancellationToken>());
            _audioDirector.SyncHiddenEndpointsAsync(_defaultSettings.HiddenAudioEndpointIds, Arg.Any<CancellationToken>());
            _settingsService.SaveSettingsAsync(
                Arg.Is<UserSettings>(s => s.LastActiveProfile == ProfileMode.SimRig),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenRigMonitorMissing_AbortsSafelyWithoutDisablingDeskDisplay()
    {
        // Arrange - Rig monitor is unplugged/missing from enumerated displays
        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DisplayDeviceInfo>
            {
                new(DeskMonitorId, @"\\.\DISPLAY1", "MPG341CQPX OLED", "NVIDIA RTX 4070 Ti", true, true)
            });

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        ProfileChangedEventArgs? receivedEvent = null;
        coordinator.ProfileChanged += (_, args) => receivedEvent = args;

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert
        Assert.False(result);
        Assert.Equal(ProfileMode.Desk, coordinator.CurrentProfile);

        await _displayService.DidNotReceiveWithAnyArgs()
            .ApplySingleDisplayTopologyAsync(default!, default, default);
        await _audioDirector.DidNotReceiveWithAnyArgs()
            .SetDefaultPlaybackEndpointAsync(default!, default);
        await _settingsService.DidNotReceiveWithAnyArgs()
            .SaveSettingsAsync(default!, default);

        Assert.NotNull(receivedEvent);
        Assert.Equal(ProfileMode.Desk, receivedEvent.PreviousProfile);
        Assert.Equal(ProfileMode.SimRig, receivedEvent.NewProfile);
        Assert.False(receivedEvent.Success);
        Assert.False(string.IsNullOrWhiteSpace(receivedEvent.ErrorMessage));
        Assert.Contains(RigMonitorId, receivedEvent.ErrorMessage);
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenDeskMonitorMissing_AbortsSafelyWithoutDisablingRigDisplay()
    {
        // Arrange - SimRig is currently active, Desk monitor is unplugged/missing
        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DisplayDeviceInfo>
            {
                new(RigMonitorId, @"\\.\DISPLAY2", "VG34VQL3A", "NVIDIA RTX 4070 Ti", true, true)
            });

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.SimRig);

        ProfileChangedEventArgs? receivedEvent = null;
        coordinator.ProfileChanged += (_, args) => receivedEvent = args;

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.Desk);

        // Assert
        Assert.False(result);
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);

        await _displayService.DidNotReceiveWithAnyArgs()
            .ApplySingleDisplayTopologyAsync(default!, default, default);
        await _audioDirector.DidNotReceiveWithAnyArgs()
            .SetDefaultPlaybackEndpointAsync(default!, default);
        await _settingsService.DidNotReceiveWithAnyArgs()
            .SaveSettingsAsync(default!, default);

        Assert.NotNull(receivedEvent);
        Assert.Equal(ProfileMode.SimRig, receivedEvent.PreviousProfile);
        Assert.Equal(ProfileMode.Desk, receivedEvent.NewProfile);
        Assert.False(receivedEvent.Success);
        Assert.False(string.IsNullOrWhiteSpace(receivedEvent.ErrorMessage));
        Assert.Contains(DeskMonitorId, receivedEvent.ErrorMessage);
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenCancellationRequested_UnwindsCleanly()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        var eventFired = false;
        coordinator.ProfileChanged += (_, _) => eventFired = true;

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.SwitchProfileAsync(ProfileMode.SimRig, cts.Token));

        Assert.Equal(ProfileMode.Desk, coordinator.CurrentProfile);
        Assert.False(eventFired);

        // Verify coordinator semaphore was unwound and subsequent call can succeed
        var secondResult = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);
        Assert.True(secondResult);
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
    }

    [Fact]
    public async Task SwitchProfileAsync_DeskProfile_ResolvesPebbleWithMsiFallback()
    {
        // Arrange 1: Pebble is Active -> routes to Pebble
        using var coordinator1 = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.SimRig);

        var result1 = await coordinator1.SwitchProfileAsync(ProfileMode.Desk);

        Assert.True(result1);
        await _audioDirector.Received(1)
            .SetDefaultPlaybackEndpointAsync(DeskPrimaryAudioId, Arg.Any<CancellationToken>());

        // Arrange 2: Pebble is Unplugged, MSI OLED is Active -> routes to MSI fallback
        _audioDirector.ClearReceivedCalls();
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AudioEndpointInfo>
            {
                new(DeskPrimaryAudioId, "Speakers (Pebble V3)", "Realtek Audio", DevicePresenceState.Unplugged, false, false),
                new(DeskFallbackAudioId, "MSI MPG341CQPX", "NVIDIA High Definition Audio", DevicePresenceState.Active, false, false)
            });

        using var coordinator2 = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.SimRig);

        var result2 = await coordinator2.SwitchProfileAsync(ProfileMode.Desk);

        Assert.True(result2);
        await _audioDirector.Received(1)
            .SetDefaultPlaybackEndpointAsync(DeskFallbackAudioId, Arg.Any<CancellationToken>());

        // Arrange 3: Neither Pebble nor MSI is Active, but USB Headset is Active -> routes to first active
        const string headsetId = "{HEADSET-ACTIVE-GUID}";
        _audioDirector.ClearReceivedCalls();
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AudioEndpointInfo>
            {
                new(DeskPrimaryAudioId, "Speakers (Pebble V3)", "Realtek Audio", DevicePresenceState.Unplugged, false, false),
                new(DeskFallbackAudioId, "MSI MPG341CQPX", "NVIDIA High Definition Audio", DevicePresenceState.Disabled, false, false),
                new(headsetId, "USB Headset", "USB Audio", DevicePresenceState.Active, false, false)
            });

        using var coordinator3 = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.SimRig);

        var result3 = await coordinator3.SwitchProfileAsync(ProfileMode.Desk);

        Assert.True(result3);
        await _audioDirector.Received(1)
            .SetDefaultPlaybackEndpointAsync(headsetId, Arg.Any<CancellationToken>());

        // Arrange 4: No endpoints active -> attempts primary DeskPrimaryAudioId
        _audioDirector.ClearReceivedCalls();
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AudioEndpointInfo>
            {
                new(DeskPrimaryAudioId, "Speakers (Pebble V3)", "Realtek Audio", DevicePresenceState.Unplugged, false, false),
                new(DeskFallbackAudioId, "MSI MPG341CQPX", "NVIDIA High Definition Audio", DevicePresenceState.Disabled, false, false)
            });

        using var coordinator4 = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.SimRig);

        var result4 = await coordinator4.SwitchProfileAsync(ProfileMode.Desk);

        Assert.True(result4);
        await _audioDirector.Received(1)
            .SetDefaultPlaybackEndpointAsync(DeskPrimaryAudioId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchProfileAsync_UpdatesLastActiveProfileAndFiresEvent()
    {
        // Arrange
        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        ProfileChangedEventArgs? firedEvent = null;
        coordinator.ProfileChanged += (_, e) => firedEvent = e;

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert
        Assert.True(result);
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
        Assert.NotNull(firedEvent);
        Assert.Equal(ProfileMode.Desk, firedEvent.PreviousProfile);
        Assert.Equal(ProfileMode.SimRig, firedEvent.NewProfile);
        Assert.True(firedEvent.Success);
        Assert.Null(firedEvent.ErrorMessage);

        await _settingsService.Received(1).SaveSettingsAsync(
            Arg.Is<UserSettings>(s => s.LastActiveProfile == ProfileMode.SimRig),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenDisplayServiceThrows_FiresFailureEventAndReturnsFalse()
    {
        // Arrange
        _displayService.ApplySingleDisplayTopologyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("CCD SetDisplayConfig failed with error 87"));

        using var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        ProfileChangedEventArgs? firedEvent = null;
        coordinator.ProfileChanged += (_, e) => firedEvent = e;

        // Act
        var result = await coordinator.SwitchProfileAsync(ProfileMode.SimRig);

        // Assert
        Assert.False(result);
        Assert.Equal(ProfileMode.Desk, coordinator.CurrentProfile);
        Assert.NotNull(firedEvent);
        Assert.False(firedEvent.Success);
        Assert.Equal(ProfileMode.Desk, firedEvent.PreviousProfile);
        Assert.Equal(ProfileMode.SimRig, firedEvent.NewProfile);
        Assert.Equal("CCD SetDisplayConfig failed with error 87", firedEvent.ErrorMessage);
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var coordinator = new ProfileSwitchCoordinator(
            _displayService, _audioDirector, _settingsService, ProfileMode.Desk);
        coordinator.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => coordinator.SwitchProfileAsync(ProfileMode.SimRig));
    }

    [Fact]
    public async Task SwitchToPresetAsync_WithinSameEnvironmentMode_SwitchesPresetAndPersistsIndex()
    {
        // Arrange
        _defaultSettings.DeskPresets[1].TargetMonitorId = DeskMonitorId;
        _defaultSettings.DeskPresets[1].PrimaryAudioId = DeskPrimaryAudioId;

        using var coordinator = new ProfileSwitchCoordinator(_displayService, _audioDirector, _settingsService, ProfileMode.Desk);
        ProfileChangedEventArgs? received = null;
        coordinator.ProfileChanged += (_, e) => received = e;

        // Act
        var result = await coordinator.SwitchToPresetAsync(ProfileMode.Desk, 1);

        // Assert
        Assert.True(result);
        Assert.Equal(ProfileMode.Desk, coordinator.CurrentProfile);
        Assert.Equal(1, coordinator.CurrentPresetIndex);
        Assert.Equal(_defaultSettings.DeskPresets[1], coordinator.CurrentPreset);
        Assert.NotNull(received);
        Assert.Equal(_defaultSettings.DeskPresets[1], received.ActivePreset);
        await _settingsService.Received(1).SaveSettingsAsync(
            Arg.Is<UserSettings>(s => s.ActiveDeskPresetIndex == 1 && s.LastActiveProfile == ProfileMode.Desk),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchToPresetAsync_ToOppositeEnvironmentMode_SwitchesProfileAndPreset()
    {
        // Arrange
        _defaultSettings.RigPresets[2].TargetMonitorId = RigMonitorId;
        _defaultSettings.RigPresets[2].PrimaryAudioId = RigPrimaryAudioId;

        using var coordinator = new ProfileSwitchCoordinator(_displayService, _audioDirector, _settingsService, ProfileMode.Desk);
        ProfileChangedEventArgs? received = null;
        coordinator.ProfileChanged += (_, e) => received = e;

        // Act
        var result = await coordinator.SwitchToPresetAsync(ProfileMode.SimRig, 2);

        // Assert
        Assert.True(result);
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
        Assert.Equal(2, coordinator.CurrentPresetIndex);
        Assert.Equal(_defaultSettings.RigPresets[2], coordinator.CurrentPreset);
        Assert.NotNull(received);
        Assert.Equal(ProfileMode.Desk, received.PreviousProfile);
        Assert.Equal(ProfileMode.SimRig, received.NewProfile);
        Assert.Equal(_defaultSettings.RigPresets[2], received.ActivePreset);
        await _settingsService.Received(1).SaveSettingsAsync(
            Arg.Is<UserSettings>(s => s.ActiveRigPresetIndex == 2 && s.LastActiveProfile == ProfileMode.SimRig),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchToPresetAsync_WhenTargetMonitorEmptyOrDisconnected_HaltsAtSafetyGate()
    {
        // Arrange 1: Empty monitor ID
        _defaultSettings.DeskPresets[1].TargetMonitorId = string.Empty;
        using var coordinator = new ProfileSwitchCoordinator(_displayService, _audioDirector, _settingsService, ProfileMode.Desk);
        ProfileChangedEventArgs? emptyEvent = null;
        coordinator.ProfileChanged += (_, e) => emptyEvent = e;

        var emptyResult = await coordinator.SwitchToPresetAsync(ProfileMode.Desk, 1);

        Assert.False(emptyResult);
        Assert.False(emptyEvent!.Success);
        Assert.Contains("No target display configured", emptyEvent.ErrorMessage);

        // Arrange 2: Disconnected monitor ID
        _defaultSettings.DeskPresets[2].TargetMonitorId = "DISCONNECTED_DISP";
        ProfileChangedEventArgs? missingEvent = null;
        coordinator.ProfileChanged += (_, e) => missingEvent = e;

        var missingResult = await coordinator.SwitchToPresetAsync(ProfileMode.Desk, 2);

        Assert.False(missingResult);
        Assert.False(missingEvent!.Success);
        Assert.Contains("DISCONNECTED_DISP", missingEvent.ErrorMessage);
        await _settingsService.DidNotReceiveWithAnyArgs().SaveSettingsAsync(default!, default);
    }

    [Fact]
    public async Task SwitchToPresetAsync_AudioRouting_UsesPresetPrimaryAndFallbackAudioEndpoints()
    {
        // Arrange
        const string customPrimary = "{CUSTOM-PRIMARY-GUID}";
        const string customFallback = "{CUSTOM-FALLBACK-GUID}";
        _defaultSettings.DeskPresets[1].TargetMonitorId = DeskMonitorId;
        _defaultSettings.DeskPresets[1].PrimaryAudioId = customPrimary;
        _defaultSettings.DeskPresets[1].FallbackAudioId = customFallback;

        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns([
                new AudioEndpointInfo(customPrimary, "Primary Mic/Spk", "Audio", DevicePresenceState.Unplugged, false, false),
                new AudioEndpointInfo(customFallback, "Fallback Spk", "Audio", DevicePresenceState.Active, false, false)
            ]);

        using var coordinator = new ProfileSwitchCoordinator(_displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        // Act
        var result = await coordinator.SwitchToPresetAsync(ProfileMode.Desk, 1);

        // Assert
        Assert.True(result);
        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync(customFallback, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SetCurrentPreset_SyncsStateWithoutHardwareSwitch()
    {
        // Arrange
        using var coordinator = new ProfileSwitchCoordinator(_displayService, _audioDirector, _settingsService, ProfileMode.Desk);

        // Act
        coordinator.SetCurrentPreset(ProfileMode.SimRig, 2);

        // Assert
        Assert.Equal(ProfileMode.SimRig, coordinator.CurrentProfile);
        Assert.Equal(2, coordinator.CurrentPresetIndex);
        _displayService.DidNotReceiveWithAnyArgs().ApplySingleDisplayTopologyAsync(default!, default, default);
        _audioDirector.DidNotReceiveWithAnyArgs().SetDefaultPlaybackEndpointAsync(default!, default);
    }
}
