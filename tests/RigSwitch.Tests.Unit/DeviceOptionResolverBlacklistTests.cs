namespace RigSwitch.Tests.Unit;

using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

public sealed class DeviceOptionResolverBlacklistTests
{
    [Fact]
    public void ExcludeBlacklistedEndpoints_RemovesBlacklistedFromOptions()
    {
        var endpoints = new List<AudioEndpointInfo>
        {
            new("{DAC-1}", "Nobsound Shaker DAC", "USB", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{SPEAKERS}", "Pebble V3", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback)
        };
        var blacklisted = new List<string> { "{DAC-1}" };

        var allowed = DeviceOptionResolver.FilterBlacklistedEndpoints(endpoints, blacklisted);

        Assert.Single(allowed);
        Assert.Equal("{SPEAKERS}", allowed[0].Id);
    }

    [Fact]
    public void ExcludeBlacklistedEndpoints_MatchesByGuidComponent()
    {
        const string fullId = "{0.0.0.00000000}.{a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d}";
        const string guidOnly = "{a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d}";

        var endpoints = new List<AudioEndpointInfo>
        {
            new(fullId, "Bass Shaker", "USB Audio", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{OTHER}", "Headset", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback)
        };
        var blacklisted = new List<string> { guidOnly };

        var allowed = DeviceOptionResolver.FilterBlacklistedEndpoints(endpoints, blacklisted);

        Assert.Single(allowed);
        Assert.Equal("{OTHER}", allowed[0].Id);
    }

    [Fact]
    public void ExcludeBlacklistedEndpoints_WhenBlacklistNullOrEmpty_ReturnsAllEndpoints()
    {
        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SPK-1}", "Speakers", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback)
        };

        var allowedEmpty = DeviceOptionResolver.FilterBlacklistedEndpoints(endpoints, []);
        var allowedNull = DeviceOptionResolver.FilterBlacklistedEndpoints(endpoints, null);

        Assert.Single(allowedEmpty);
        Assert.Single(allowedNull);
    }
}
