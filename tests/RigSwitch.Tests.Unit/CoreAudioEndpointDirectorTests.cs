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
}
