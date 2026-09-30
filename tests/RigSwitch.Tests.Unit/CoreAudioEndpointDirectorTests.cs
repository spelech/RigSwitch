namespace RigSwitch.Tests.Unit;

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Windows.CoreAudio;
using Xunit;

public sealed class CoreAudioEndpointDirectorTests
{
    private readonly INativeAudioProvider _audioProvider;
    private readonly CoreAudioEndpointDirector _director;

    public CoreAudioEndpointDirectorTests()
    {
        _audioProvider = Substitute.For<INativeAudioProvider>();
        _director = new CoreAudioEndpointDirector(_audioProvider);
    }

    [Fact]
    public void Constructor_WithoutParameters_InitializesSuccessfully()
    {
        // Act
        var director = new CoreAudioEndpointDirector();

        // Assert
        Assert.NotNull(director);
    }

    [Fact]
    public async Task EnumerateAudioEndpointsAsync_ReturnsConfiguredEndpoints()
    {
        // Arrange
        var configuredEndpoints = new List<AudioEndpointInfo>
        {
            new(
                id: "{0.0.0.00000000}.{11111111-1111-1111-1111-111111111111}",
                name: "Speakers (Pebble V3)",
                adapterDescription: "Realtek Audio",
                state: DevicePresenceState.Active,
                isDefaultPlayback: true,
                isDefaultCommunications: false),
            new(
                id: "{0.0.0.00000000}.{22222222-2222-2222-2222-222222222222}",
                name: "Headphones (Wireless Headset)",
                adapterDescription: "USB Audio",
                state: DevicePresenceState.Active,
                isDefaultPlayback: false,
                isDefaultCommunications: true),
            new(
                id: "{0.0.0.00000000}.{33333333-3333-3333-3333-333333333333}",
                name: "VG34VQL3A (NVIDIA)",
                adapterDescription: "NVIDIA High Definition Audio",
                state: DevicePresenceState.Disabled,
                isDefaultPlayback: false,
                isDefaultCommunications: false),
        };

        _audioProvider.EnumerateRenderEndpoints().Returns(configuredEndpoints);

        // Act
        var actualEndpoints = await _director.EnumerateAudioEndpointsAsync();

        // Assert
        Assert.NotNull(actualEndpoints);
        Assert.Equal(3, actualEndpoints.Count);
        Assert.Equal("{0.0.0.00000000}.{11111111-1111-1111-1111-111111111111}", actualEndpoints[0].Id);
        Assert.True(actualEndpoints[0].IsDefaultPlayback);
        Assert.Equal("{0.0.0.00000000}.{22222222-2222-2222-2222-222222222222}", actualEndpoints[1].Id);
        Assert.True(actualEndpoints[1].IsDefaultCommunications);
        Assert.Equal(DevicePresenceState.Disabled, actualEndpoints[2].State);
        _audioProvider.Received(1).EnumerateRenderEndpoints();
    }

    [Fact]
    public async Task SetDefaultPlaybackEndpointAsync_SetsConsoleAndMultimediaRoles()
    {
        // Arrange
        const string targetEndpointId = "{0.0.0.00000000}.{11111111-1111-1111-1111-111111111111}";

        // Act
        await _director.SetDefaultPlaybackEndpointAsync(targetEndpointId);

        // Assert
        _audioProvider.Received(1).SetDefaultEndpoint(targetEndpointId, ERole.eConsole);
        _audioProvider.Received(1).SetDefaultEndpoint(targetEndpointId, ERole.eMultimedia);
        _audioProvider.Received(1).SetDefaultEndpoint(targetEndpointId, ERole.eCommunications);
    }

    [Fact]
    public async Task SetEndpointVisibilityAsync_CallsProviderWithCorrectFlags()
    {
        // Arrange
        const string targetEndpointId = "{0.0.0.00000000}.{33333333-3333-3333-3333-333333333333}";

        // Act - enable
        await _director.SetEndpointVisibilityAsync(targetEndpointId, isVisible: true);

        // Assert - enable
        _audioProvider.Received(1).SetEndpointVisibility(targetEndpointId, true);

        // Act - disable
        await _director.SetEndpointVisibilityAsync(targetEndpointId, isVisible: false);

        // Assert - disable
        _audioProvider.Received(1).SetEndpointVisibility(targetEndpointId, false);
    }

    [Fact]
    public async Task SyncHiddenEndpointsAsync_DisablesAllSpecifiedEndpoints()
    {
        // Arrange
        var hiddenEndpoints = new[]
        {
            "{0.0.0.00000000}.{22222222-2222-2222-2222-222222222222}",
            "{0.0.0.00000000}.{33333333-3333-3333-3333-333333333333}",
            "{0.0.0.00000000}.{44444444-4444-4444-4444-444444444444}",
        };

        // Act
        await _director.SyncHiddenEndpointsAsync(hiddenEndpoints);

        // Assert
        _audioProvider.Received(1).SetEndpointVisibility("{0.0.0.00000000}.{22222222-2222-2222-2222-222222222222}", false);
        _audioProvider.Received(1).SetEndpointVisibility("{0.0.0.00000000}.{33333333-3333-3333-3333-333333333333}", false);
        _audioProvider.Received(1).SetEndpointVisibility("{0.0.0.00000000}.{44444444-4444-4444-4444-444444444444}", false);
    }

    [Fact]
    public async Task SetDefaultPlaybackEndpointAsync_WhenProviderThrows_PropagatesException()
    {
        // Arrange
        const string targetEndpointId = "{0.0.0.00000000}.{bad-endpoint-id}";
        var expectedException = new InvalidOperationException("Failed to set default COM endpoint");

        _audioProvider
            .When(p => p.SetDefaultEndpoint(targetEndpointId, Arg.Any<ERole>()))
            .Do(_ => throw expectedException);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _director.SetDefaultPlaybackEndpointAsync(targetEndpointId));

        Assert.Same(expectedException, ex);
    }

    [Fact]
    public async Task CancellationTokens_AreRespectedAcrossAllMethods()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var dummyEndpoints = new[] { "{0.0.0.00000000}.{11111111-1111-1111-1111-111111111111}" };

        // Act & Assert - EnumerateAudioEndpointsAsync
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _director.EnumerateAudioEndpointsAsync(cts.Token));

        // Act & Assert - SetDefaultPlaybackEndpointAsync
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _director.SetDefaultPlaybackEndpointAsync("any-id", cts.Token));

        // Act & Assert - SetEndpointVisibilityAsync
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _director.SetEndpointVisibilityAsync("any-id", isVisible: true, cts.Token));

        // Act & Assert - SyncHiddenEndpointsAsync
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _director.SyncHiddenEndpointsAsync(dummyEndpoints, cts.Token));

        // Ensure provider methods were never called due to early cancellation
        _audioProvider.DidNotReceive().EnumerateRenderEndpoints();
        _audioProvider.DidNotReceive().SetDefaultEndpoint(Arg.Any<string>(), Arg.Any<ERole>());
        _audioProvider.DidNotReceive().SetEndpointVisibility(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetDefaultPlaybackEndpointAsync_WhenEndpointIdNullOrWhitespace_ThrowsArgumentException(string? invalidId)
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _director.SetDefaultPlaybackEndpointAsync(invalidId!));

        _audioProvider.DidNotReceive().SetDefaultEndpoint(Arg.Any<string>(), Arg.Any<ERole>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetEndpointVisibilityAsync_WhenEndpointIdNullOrWhitespace_ThrowsArgumentException(string? invalidId)
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _director.SetEndpointVisibilityAsync(invalidId!, isVisible: true));

        _audioProvider.DidNotReceive().SetEndpointVisibility(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task SyncHiddenEndpointsAsync_WhenHiddenEndpointsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _director.SyncHiddenEndpointsAsync(null!));
    }

    [Fact]
    public async Task SyncHiddenEndpointsAsync_SkipsNullOrWhitespaceEndpointIds()
    {
        // Arrange
        var hiddenEndpoints = new[]
        {
            "{0.0.0.00000000}.{valid-id}",
            null!,
            "",
            "   ",
            "{0.0.0.00000000}.{another-valid-id}",
        };

        // Act
        await _director.SyncHiddenEndpointsAsync(hiddenEndpoints);

        // Assert
        _audioProvider.Received(1).SetEndpointVisibility("{0.0.0.00000000}.{valid-id}", false);
        _audioProvider.Received(1).SetEndpointVisibility("{0.0.0.00000000}.{another-valid-id}", false);
        _audioProvider.Received(2).SetEndpointVisibility(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task SyncHiddenEndpointsAsync_WhenEmptyList_DoesNotCallProvider()
    {
        // Act
        await _director.SyncHiddenEndpointsAsync([]);

        // Assert
        _audioProvider.DidNotReceive().SetEndpointVisibility(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task EnumerateAudioEndpointsAsync_WhenProviderThrows_PropagatesException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Enumeration failed");
        _audioProvider.EnumerateRenderEndpoints().Throws(expectedException);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _director.EnumerateAudioEndpointsAsync());

        Assert.Same(expectedException, ex);
    }

    [Fact]
    public async Task SetEndpointVisibilityAsync_WhenProviderThrows_PropagatesException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Visibility set failed");
        _audioProvider
            .When(p => p.SetEndpointVisibility(Arg.Any<string>(), Arg.Any<bool>()))
            .Do(_ => throw expectedException);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _director.SetEndpointVisibilityAsync("endpoint-1", true));

        Assert.Same(expectedException, ex);
    }

    [Fact]
    public async Task EnumerateAudioEndpointsAsync_WithCaptureFlow_ReturnsOnlyCaptureEndpoints()
    {
        // Arrange
        var captureEndpoints = new List<AudioEndpointInfo>
        {
            new(
                id: "{0.0.1.00000000}.{MIC-1}",
                name: "Boom Microphone",
                adapterDescription: "Realtek Audio",
                state: DevicePresenceState.Active,
                isDefaultPlayback: false,
                isDefaultCommunications: true,
                flow: AudioDeviceFlow.Capture,
                isDefaultCapture: true)
        };

        _audioProvider.EnumerateCaptureEndpoints().Returns(captureEndpoints);

        // Act
        var result = await _director.EnumerateAudioEndpointsAsync(AudioDeviceFlow.Capture);

        // Assert
        Assert.Single(result);
        Assert.Equal("{0.0.1.00000000}.{MIC-1}", result[0].Id);
        Assert.Equal(AudioDeviceFlow.Capture, result[0].Flow);
        Assert.True(result[0].IsDefaultCapture);
        _audioProvider.Received(1).EnumerateCaptureEndpoints();
        _audioProvider.DidNotReceive().EnumerateRenderEndpoints();
    }

    [Fact]
    public async Task EnumerateAudioEndpointsAsync_WithNullFlow_ReturnsBothRenderAndCaptureEndpoints()
    {
        // Arrange
        var renderEndpoints = new List<AudioEndpointInfo>
        {
            new("RENDER-1", "Speakers", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback, false)
        };
        var captureEndpoints = new List<AudioEndpointInfo>
        {
            new("CAPTURE-1", "Mic", "USB", DevicePresenceState.Active, false, true, AudioDeviceFlow.Capture, true)
        };

        _audioProvider.EnumerateRenderEndpoints().Returns(renderEndpoints);
        _audioProvider.EnumerateCaptureEndpoints().Returns(captureEndpoints);

        // Act
        var result = await _director.EnumerateAudioEndpointsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Id == "RENDER-1");
        Assert.Contains(result, x => x.Id == "CAPTURE-1");
        _audioProvider.Received(1).EnumerateRenderEndpoints();
        _audioProvider.Received(1).EnumerateCaptureEndpoints();
    }

    [Fact]
    public async Task SetDefaultCaptureEndpointAsync_SetsConsoleMultimediaAndCommunications()
    {
        // Arrange
        const string targetId = "{0.0.1.00000000}.{TARGET-MIC}";

        // Act
        await _director.SetDefaultCaptureEndpointAsync(targetId);

        // Assert
        _audioProvider.Received(1).SetDefaultEndpoint(targetId, ERole.eConsole);
        _audioProvider.Received(1).SetDefaultEndpoint(targetId, ERole.eMultimedia);
        _audioProvider.Received(1).SetDefaultEndpoint(targetId, ERole.eCommunications);
    }

    [Theory]
    [InlineData(75, false, 0.75f, false)]
    [InlineData(0, true, 0.0f, true)]
    [InlineData(100, false, 1.0f, false)]
    [InlineData(-10, false, 0.0f, false)] // Clamped lower
    [InlineData(150, true, 1.0f, true)]   // Clamped upper
    public async Task SetEndpointVolumeAsync_ClampsScalarAndInvokesProvider(int volumePercent, bool isMuted, float expectedScalar, bool expectedMute)
    {
        // Arrange
        const string endpointId = "{ENDPOINT-VOL}";

        // Act
        await _director.SetEndpointVolumeAsync(endpointId, volumePercent, isMuted);

        // Assert
        _audioProvider.Received(1).SetEndpointVolume(
            endpointId,
            Arg.Is<float>(v => Math.Abs(v - expectedScalar) < 0.001f),
            expectedMute);
    }

    [Fact]
    public async Task GetEndpointVolumeAsync_TranslatesScalarToPercentage()
    {
        // Arrange
        const string endpointId = "{ENDPOINT-VOL}";
        _audioProvider.GetEndpointVolume(endpointId).Returns((0.68f, true));

        // Act
        var (volumePercent, isMuted) = await _director.GetEndpointVolumeAsync(endpointId);

        // Assert
        Assert.Equal(68, volumePercent);
        Assert.True(isMuted);
    }
}
