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

        var render = _provider.EnumerateRenderEndpoints();
        var capture = _provider.EnumerateCaptureEndpoints();
        var combined = new List<AudioEndpointInfo>(render.Count + capture.Count);
        combined.AddRange(render);
        combined.AddRange(capture);
        return Task.FromResult<IReadOnlyList<AudioEndpointInfo>>(combined.AsReadOnly());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AudioEndpointInfo>> EnumerateAudioEndpointsAsync(
        RigSwitch.Core.Enums.AudioDeviceFlow flow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (flow == RigSwitch.Core.Enums.AudioDeviceFlow.Playback)
        {
            return Task.FromResult(_provider.EnumerateRenderEndpoints());
        }

        return Task.FromResult(_provider.EnumerateCaptureEndpoints());
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
    public Task SetDefaultCaptureEndpointAsync(string endpointId, CancellationToken cancellationToken = default)
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
    public Task SetEndpointVolumeAsync(string endpointId, int volumePercent, bool isMuted, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        float scalar = Math.Clamp(volumePercent, 0, 100) / 100.0f;
        _provider.SetEndpointVolume(endpointId, scalar, isMuted);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<(int VolumePercent, bool IsMuted)> GetEndpointVolumeAsync(string endpointId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        var (scalar, isMuted) = _provider.GetEndpointVolume(endpointId);
        int percent = (int)Math.Round(Math.Clamp(scalar, 0.0f, 1.0f) * 100.0f);
        return Task.FromResult((percent, isMuted));
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
