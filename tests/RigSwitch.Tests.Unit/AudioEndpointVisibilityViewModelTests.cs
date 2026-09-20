namespace RigSwitch.Tests.Unit;

using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using Xunit;

public sealed class AudioEndpointVisibilityViewModelTests
{
    [Theory]
    [InlineData(DevicePresenceState.Active, "Active")]
    [InlineData(DevicePresenceState.Disabled, "Disabled")]
    [InlineData(DevicePresenceState.Unplugged, "Unplugged")]
    [InlineData(DevicePresenceState.NotPresent, "Not Present")]
    public void Constructor_SetsPresenceStateAndFriendlyLabel(DevicePresenceState state, string expectedLabel)
    {
        var vm = new AudioEndpointVisibilityItemViewModel(
            "{guid}",
            "Test Device",
            "Test Adapter",
            isVisible: true,
            state: state);

        Assert.Equal(state, vm.State);
        Assert.Equal(expectedLabel, vm.StateLabel);
    }
}
