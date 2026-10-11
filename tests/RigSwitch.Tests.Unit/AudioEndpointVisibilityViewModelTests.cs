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

    [Fact]
    public void IsBlacklisted_WhenTrue_UpdatesBadgeVisuals()
    {
        var vm = new AudioEndpointVisibilityItemViewModel(
            "{guid}",
            "Nobsound Bass Shaker DAC",
            "USB Audio",
            isVisible: true,
            state: DevicePresenceState.Active,
            isBlacklisted: false);

        Assert.Equal("Active", vm.StateLabel);
        Assert.Equal("#0D2818", vm.StateBadgeBackground);
        Assert.Equal("#00E676", vm.StateBadgeForeground);
        Assert.Equal("#00C853", vm.StateBadgeBorder);

        vm.IsBlacklisted = true;

        Assert.Equal("Blacklisted / Bass Shaker", vm.StateLabel);
        Assert.Equal("#2A0E14", vm.StateBadgeBackground);
        Assert.Equal("#FF5252", vm.StateBadgeForeground);
        Assert.Equal("#D32F2F", vm.StateBadgeBorder);
    }

    [Fact]
    public async Task IsBlacklisted_WhenChanged_InvokesCallback()
    {
        bool callbackFired = false;
        bool reportedState = false;

        var vm = new AudioEndpointVisibilityItemViewModel(
            "{guid}",
            "Test Device",
            "Adapter",
            isVisible: true,
            onBlacklistChanged: (item, isBlacklisted) =>
            {
                callbackFired = true;
                reportedState = isBlacklisted;
                return Task.CompletedTask;
            });

        vm.IsBlacklisted = true;

        Assert.True(callbackFired);
        Assert.True(reportedState);
    }

    [Fact]
    public void RevertBlacklisted_RestoresStateWithoutCallback()
    {
        bool callbackFired = false;

        var vm = new AudioEndpointVisibilityItemViewModel(
            "{guid}",
            "Test Device",
            "Adapter",
            isVisible: true,
            isBlacklisted: true,
            onBlacklistChanged: (_, _) =>
            {
                callbackFired = true;
                return Task.CompletedTask;
            });

        vm.RevertBlacklisted(false);

        Assert.False(vm.IsBlacklisted);
        Assert.False(callbackFired);
        Assert.Equal("Active", vm.StateLabel);
    }
}
