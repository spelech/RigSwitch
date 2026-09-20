namespace RigSwitch.Infrastructure.Windows.CoreAudio;

using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Directs audio endpoint routing and controls endpoint visibility in Windows CoreAudio.
/// </summary>
public sealed class CoreAudioEndpointDirector : IAudioEndpointDirector
{
    private readonly INativeAudioProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoreAudioEndpointDirector"/> class.
    /// </summary>
    /// <param name="provider">Optional native audio provider abstraction. Defaults to <see cref="WindowsNativeAudioProvider"/>.</param>
    public CoreAudioEndpointDirector(INativeAudioProvider? provider = null)
    {
        _provider = provider ?? new WindowsNativeAudioProvider();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AudioEndpointInfo>> EnumerateAudioEndpointsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var endpoints = _provider.EnumerateRenderEndpoints();
        return Task.FromResult(endpoints);
    }

    /// <inheritdoc/>
    public Task SetDefaultPlaybackEndpointAsync(string endpointId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        _provider.SetDefaultEndpoint(endpointId, ERole.eConsole);
        cancellationToken.ThrowIfCancellationRequested();

        _provider.SetDefaultEndpoint(endpointId, ERole.eMultimedia);
        cancellationToken.ThrowIfCancellationRequested();

        _provider.SetDefaultEndpoint(endpointId, ERole.eCommunications);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetEndpointVisibilityAsync(string endpointId, bool isVisible, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        _provider.SetEndpointVisibility(endpointId, isVisible);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SyncHiddenEndpointsAsync(IEnumerable<string> hiddenEndpointIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(hiddenEndpointIds);

        foreach (var endpointId in hiddenEndpointIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(endpointId))
            {
                _provider.SetEndpointVisibility(endpointId, isVisible: false);
            }
        }

        return Task.CompletedTask;
    }
}
