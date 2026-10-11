namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.Core.Enums;
using RigSwitch.Infrastructure.Windows.CoreAudio;
using Xunit;

public sealed class CoreAudioNotificationClientTests
{
    [Fact]
    public void NativeAudioProvider_ExposesDefaultDeviceChangedEvent()
    {
        using var provider = new WindowsNativeAudioProvider();
        bool eventFired = false;
        provider.DefaultDeviceChanged += (flow, role, id) =>
        {
            eventFired = true;
        };

        Assert.False(eventFired);
    }

    [Fact]
    public void OnDefaultDeviceChanged_FiresDefaultDeviceChangedEvent_WithExpectedArguments()
    {
        // Arrange
        var enumerator = Substitute.For<IMMDeviceEnumerator>();
        using var provider = new WindowsNativeAudioProvider(enumerator, policyConfig: null);

        EDataFlow? receivedFlow = null;
        ERole? receivedRole = null;
        string? receivedId = null;

        provider.DefaultDeviceChanged += (flow, role, id) =>
        {
            receivedFlow = flow;
            receivedRole = role;
            receivedId = id;
        };

        const string expectedId = "{0.0.0.00000000}.{12345678-1234-1234-1234-123456789abc}";

        // Act
        provider.OnDefaultDeviceChanged(EDataFlow.eRender, ERole.eMultimedia, expectedId);

        // Assert
        Assert.Equal(EDataFlow.eRender, receivedFlow);
        Assert.Equal(ERole.eMultimedia, receivedRole);
        Assert.Equal(expectedId, receivedId);
    }

    [Fact]
    public void Constructor_RegistersNotificationCallback_WhenEnumeratorProvided()
    {
        // Arrange
        var enumerator = Substitute.For<IMMDeviceEnumerator>();
        enumerator.RegisterEndpointNotificationCallback(Arg.Any<IMMNotificationClient>()).Returns(0);

        // Act
        using var provider = new WindowsNativeAudioProvider(enumerator, policyConfig: null);

        // Assert
        enumerator.Received(1).RegisterEndpointNotificationCallback(provider);
    }

    [Fact]
    public void Dispose_UnregistersNotificationCallback_WhenRegistered()
    {
        // Arrange
        var enumerator = Substitute.For<IMMDeviceEnumerator>();
        enumerator.RegisterEndpointNotificationCallback(Arg.Any<IMMNotificationClient>()).Returns(0);
        enumerator.UnregisterEndpointNotificationCallback(Arg.Any<IMMNotificationClient>()).Returns(0);

        var provider = new WindowsNativeAudioProvider(enumerator, policyConfig: null);

        // Act
        provider.Dispose();

        // Assert
        enumerator.Received(1).UnregisterEndpointNotificationCallback(provider);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_UnregistersOnlyOnce()
    {
        // Arrange
        var enumerator = Substitute.For<IMMDeviceEnumerator>();
        enumerator.RegisterEndpointNotificationCallback(Arg.Any<IMMNotificationClient>()).Returns(0);
        enumerator.UnregisterEndpointNotificationCallback(Arg.Any<IMMNotificationClient>()).Returns(0);

        var provider = new WindowsNativeAudioProvider(enumerator, policyConfig: null);

        // Act
        provider.Dispose();
        provider.Dispose();

        // Assert
        enumerator.Received(1).UnregisterEndpointNotificationCallback(provider);
    }

    [Fact]
    public void Dispose_WhenRegistrationFailed_DoesNotCallUnregister()
    {
        // Arrange
        var enumerator = Substitute.For<IMMDeviceEnumerator>();
        enumerator.RegisterEndpointNotificationCallback(Arg.Any<IMMNotificationClient>()).Returns(unchecked((int)0x80004005)); // E_FAIL

        var provider = new WindowsNativeAudioProvider(enumerator, policyConfig: null);

        // Act
        provider.Dispose();

        // Assert
        enumerator.DidNotReceive().UnregisterEndpointNotificationCallback(Arg.Any<IMMNotificationClient>());
    }

    [Fact]
    public void IMMNotificationClient_StubMethods_DoNotThrow()
    {
        // Arrange
        var enumerator = Substitute.For<IMMDeviceEnumerator>();
        using var provider = new WindowsNativeAudioProvider(enumerator, policyConfig: null);

        // Act & Assert (none of the stubs should throw)
        provider.OnDeviceStateChanged("test-id", DevicePresenceState.Active);
        provider.OnDeviceAdded("test-id");
        provider.OnDeviceRemoved("test-id");
        provider.OnPropertyValueChanged("test-id", default);
    }
}
