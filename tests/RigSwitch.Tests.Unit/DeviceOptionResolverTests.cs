namespace RigSwitch.Tests.Unit;

using System.Collections.ObjectModel;
using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

public sealed class DeviceOptionResolverTests
{
    [Fact]
    public void ResolveAudioOption_WhenEndpointIsInactive_AddsToOptionsWithFriendlyNameAndDisconnectedTag()
    {
        // Arrange
        const string targetId = "{0.0.0.00000000}.{11111111-2222-3333-4444-555555555555}";
        var endpoints = new List<AudioEndpointInfo>
        {
            new(targetId, "Headphones", "Realtek", DevicePresenceState.Unplugged, false, false)
        };
        var options = new ObservableCollection<DeviceSelectionOption>();
        var customNames = new Dictionary<string, string> { [targetId] = "My Headset" };
        var cachedNames = new Dictionary<string, string>();

        // Act
        var result = DeviceOptionResolver.ResolveAudioOption(targetId, endpoints, options, customNames, cachedNames);

        // Assert
        Assert.Equal(targetId, result);
        var addedOption = Assert.Single(options);
        Assert.Equal(targetId, addedOption.Id);
        Assert.Equal("My Headset (Headphones) (Disconnected)", addedOption.DisplayName);
    }

    [Fact]
    public void ResolveAudioOption_WhenEndpointIsCompletelyMissing_UsesCachedNameAndTag()
    {
        // Arrange
        const string targetId = "{0.0.0.00000000}.{99999999-8888-7777-6666-555555555555}";
        var endpoints = new List<AudioEndpointInfo>();
        var options = new ObservableCollection<DeviceSelectionOption>();
        var customNames = new Dictionary<string, string>();
        var cachedNames = new Dictionary<string, string> { [targetId] = "Wireless Gaming DAC" };

        // Act
        var result = DeviceOptionResolver.ResolveAudioOption(targetId, endpoints, options, customNames, cachedNames);

        // Assert
        Assert.Equal(targetId, result);
        var addedOption = Assert.Single(options);
        Assert.Equal(targetId, addedOption.Id);
        Assert.Equal("Wireless Gaming DAC (Disconnected)", addedOption.DisplayName);
    }

    [Fact]
    public void EnsureDisplayOption_WhenMonitorMissing_UsesCachedNameAndTag()
    {
        // Arrange
        const string monitorId = "MSI4DD0";
        var options = new ObservableCollection<DeviceSelectionOption>();
        var customNames = new Dictionary<string, string>();
        var cachedNames = new Dictionary<string, string> { [monitorId] = "MSI MPG 341CQPX" };

        // Act
        DeviceOptionResolver.EnsureDisplayOption(monitorId, options, customNames, cachedNames);

        // Assert
        var addedOption = Assert.Single(options);
        Assert.Equal(monitorId, addedOption.Id);
        Assert.Equal("MSI MPG 341CQPX [MSI4DD0] (Disconnected)", addedOption.DisplayName);
    }

    [Fact]
    public void EnsureDisplayOption_WhenMonitorHasNicknameAndCachedName_FormatsBoth()
    {
        // Arrange
        const string monitorId = "MSI4DD0";
        var options = new ObservableCollection<DeviceSelectionOption>();
        var customNames = new Dictionary<string, string> { [monitorId] = "Center Rig Display" };
        var cachedNames = new Dictionary<string, string> { [monitorId] = "MSI MPG 341CQPX" };

        // Act
        DeviceOptionResolver.EnsureDisplayOption(monitorId, options, customNames, cachedNames);

        // Assert
        var addedOption = Assert.Single(options);
        Assert.Equal(monitorId, addedOption.Id);
        Assert.Equal("Center Rig Display (MSI MPG 341CQPX) [MSI4DD0] (Disconnected)", addedOption.DisplayName);
    }
}
