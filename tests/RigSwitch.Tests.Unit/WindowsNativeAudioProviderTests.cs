namespace RigSwitch.Tests.Unit;

using System.Runtime.InteropServices;
using NSubstitute;
using RigSwitch.Core.Enums;
using RigSwitch.Infrastructure.Windows.CoreAudio;
using Xunit;

public sealed class WindowsNativeAudioProviderTests
{
    [Theory]
    [InlineData("{0.0.0.00000000}.{067dbc2f-95ba-413e-b0e4-758644811d1c}", "{067dbc2f-95ba-413e-b0e4-758644811d1c}")]
    [InlineData("{11111111-2222-3333-4444-555555555555}", "{11111111-2222-3333-4444-555555555555}")]
    [InlineData("11111111-2222-3333-4444-555555555555", "{11111111-2222-3333-4444-555555555555}")]
    [InlineData("custom-endpoint-id", "{custom-endpoint-id}")]
    [InlineData("{already-braced-custom-id}", "{already-braced-custom-id}")]
    public void ExtractEndpointGuid_ParsesVariousFormatsCorrectly(string input, string expected)
    {
        // Act
        string actual = WindowsNativeAudioProvider.ExtractEndpointGuid(input);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SetEndpointVisibility_WhenComSucceeds_DoesNotTriggerRegistryFallback()
    {
        // Arrange
        var policyConfig = Substitute.For<IPolicyConfig>();
        policyConfig.SetEndpointVisibility(Arg.Any<string>(), Arg.Any<bool>()).Returns(0);

        bool fallbackInvoked = false;
        var provider = new WindowsNativeAudioProvider(
            deviceEnumerator: null,
            policyConfig: policyConfig,
            registryFallbackAction: (_, _) => fallbackInvoked = true);

        // Act
        provider.SetEndpointVisibility("{0.0.0.00000000}.{11111111-1111-1111-1111-111111111111}", true);

        // Assert
        policyConfig.Received(1).SetEndpointVisibility("{0.0.0.00000000}.{11111111-1111-1111-1111-111111111111}", true);
        Assert.False(fallbackInvoked);
    }

    [Fact]
    public void SetEndpointVisibility_WhenComReturnsNonZeroHr_TriggersRegistryFallback()
    {
        // Arrange
        const string endpointId = "{0.0.0.00000000}.{22222222-2222-2222-2222-222222222222}";
        var policyConfig = Substitute.For<IPolicyConfig>();
        policyConfig.SetEndpointVisibility(endpointId, false).Returns(unchecked((int)0x80070005)); // E_ACCESSDENIED

        string? fallbackEndpoint = null;
        bool? fallbackVisibility = null;

        var provider = new WindowsNativeAudioProvider(
            deviceEnumerator: null,
            policyConfig: policyConfig,
            registryFallbackAction: (id, visible) =>
            {
                fallbackEndpoint = id;
                fallbackVisibility = visible;
            });

        // Act
        provider.SetEndpointVisibility(endpointId, false);

        // Assert
        Assert.Equal(endpointId, fallbackEndpoint);
        Assert.False(fallbackVisibility);
    }

    [Fact]
    public void SetEndpointVisibility_WhenComThrowsException_TriggersRegistryFallback()
    {
        // Arrange
        const string endpointId = "{0.0.0.00000000}.{33333333-3333-3333-3333-333333333333}";
        var policyConfig = Substitute.For<IPolicyConfig>();
        policyConfig.When(p => p.SetEndpointVisibility(endpointId, true))
            .Do(_ => throw new InvalidOperationException("Access Denied"));

        string? fallbackEndpoint = null;
        bool? fallbackVisibility = null;

        var provider = new WindowsNativeAudioProvider(
            deviceEnumerator: null,
            policyConfig: policyConfig,
            registryFallbackAction: (id, visible) =>
            {
                fallbackEndpoint = id;
                fallbackVisibility = visible;
            });

        // Act
        provider.SetEndpointVisibility(endpointId, true);

        // Assert
        Assert.Equal(endpointId, fallbackEndpoint);
        Assert.True(fallbackVisibility);
    }

    [Fact]
    public void ApplyRegistryVisibilityFallback_WhenKeyNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        const string nonExistentId = "{00000000-dead-beef-cafe-000000000000}";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => WindowsNativeAudioProvider.ApplyRegistryVisibilityFallback(nonExistentId, isVisible: false));

        Assert.Contains(nonExistentId, ex.Message);
    }

    [Fact]
    public void EnumerateRenderEndpoints_WithManagedNSubstituteDoubles_DoesNotThrowOnMarshalRelease()
    {
        // Arrange
        var enumerator = Substitute.For<IMMDeviceEnumerator>();
        var collection = Substitute.For<IMMDeviceCollection>();
        var device = Substitute.For<IMMDevice>();
        var propertyStore = Substitute.For<IPropertyStore>();

        const string endpointId = "{0.0.0.00000000}.{44444444-4444-4444-4444-444444444444}";
        device.GetId(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = endpointId;
            return 0;
        });

        device.GetState(out Arg.Any<DevicePresenceState>()).Returns(x =>
        {
            x[0] = DevicePresenceState.Active;
            return 0;
        });

        device.OpenPropertyStore(StorageAccessMode.Read, out Arg.Any<IPropertyStore>()).Returns(x =>
        {
            x[1] = propertyStore;
            return 0;
        });

        collection.GetCount(out Arg.Any<uint>()).Returns(x =>
        {
            x[0] = 1u;
            return 0;
        });

        collection.Item(0, out Arg.Any<IMMDevice>()).Returns(x =>
        {
            x[1] = device;
            return 0;
        });

        enumerator.EnumAudioEndpoints(EDataFlow.eRender, Arg.Any<DevicePresenceState>(), out Arg.Any<IMMDeviceCollection>())
            .Returns(x =>
            {
                x[2] = collection;
                return 0;
            });

        enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out Arg.Any<IMMDevice>())
            .Returns(x =>
            {
                x[2] = device;
                return 0;
            });

        enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eCommunications, out Arg.Any<IMMDevice>())
            .Returns(x =>
            {
                x[2] = null!;
                return 1;
            });

        var provider = new WindowsNativeAudioProvider(enumerator, policyConfig: null);

        // Act - Should not throw ArgumentException from Marshal.ReleaseComObject when releasing managed mocks
        var endpoints = provider.EnumerateRenderEndpoints();

        // Assert
        Assert.NotNull(endpoints);
        Assert.Single(endpoints);
        Assert.Equal(endpointId, endpoints[0].Id);
        Assert.True(endpoints[0].IsDefaultPlayback);
        Assert.False(endpoints[0].IsDefaultCommunications);
    }

    [Fact]
    public void SetDefaultEndpoint_WhenComSucceeds_InvokesPolicyConfig()
    {
        // Arrange
        const string endpointId = "{0.0.0.00000000}.{55555555-5555-5555-5555-555555555555}";
        var policyConfig = Substitute.For<IPolicyConfig>();
        policyConfig.SetDefaultEndpoint(endpointId, ERole.eConsole).Returns(0);

        var provider = new WindowsNativeAudioProvider(deviceEnumerator: null, policyConfig: policyConfig);

        // Act
        provider.SetDefaultEndpoint(endpointId, ERole.eConsole);

        // Assert
        policyConfig.Received(1).SetDefaultEndpoint(endpointId, ERole.eConsole);
    }

    [Fact]
    public void SetDefaultEndpoint_WhenComFails_ThrowsException()
    {
        // Arrange
        const string endpointId = "{0.0.0.00000000}.{55555555-5555-5555-5555-555555555555}";
        var policyConfig = Substitute.For<IPolicyConfig>();
        policyConfig.SetDefaultEndpoint(endpointId, ERole.eMultimedia).Returns(unchecked((int)0x80004005));

        var provider = new WindowsNativeAudioProvider(deviceEnumerator: null, policyConfig: policyConfig);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => provider.SetDefaultEndpoint(endpointId, ERole.eMultimedia));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetDefaultEndpoint_WhenInvalidEndpointId_ThrowsArgumentException(string? invalidId)
    {
        // Arrange
        var provider = new WindowsNativeAudioProvider();

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => provider.SetDefaultEndpoint(invalidId!, ERole.eConsole));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetEndpointVisibility_WhenInvalidEndpointId_ThrowsArgumentException(string? invalidId)
    {
        // Arrange
        var provider = new WindowsNativeAudioProvider();

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => provider.SetEndpointVisibility(invalidId!, isVisible: true));
    }
}
