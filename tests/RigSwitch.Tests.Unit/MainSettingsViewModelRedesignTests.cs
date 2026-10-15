namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.App.Services;
using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using Xunit;

public sealed class MainSettingsViewModelRedesignTests : IDisposable
{
    private readonly IProfileSwitchCoordinator _coordinator = Substitute.For<IProfileSwitchCoordinator>();
    private readonly ISettingsStorageService _settingsService = Substitute.For<ISettingsStorageService>();
    private readonly IDisplayConfigurationService _displayService = Substitute.For<IDisplayConfigurationService>();
    private readonly IAudioEndpointDirector _audioDirector = Substitute.For<IAudioEndpointDirector>();
    private readonly IGlobalHotkeyService _hotkeyService = Substitute.For<IGlobalHotkeyService>();
    private readonly TrayIconService _trayService;

    public MainSettingsViewModelRedesignTests()
    {
        _trayService = new TrayIconService(_coordinator, _settingsService);
    }

    public void Dispose()
    {
        _trayService.Dispose();
    }

    [Fact]
    public void AppVersionText_StartsWithVAndVersionNumbers()
    {
        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            _trayService);

        Assert.Matches(@"^v\d+\.\d+", vm.AppVersionText);
    }

    [Fact]
    public async Task RelevantAudioFilter_HidesUnreferencedNotPresentGhosts_AndKeepsReferencedOnes()
    {
        var settings = new UserSettings
        {
            DeskPresets =
            [
                new WorkstationPreset
                {
                    AudioPriorityIds = ["{PEBBLE-AUX}", "{MONITOR-OLED}"]
                }
            ]
        };

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{MONITOR-OLED}", "MPG341CX OLED", "NVIDIA", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback),
            new("{PEBBLE-AUX}", "Speakers (Pebble V3)", "USB", DevicePresenceState.NotPresent, false, false, AudioDeviceFlow.Playback),
            new("{GHOST-HDMI-1}", "Old TV Audio 2021", "NVIDIA", DevicePresenceState.NotPresent, false, false, AudioDeviceFlow.Playback),
            new("{DISABLED-OCULUS}", "Oculus Virtual Audio", "Oculus", DevicePresenceState.Disabled, false, false, AudioDeviceFlow.Playback)
        };

        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>()).Returns([]);
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            _trayService);

        await vm.LoadAsync();

        Assert.True(vm.IsRelevantFilterSelected);
        var visibleItems = vm.FilteredAudioEndpoints!.Cast<AudioEndpointVisibilityItemViewModel>().ToList();

        Assert.Equal(3, visibleItems.Count);
        Assert.Contains(visibleItems, x => x.Id == "{MONITOR-OLED}");
        Assert.Contains(visibleItems, x => x.Id == "{PEBBLE-AUX}");
        Assert.Contains(visibleItems, x => x.Id == "{DISABLED-OCULUS}");
        Assert.DoesNotContain(visibleItems, x => x.Id == "{GHOST-HDMI-1}");

        // Switching to All Registry Ghosts shows all 4
        vm.SelectAllAudioFilterCommand.Execute(null);
        var allItems = vm.FilteredAudioEndpoints!.Cast<AudioEndpointVisibilityItemViewModel>().ToList();
        Assert.Equal(4, allItems.Count);
    }

    [Fact]
    public async Task AddAndRemoveNickname_MovesDeviceBetweenUnnicknamedAndConfiguredCollections()
    {
        var settings = new UserSettings();
        settings.CustomDeviceNames["MON1"] = "Main Desk OLED";

        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>()).Returns([
            new DisplayDeviceInfo("MON1", @"\\.\DISPLAY1", "MPG341CX OLED", "NVIDIA", true, true),
            new DisplayDeviceInfo("MON2", @"\\.\DISPLAY2", "VG34VQL3A", "NVIDIA", false, false)
        ]);
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns([
            new AudioEndpointInfo("{SPK1}", "Pebble V3", "USB", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback)
        ]);

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            _trayService);

        await vm.LoadAsync();

        Assert.Single(vm.ConfiguredNicknames);
        Assert.Equal("MON1", vm.ConfiguredNicknames[0].DeviceId);
        Assert.DoesNotContain(vm.UnnicknamedDeviceOptions, x => x.Id == "MON1");
        Assert.Contains(vm.UnnicknamedDeviceOptions, x => x.Id == "MON2");
        Assert.Contains(vm.UnnicknamedDeviceOptions, x => x.Id == "{SPK1}");

        // Add nickname for MON2
        vm.SelectedNewNicknameDeviceId = "MON2";
        vm.NewNicknameText = "Sim Rig Ultrawide";
        vm.AddNicknameCommand.Execute(null);

        Assert.Equal(2, vm.ConfiguredNicknames.Count);
        Assert.DoesNotContain(vm.UnnicknamedDeviceOptions, x => x.Id == "MON2");

        // Remove nickname for MON1
        var mon1Item = vm.ConfiguredNicknames.First(x => x.DeviceId == "MON1");
        mon1Item.RemoveCommand.Execute(null);

        Assert.Single(vm.ConfiguredNicknames);
        Assert.Contains(vm.UnnicknamedDeviceOptions, x => x.Id == "MON1");
    }

    [Fact]
    public void PresetConfigurationItemViewModel_MonitorCanvasAndAudioRanking_ManipulatesCorrectly()
    {
        var preset = new WorkstationPreset
        {
            Name = "Desk Daily",
            TargetMonitorIds = ["MON1"],
            AudioPriorityIds = ["{AUD1}", "{AUD2}"]
        };

        var vm = new PresetConfigurationItemViewModel(preset, 0, "DeskGroup", true);
        vm.HydrateHardwareContext(
            displays:
            [
                new DisplayDeviceInfo("MON1", @"\\.\DISPLAY1", "Desk OLED", "GPU", true, true, true, true, 0, 0, 3440, 1440),
                new DisplayDeviceInfo("MON2", @"\\.\DISPLAY2", "Side Portrait", "GPU", true, false, false, false, 3440, 0, 1920, 1080)
            ],
            audioEndpoints:
            [
                new AudioEndpointInfo("{AUD1}", "Pebble V3", "USB", DevicePresenceState.NotPresent, false, false, AudioDeviceFlow.Playback),
                new AudioEndpointInfo("{AUD2}", "OLED Speakers", "NVIDIA", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback),
                new AudioEndpointInfo("{AUD3}", "Headset", "USB", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback)
            ],
            customNames: new Dictionary<string, string>(),
            cachedNames: new Dictionary<string, string>(),
            blacklistedIds: []);

        Assert.Single(vm.StageMonitors);
        Assert.Single(vm.UnassignedMonitors);
        Assert.Equal("MON2", vm.UnassignedMonitors[0].MonitorId);

        // Add MON2 from tray to stage
        vm.AddMonitorToStage(vm.UnassignedMonitors[0]);
        Assert.Equal(2, vm.StageMonitors.Count);
        Assert.Empty(vm.UnassignedMonitors);

        // Set MON2 as primary -> normalizes MON2 to (0,0) and shifts MON1 to (-3440, 0)
        var mon2 = vm.StageMonitors.First(m => m.MonitorId == "MON2");
        vm.SetPrimaryMonitor(mon2);
        Assert.True(mon2.IsPrimary);
        Assert.Equal(0, mon2.PositionX);
        Assert.Equal("MON2", vm.TargetMonitorId);

        // Move AUD2 up to #1
        var aud2Item = vm.AudioRanking.First(a => a.EndpointId == "{AUD2}");
        vm.MoveAudioUp(aud2Item);
        Assert.Equal("{AUD2", vm.AudioRanking[0].EndpointId[..5]);
        Assert.Equal("{AUD2}", vm.PrimaryAudioId);
    }
}
