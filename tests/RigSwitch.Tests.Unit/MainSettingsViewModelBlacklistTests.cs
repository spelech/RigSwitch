namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.App.Services;
using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using Xunit;

public sealed class MainSettingsViewModelBlacklistTests
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsService;
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly IGlobalHotkeyService _hotkeyService;

    public MainSettingsViewModelBlacklistTests()
    {
        _coordinator = Substitute.For<IProfileSwitchCoordinator>();
        _settingsService = Substitute.For<ISettingsStorageService>();
        _displayService = Substitute.For<IDisplayConfigurationService>();
        _audioDirector = Substitute.For<IAudioEndpointDirector>();
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
    }

    [Fact]
    public async Task LoadAsync_ExcludesBlacklistedEndpointsFromAvailableAudioOptions()
    {
        var settings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{SHAKER-DAC-1}"]
        };

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SHAKER-DAC-1}", "Nobsound Shaker", "USB", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{SPEAKERS-1}", "Desktop Speakers", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback)
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
            new TrayIconService(_coordinator, _settingsService));

        await vm.LoadAsync();

        // 1 placeholder ("— Select Audio Device (Unassigned) —") + 1 non-blacklisted device
        Assert.Equal(2, vm.AvailableAudioOptions.Count);
        Assert.Contains(vm.AvailableAudioOptions, opt => opt.Id == "{SPEAKERS-1}");
        Assert.DoesNotContain(vm.AvailableAudioOptions, opt => opt.Id == "{SHAKER-DAC-1}");

        // AudioEndpointsVisibility contains both, with shaker marked blacklisted
        Assert.Equal(2, vm.AudioEndpointsVisibility.Count);
        var shakerItem = vm.AudioEndpointsVisibility.First(e => e.Id == "{SHAKER-DAC-1}");
        Assert.True(shakerItem.IsBlacklisted);
        var spkItem = vm.AudioEndpointsVisibility.First(e => e.Id == "{SPEAKERS-1}");
        Assert.False(spkItem.IsBlacklisted);
    }

    [Fact]
    public async Task BlacklistToggle_UpdatesSettingsAndRefreshesAudioOptions()
    {
        var settings = new UserSettings();
        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SHAKER-DAC-1}", "Nobsound Shaker", "USB", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{SPEAKERS-1}", "Desktop Speakers", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback)
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
            new TrayIconService(_coordinator, _settingsService));

        await vm.LoadAsync();

        var shakerItem = vm.AudioEndpointsVisibility.First(e => e.Id == "{SHAKER-DAC-1}");
        shakerItem.IsBlacklisted = true;

        // Allow async callback to complete
        await Task.Delay(50);

        Assert.Contains("{SHAKER-DAC-1}", settings.BlacklistedAudioEndpointIds);
        Assert.DoesNotContain(vm.AvailableAudioOptions, opt => opt.Id == "{SHAKER-DAC-1}");

        // Now untoggle blacklist
        shakerItem.IsBlacklisted = false;
        await Task.Delay(50);

        Assert.DoesNotContain("{SHAKER-DAC-1}", settings.BlacklistedAudioEndpointIds);
        Assert.Contains(vm.AvailableAudioOptions, opt => opt.Id == "{SHAKER-DAC-1}");
    }

    [Fact]
    public async Task LoadAsync_And_SaveSettingsAsync_RoundTripAudioToggleHotkeyAndEnforcePrimaryAudio()
    {
        var settings = new UserSettings
        {
            AudioToggleHotkey = "Ctrl+Shift+F12",
            EnforcePrimaryAudio = false
        };

        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>()).Returns([]);
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns([]);

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            new TrayIconService(_coordinator, _settingsService));

        await vm.LoadAsync();

        Assert.Equal("Ctrl+Shift+F12", vm.AudioToggleHotkey);
        Assert.False(vm.EnforcePrimaryAudio);

        vm.AudioToggleHotkey = "Alt+F10";
        vm.EnforcePrimaryAudio = true;

        await vm.SaveSettingsAsync();

        await _settingsService.Received().SaveSettingsAsync(Arg.Is<UserSettings>(s =>
            s.AudioToggleHotkey == "Alt+F10" &&
            s.EnforcePrimaryAudio == true));
    }

    [Fact]
    public void RegisterGlobalHotkeys_RegistersAudioToggleHotkey()
    {
        var settings = new UserSettings
        {
            AudioToggleHotkey = "Ctrl+Alt+A"
        };

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            new TrayIconService(_coordinator, _settingsService));

        vm.RegisterGlobalHotkeys(settings);

        _hotkeyService.Received().RegisterHotkey("Ctrl+Alt+A", Arg.Any<Action>());
    }
}
