namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Core.Services;
using Xunit;

public sealed class ProfileSwitchCoordinatorAudioToggleTests
{
    private readonly IDisplayConfigurationService _displayManager = Substitute.For<IDisplayConfigurationService>();
    private readonly IAudioEndpointDirector _audioDirector = Substitute.For<IAudioEndpointDirector>();
    private readonly ISettingsStorageService _storageService = Substitute.For<ISettingsStorageService>();

    [Fact]
    public async Task SwitchProfile_FiltersOutBlacklistedAudioEndpoints()
    {
        var settings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{SHAKER-DAC-1}"],
            DeskPresets =
            [
                new WorkstationPreset
                {
                    PrimaryAudioId = "{OFFLINE-ID}",
                    FallbackAudioId = string.Empty,
                    TargetMonitorIds = ["MONITOR-1"]
                }
            ]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _displayManager.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns([new DisplayDeviceInfo("MONITOR-1", @"\\.\DISPLAY1", "OLED", "GPU", true, true)]);

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SHAKER-DAC-1}", "Nobsound USB DAC", "USB", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{SPEAKERS}", "Pebble V3", "Realtek", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback)
        };
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        var coordinator = new ProfileSwitchCoordinator(_displayManager, _audioDirector, _storageService);
        await coordinator.SwitchProfileAsync(ProfileMode.Desk);

        // Crucial: Must select Pebble V3, NEVER the blacklisted shaker DAC!
        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
        await _audioDirector.DidNotReceive().SetDefaultPlaybackEndpointAsync("{SHAKER-DAC-1}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleAudioOutputMode_SwitchesBetweenPrimaryAndSecondary()
    {
        var settings = new UserSettings
        {
            ActiveAudioOutputMode = AudioOutputMode.Primary,
            DeskPresets =
            [
                new WorkstationPreset
                {
                    PrimaryAudioId = "{SPEAKERS}",
                    SecondaryAudioId = "{SONAR-HEADSET}",
                    PrimaryMicrophoneId = "{DESK-MIC}",
                    SecondaryMicrophoneId = "{HEADSET-MIC}"
                }
            ]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SPEAKERS}", "Pebble V3", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback),
            new("{SONAR-HEADSET}", "SteelSeries Sonar Gaming", "SteelSeries", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{DESK-MIC}", "Desk Mic", "USB", DevicePresenceState.Active, true, false, AudioDeviceFlow.Capture),
            new("{HEADSET-MIC}", "Headset Mic", "SteelSeries", DevicePresenceState.Active, false, false, AudioDeviceFlow.Capture)
        };
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        var coordinator = new ProfileSwitchCoordinator(_displayManager, _audioDirector, _storageService);

        var newMode = await coordinator.ToggleAudioOutputModeAsync();
        Assert.Equal(AudioOutputMode.Secondary, newMode);
        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{SONAR-HEADSET}", Arg.Any<CancellationToken>());
        await _audioDirector.Received(1).SetDefaultCaptureEndpointAsync("{HEADSET-MIC}", Arg.Any<CancellationToken>());
        await _storageService.Received().SaveSettingsAsync(Arg.Is<UserSettings>(s => s.ActiveAudioOutputMode == AudioOutputMode.Secondary), Arg.Any<CancellationToken>());

        var toggledBackMode = await coordinator.ToggleAudioOutputModeAsync();
        Assert.Equal(AudioOutputMode.Primary, toggledBackMode);
        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
        await _audioDirector.Received(1).SetDefaultCaptureEndpointAsync("{DESK-MIC}", Arg.Any<CancellationToken>());
        await _storageService.Received().SaveSettingsAsync(Arg.Is<UserSettings>(s => s.ActiveAudioOutputMode == AudioOutputMode.Primary), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchProfile_WhenInSecondaryMode_SelectsSecondaryEndpoints()
    {
        var settings = new UserSettings
        {
            ActiveAudioOutputMode = AudioOutputMode.Secondary,
            DeskPresets =
            [
                new WorkstationPreset
                {
                    PrimaryAudioId = "{SPEAKERS}",
                    SecondaryAudioId = "{SONAR-HEADSET}",
                    TargetMonitorIds = ["MONITOR-1"]
                }
            ]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _displayManager.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns([new DisplayDeviceInfo("MONITOR-1", @"\\.\DISPLAY1", "OLED", "GPU", true, true)]);

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SPEAKERS}", "Pebble V3", "Realtek", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{SONAR-HEADSET}", "SteelSeries Sonar Gaming", "SteelSeries", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback)
        };
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        var coordinator = new ProfileSwitchCoordinator(_displayManager, _audioDirector, _storageService);
        await coordinator.SwitchProfileAsync(ProfileMode.Desk);

        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{SONAR-HEADSET}", Arg.Any<CancellationToken>());
        await _audioDirector.DidNotReceive().SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchProfile_WhenMicrophoneNotConfigured_DoesNotAlterDefaultCaptureEndpoint()
    {
        var settings = new UserSettings
        {
            DeskPresets =
            [
                new WorkstationPreset
                {
                    PrimaryAudioId = "{SPEAKERS}",
                    PrimaryMicrophoneId = string.Empty,
                    FallbackMicrophoneId = string.Empty,
                    SecondaryMicrophoneId = string.Empty,
                    TargetMonitorIds = ["MONITOR-1"]
                }
            ]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _displayManager.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns([new DisplayDeviceInfo("MONITOR-1", @"\\.\DISPLAY1", "OLED", "GPU", true, true)]);

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SPEAKERS}", "Pebble V3", "Realtek", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{ACTIVE-MIC}", "USB Microphone", "Realtek", DevicePresenceState.Active, false, false, AudioDeviceFlow.Capture)
        };
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        var coordinator = new ProfileSwitchCoordinator(_displayManager, _audioDirector, _storageService);
        await coordinator.SwitchProfileAsync(ProfileMode.Desk);

        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
        await _audioDirector.DidNotReceive().SetDefaultCaptureEndpointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleAudioOutputMode_WhenMicrophoneNotConfigured_DoesNotAlterDefaultCaptureEndpoint()
    {
        var settings = new UserSettings
        {
            ActiveAudioOutputMode = AudioOutputMode.Primary,
            DeskPresets =
            [
                new WorkstationPreset
                {
                    PrimaryAudioId = "{SPEAKERS}",
                    SecondaryAudioId = "{SONAR-HEADSET}",
                    PrimaryMicrophoneId = string.Empty,
                    FallbackMicrophoneId = string.Empty,
                    SecondaryMicrophoneId = string.Empty
                }
            ]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SPEAKERS}", "Pebble V3", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback),
            new("{SONAR-HEADSET}", "SteelSeries Sonar Gaming", "SteelSeries", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{ACTIVE-MIC}", "USB Microphone", "Realtek", DevicePresenceState.Active, false, false, AudioDeviceFlow.Capture)
        };
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        var coordinator = new ProfileSwitchCoordinator(_displayManager, _audioDirector, _storageService);
        await coordinator.ToggleAudioOutputModeAsync();

        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{SONAR-HEADSET}", Arg.Any<CancellationToken>());
        await _audioDirector.DidNotReceive().SetDefaultCaptureEndpointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchProfile_WhenPrimaryIsNotPresent_NotifiesIntentionalSwitchWithResolvedFallback()
    {
        var settings = new UserSettings
        {
            ActiveAudioOutputMode = AudioOutputMode.Primary,
            DeskPresets =
            [
                new WorkstationPreset
                {
                    PrimaryAudioId = "{PEBBLE-USB}",
                    FallbackAudioId = "{MONITOR-OLED}",
                    TargetMonitorIds = ["MONITOR-1"]
                }
            ]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _displayManager.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns([new DisplayDeviceInfo("MONITOR-1", @"\\.\DISPLAY1", "OLED", "GPU", true, true)]);

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{PEBBLE-USB}", "Pebble V3", "USB", DevicePresenceState.NotPresent, false, false, AudioDeviceFlow.Playback),
            new("{MONITOR-OLED}", "MPG341CX OLED", "NVIDIA", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback)
        };
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        var notifiedEndpoints = new List<string>();
        var coordinator = new ProfileSwitchCoordinator(
            _displayManager,
            _audioDirector,
            _storageService,
            onIntentionalAudioSwitch: notifiedEndpoints.Add);

        await coordinator.SwitchProfileAsync(ProfileMode.Desk);

        Assert.Single(notifiedEndpoints);
        Assert.Equal("{MONITOR-OLED}", notifiedEndpoints[0]);
    }

    [Fact]
    public async Task SwitchProfile_WhenMonitorPlacementsConfigured_ForwardsPlacementsAndPerMonitorHdr()
    {
        var placements = new List<MonitorPlacement>
        {
            new("MONITOR-1", 0, 0, 3440, 1440, true, PresetHdrMode.Enable),
            new("MONITOR-2", 3440, 0, 1920, 1080, false, PresetHdrMode.Disable)
        };
        var settings = new UserSettings
        {
            DeskPresets =
            [
                new WorkstationPreset
                {
                    TargetMonitorIds = ["MONITOR-1", "MONITOR-2"],
                    MonitorPlacements = placements
                }
            ]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _displayManager.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns([
                new DisplayDeviceInfo("MONITOR-1", @"\\.\DISPLAY1", "OLED", "GPU", true, true),
                new DisplayDeviceInfo("MONITOR-2", @"\\.\DISPLAY2", "Side", "GPU", true, false)
            ]);
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns([]);

        var coordinator = new ProfileSwitchCoordinator(_displayManager, _audioDirector, _storageService);
        var result = await coordinator.SwitchProfileAsync(ProfileMode.Desk);

        Assert.True(result);
        await _displayManager.Received(1).ApplyDisplayTopologyAsync(
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 2 && ids[0] == "MONITOR-1"),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Is<IReadOnlyList<MonitorPlacement>?>(p => p != null && p.Count == 2),
            Arg.Any<CancellationToken>());
        await _displayManager.Received(1).SetHdrStateAsync("MONITOR-1", true, Arg.Any<CancellationToken>());
        await _displayManager.Received(1).SetHdrStateAsync("MONITOR-2", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleAudioOutputMode_WithRankedPriorityList_CyclesAcrossActiveEndpoints()
    {
        var settings = new UserSettings
        {
            DeskPresets =
            [
                new WorkstationPreset
                {
                    AudioPriorityIds = ["{OFFLINE-AUX}", "{SPEAKERS}", "{HEADSET}", "{MONITOR-AUDIO}"]
                }
            ]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{OFFLINE-AUX}", "Pebble V3 (AUX)", "USB", DevicePresenceState.NotPresent, false, false, AudioDeviceFlow.Playback),
            new("{SPEAKERS}", "Desk Speakers", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback),
            new("{HEADSET}", "Sonar Headset", "SteelSeries", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{MONITOR-AUDIO}", "OLED Audio", "NVIDIA", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback)
        };
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        var coordinator = new ProfileSwitchCoordinator(_displayManager, _audioDirector, _storageService);
        await coordinator.ToggleAudioOutputModeAsync();

        // Since {SPEAKERS} is currently IsDefaultPlayback=true, cycling advances to {HEADSET}
        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{HEADSET}", Arg.Any<CancellationToken>());
    }
}

