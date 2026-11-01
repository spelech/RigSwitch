namespace RigSwitch.Tests.Unit;

using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using Xunit;

public sealed class AudioEndpointFilteringTests
{
    [Fact]
    public void FilteredAudioEndpoints_FiltersBySearchText()
    {
        var item1 = new AudioEndpointVisibilityItemViewModel("id1", "SteelSeries Sonar Gaming", "SteelSeries", true, null, DevicePresenceState.Active);
        var item2 = new AudioEndpointVisibilityItemViewModel("id2", "Realtek High Definition", "Realtek", true, null, DevicePresenceState.Active);
        var item3 = new AudioEndpointVisibilityItemViewModel("id3", "Oculus Virtual Audio", "Oculus", false, null, DevicePresenceState.Disabled);

        var list = new List<AudioEndpointVisibilityItemViewModel> { item1, item2, item3 };

        // Predicate matching helper
        var filtered = list.Where(item => MainSettingsViewModel.MatchesAudioFilter(item, "sonar", "All")).ToList();
        Assert.Single(filtered);
        Assert.Equal("id1", filtered[0].Id);
    }

    [Fact]
    public void FilteredAudioEndpoints_FiltersByPresenceState()
    {
        var item1 = new AudioEndpointVisibilityItemViewModel("id1", "Speakers", "Realtek", true, null, DevicePresenceState.Active);
        var item2 = new AudioEndpointVisibilityItemViewModel("id2", "Headset", "Corsair", false, null, DevicePresenceState.Disabled);
        var item3 = new AudioEndpointVisibilityItemViewModel("id3", "Monitor Audio", "NVIDIA", false, null, DevicePresenceState.Unplugged);

        var list = new List<AudioEndpointVisibilityItemViewModel> { item1, item2, item3 };

        var activeOnly = list.Where(item => MainSettingsViewModel.MatchesAudioFilter(item, string.Empty, "Active")).ToList();
        Assert.Single(activeOnly);
        Assert.Equal("id1", activeOnly[0].Id);

        var inactiveOnly = list.Where(item => MainSettingsViewModel.MatchesAudioFilter(item, string.Empty, "Inactive")).ToList();
        Assert.Equal(2, inactiveOnly.Count);
        Assert.Contains(inactiveOnly, x => x.Id == "id2");
        Assert.Contains(inactiveOnly, x => x.Id == "id3");

        var disabledUnplugged = list.Where(item => MainSettingsViewModel.MatchesAudioFilter(item, string.Empty, "Disabled / Unplugged")).ToList();
        Assert.Equal(2, disabledUnplugged.Count);

        var disabledOnly = list.Where(item => MainSettingsViewModel.MatchesAudioFilter(item, string.Empty, "Disabled")).ToList();
        Assert.Single(disabledOnly);
        Assert.Equal("id2", disabledOnly[0].Id);

        var unpluggedOnly = list.Where(item => MainSettingsViewModel.MatchesAudioFilter(item, string.Empty, "Unplugged")).ToList();
        Assert.Single(unpluggedOnly);
        Assert.Equal("id3", unpluggedOnly[0].Id);
    }

    [Fact]
    public void FilterProperties_TwoWaySelection_UpdatesAudioFilterSelectionAndNotifies()
    {
        var settingsStorage = NSubstitute.Substitute.For<RigSwitch.Core.Interfaces.ISettingsStorageService>();
        var coordinator = NSubstitute.Substitute.For<RigSwitch.Core.Interfaces.IProfileSwitchCoordinator>();
        var displayService = NSubstitute.Substitute.For<RigSwitch.Core.Interfaces.IDisplayConfigurationService>();
        var audioDirector = NSubstitute.Substitute.For<RigSwitch.Core.Interfaces.IAudioEndpointDirector>();
        var hotkeyService = NSubstitute.Substitute.For<RigSwitch.Core.Interfaces.IGlobalHotkeyService>();
        using var trayIconService = new RigSwitch.App.Services.TrayIconService(coordinator, settingsStorage);

        using var vm = new MainSettingsViewModel(
            coordinator,
            settingsStorage,
            displayService,
            audioDirector,
            hotkeyService,
            trayIconService);

        var changedProperties = new List<string>();
        vm.PropertyChanged += (s, e) => { if (e.PropertyName != null) changedProperties.Add(e.PropertyName); };

        // Defaults to All
        Assert.True(vm.IsAllFilterSelected);
        Assert.False(vm.IsActiveFilterSelected);
        Assert.False(vm.IsInactiveFilterSelected);
        Assert.Contains("Showing All", vm.FilterStatusSummary);

        // Select Active via TwoWay property
        changedProperties.Clear();
        vm.IsActiveFilterSelected = true;
        Assert.Equal("Active", vm.AudioFilterSelection);
        Assert.True(vm.IsActiveFilterSelected);
        Assert.False(vm.IsAllFilterSelected);
        Assert.False(vm.IsInactiveFilterSelected);
        Assert.Contains(nameof(vm.IsActiveFilterSelected), changedProperties);
        Assert.Contains(nameof(vm.FilterStatusSummary), changedProperties);
        Assert.Contains("Showing Active Only", vm.FilterStatusSummary);

        // Select Inactive via TwoWay property
        changedProperties.Clear();
        vm.IsInactiveFilterSelected = true;
        Assert.Equal("Inactive", vm.AudioFilterSelection);
        Assert.True(vm.IsInactiveFilterSelected);
        Assert.False(vm.IsActiveFilterSelected);
        Assert.Contains(nameof(vm.IsInactiveFilterSelected), changedProperties);
        Assert.Contains("Showing Disabled / Unplugged", vm.FilterStatusSummary);

        // Select All via command
        vm.SelectAllAudioFilterCommand.Execute(null);
        Assert.Equal("All", vm.AudioFilterSelection);
        Assert.True(vm.IsAllFilterSelected);
    }
}
