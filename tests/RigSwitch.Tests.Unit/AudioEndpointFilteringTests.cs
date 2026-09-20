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
    }
}
