namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using Xunit;

public sealed class MainSettingsViewModelOfflineDeviceTests
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsService;
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly IGlobalHotkeyService _hotkeyService;

    public MainSettingsViewModelOfflineDeviceTests()
    {
        _coordinator = Substitute.For<IProfileSwitchCoordinator>();
        _settingsService = Substitute.For<ISettingsStorageService>();
        _displayService = Substitute.For<IDisplayConfigurationService>();
        _audioDirector = Substitute.For<IAudioEndpointDirector>();
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
    }

    [Fact]
    public void PopulateMonitorTiles_WhenPresetContainsOfflineMonitor_CreatesOfflineTileInCorrectPile()
    {
        var settings = new UserSettings();
        var deskPreset = settings.GetActivePreset(ProfileMode.Desk);
        deskPreset.TargetMonitorIds = ["ACTIVE_MON1", "OFFLINE_MON2"];
        settings.CachedDeviceNames["OFFLINE_MON2"] = "Sim Rig Secondary";

        var displays = new List<DisplayDeviceInfo>
        {
            new("ACTIVE_MON1", @"\\.\DISPLAY1", "Main Desk", "NVIDIA", true, true)
        };

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            new RigSwitch.App.Services.TrayIconService(_coordinator, _settingsService));

        vm.PopulateMonitorTiles(displays, settings);

        Assert.Equal(2, vm.MonitorTiles.Count);
        var activeTile = vm.MonitorTiles.First(t => t.MonitorId == "ACTIVE_MON1");
        Assert.True(activeTile.IsActive);
        Assert.Equal(MonitorPileAssignment.Desk, activeTile.Pile);

        var offlineTile = vm.MonitorTiles.First(t => t.MonitorId == "OFFLINE_MON2");
        Assert.False(offlineTile.IsActive);
        Assert.Equal("Sim Rig Secondary", offlineTile.FriendlyName);
        Assert.Equal(MonitorPileAssignment.Desk, offlineTile.Pile);
    }

    [Fact]
    public async Task SyncMonitorTilesToActivePresets_RetainsOfflineTilesInPreset()
    {
        var settings = new UserSettings();
        var deskPreset = settings.GetActivePreset(ProfileMode.Desk);
        deskPreset.TargetMonitorIds = ["MON_ONLINE", "MON_OFFLINE"];

        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>()).Returns([]);
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns([]);

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            new RigSwitch.App.Services.TrayIconService(_coordinator, _settingsService));

        await vm.LoadAsync();

        vm.MonitorTiles.Clear();
        vm.MonitorTiles.Add(new DisplayMonitorTileViewModel(1, "MON_ONLINE", "Online", MonitorPileAssignment.Desk, true, true));
        vm.MonitorTiles.Add(new DisplayMonitorTileViewModel(2, "MON_OFFLINE", "Offline", MonitorPileAssignment.Desk, false, false));

        vm.SyncMonitorTilesToActivePresets();

        var activeDeskPreset = settings.GetActivePreset(ProfileMode.Desk);
        Assert.Contains("MON_OFFLINE", activeDeskPreset.TargetMonitorIds);
        Assert.Equal(["MON_ONLINE", "MON_OFFLINE"], activeDeskPreset.TargetMonitorIds);
    }
}
