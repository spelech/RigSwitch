namespace RigSwitch.Infrastructure.Windows.CoreAudio;

using System.Diagnostics;
using System.Text.RegularExpressions;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Monitors Windows CoreAudio default playback endpoints in the background,
/// enforcing primary speaker routing and immediately reverting unauthorized shifts to blacklisted devices.
/// </summary>
public sealed partial class AudioEndpointWatchdogService : IAudioEndpointWatchdogService
{
    private readonly INativeAudioProvider _provider;
    private readonly IAudioEndpointDirector _director;
    private readonly ISettingsStorageService _storage;
    private readonly int _debounceMs;

    private readonly object _syncLock = new();
    private readonly SemaphoreSlim _reentrancyLock = new(1, 1);

    private CancellationTokenSource? _debounceCts;
    private string? _activeExpectedEndpointId;
    private string? _intentionalSwitchEndpointId;
    private DateTime _intentionalSwitchTimestamp = DateTime.MinValue;

    private bool _isRunning;
    private bool _disposed;

    /// <inheritdoc/>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioEndpointWatchdogService"/> class.
    /// </summary>
    /// <param name="provider">The native audio provider exposing CoreAudio notifications.</param>
    /// <param name="director">The audio endpoint director for routing default audio devices.</param>
    /// <param name="storage">The settings storage service to load user settings.</param>
    /// <param name="debounceMs">The debounce delay window in milliseconds (default is 200ms).</param>
    public AudioEndpointWatchdogService(
        INativeAudioProvider provider,
        IAudioEndpointDirector director,
        ISettingsStorageService storage,
        int debounceMs = 200)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _director = director ?? throw new ArgumentNullException(nameof(director));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _debounceMs = debounceMs;
    }

    /// <inheritdoc/>
    public void Start()
    {
        lock (_syncLock)
        {
            if (_disposed || _isRunning)
            {
                return;
            }

            _isRunning = true;
            _provider.DefaultDeviceChanged += OnDefaultDeviceChanged;
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_syncLock)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _provider.DefaultDeviceChanged -= OnDefaultDeviceChanged;

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }

    /// <inheritdoc/>
    public void NotifyIntentionalSwitch(string endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return;
        }

        lock (_syncLock)
        {
            _activeExpectedEndpointId = endpointId;
            _intentionalSwitchEndpointId = endpointId;
            _intentionalSwitchTimestamp = DateTime.UtcNow;

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }

    private void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string defaultDeviceId)
    {
        if (!_isRunning || _disposed)
        {
            return;
        }

        if (flow != EDataFlow.eRender || (role != ERole.eConsole && role != ERole.eMultimedia))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(defaultDeviceId))
        {
            return;
        }

        lock (_syncLock)
        {
            if (!_isRunning || _disposed)
            {
                return;
            }

            if (IsExpectedOrIntentional(defaultDeviceId))
            {
                return;
            }

            var oldCts = _debounceCts;
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            oldCts?.Cancel();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_debounceMs, token).ConfigureAwait(false);
                    await EvaluateEndpointChangeAsync(defaultDeviceId, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Debounced by newer event or explicitly stopped
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AudioEndpointWatchdogService] Error processing device change: {ex}");
                }
            }, token);
        }
    }

    private async Task EvaluateEndpointChangeAsync(string newEndpointId, CancellationToken cancellationToken)
    {
        await _reentrancyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (cancellationToken.IsCancellationRequested || !_isRunning || _disposed)
            {
                return;
            }

            lock (_syncLock)
            {
                if (IsExpectedOrIntentional(newEndpointId))
                {
                    return;
                }
            }

            var settings = await _storage.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            if (settings == null)
            {
                return;
            }

            var blacklisted = settings.BlacklistedAudioEndpointIds;
            var renderEndpoints = TryGetRenderEndpoints();

            bool isBlacklisted = blacklisted?.Any(b => MatchesEndpoint(b, newEndpointId)) == true;
            if (isBlacklisted)
            {
                string? targetId = null;
                lock (_syncLock)
                {
                    if (IsEndpointAvailable(_activeExpectedEndpointId, blacklisted, renderEndpoints))
                    {
                        targetId = _activeExpectedEndpointId;
                    }
                }

                if (string.IsNullOrWhiteSpace(targetId))
                {
                    var activePreset = settings.GetActivePreset(settings.LastActiveProfile);
                    targetId = ResolveEffectiveTargetEndpoint(activePreset, settings.ActiveAudioOutputMode, blacklisted, renderEndpoints);
                }

                if (string.IsNullOrWhiteSpace(targetId) && renderEndpoints is { Count: > 0 })
                {
                    targetId = renderEndpoints
                        .FirstOrDefault(e => e.State == DevicePresenceState.Active && blacklisted?.Any(b => MatchesEndpoint(b, e.Id)) != true)
                        ?.Id;
                }

                if (!string.IsNullOrWhiteSpace(targetId))
                {
                    lock (_syncLock)
                    {
                        _activeExpectedEndpointId = targetId;
                    }

                    await _director.SetDefaultPlaybackEndpointAsync(targetId, cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            // Enforce primary audio (or its active fallback) when enabled and in Primary audio mode
            if (settings.EnforcePrimaryAudio && settings.ActiveAudioOutputMode == AudioOutputMode.Primary)
            {
                var activePreset = settings.GetActivePreset(settings.LastActiveProfile);
                var effectiveTargetId = ResolveEffectiveTargetEndpoint(
                    activePreset,
                    AudioOutputMode.Primary,
                    blacklisted,
                    renderEndpoints);

                if (!string.IsNullOrWhiteSpace(effectiveTargetId) &&
                    !MatchesEndpoint(effectiveTargetId, newEndpointId))
                {
                    lock (_syncLock)
                    {
                        _activeExpectedEndpointId = effectiveTargetId;
                    }

                    await _director.SetDefaultPlaybackEndpointAsync(effectiveTargetId, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            // Allowed human switch between valid non-blacklisted devices
            lock (_syncLock)
            {
                _activeExpectedEndpointId = newEndpointId;
            }
        }
        finally
        {
            _reentrancyLock.Release();
        }
    }

    private IReadOnlyList<AudioEndpointInfo>? TryGetRenderEndpoints()
    {
        try
        {
            return _provider.EnumerateRenderEndpoints();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioEndpointWatchdogService] Error enumerating render endpoints: {ex}");
            return null;
        }
    }

    private static string? ResolveEffectiveTargetEndpoint(
        WorkstationPreset activePreset,
        AudioOutputMode mode,
        IReadOnlyList<string>? blacklisted,
        IReadOnlyList<AudioEndpointInfo>? renderEndpoints)
    {
        if (activePreset.AudioPriorityIds is { Count: > 0 })
        {
            foreach (var candidateId in activePreset.AudioPriorityIds)
            {
                if (IsEndpointAvailable(candidateId, blacklisted, renderEndpoints))
                {
                    return candidateId;
                }
            }

            return null;
        }

        var preferredId = (mode == AudioOutputMode.Secondary && !string.IsNullOrWhiteSpace(activePreset.SecondaryAudioId))
            ? activePreset.SecondaryAudioId
            : activePreset.PrimaryAudioId;

        if (IsEndpointAvailable(preferredId, blacklisted, renderEndpoints))
        {
            return preferredId;
        }

        if (IsEndpointAvailable(activePreset.FallbackAudioId, blacklisted, renderEndpoints))
        {
            return activePreset.FallbackAudioId;
        }

        return null;
    }

    private static bool IsEndpointAvailable(
        string? candidateId,
        IReadOnlyList<string>? blacklisted,
        IReadOnlyList<AudioEndpointInfo>? renderEndpoints)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return false;
        }

        if (blacklisted?.Any(b => MatchesEndpoint(b, candidateId)) == true)
        {
            return false;
        }

        if (renderEndpoints is { Count: > 0 })
        {
            return renderEndpoints.Any(e => e.State == DevicePresenceState.Active && MatchesEndpoint(candidateId, e.Id));
        }

        return true;
    }

    private bool IsExpectedOrIntentional(string endpointId)
    {
        if (!string.IsNullOrWhiteSpace(_activeExpectedEndpointId) &&
            MatchesEndpoint(endpointId, _activeExpectedEndpointId))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_intentionalSwitchEndpointId) &&
            MatchesEndpoint(endpointId, _intentionalSwitchEndpointId) &&
            DateTime.UtcNow - _intentionalSwitchTimestamp < TimeSpan.FromSeconds(5))
        {
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"(?:\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.RightToLeft)]
    private static partial Regex GuidPattern();

    private static bool MatchesEndpoint(string? idA, string? idB)
    {
        if (string.IsNullOrWhiteSpace(idA) || string.IsNullOrWhiteSpace(idB))
        {
            return false;
        }

        if (string.Equals(idA, idB, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var matchA = GuidPattern().Match(idA);
        var matchB = GuidPattern().Match(idB);

        if (matchA.Success && matchB.Success &&
            Guid.TryParse(matchA.Value, out var guidA) &&
            Guid.TryParse(matchB.Value, out var guidB))
        {
            return guidA == guidB;
        }

        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();

        _reentrancyLock.Dispose();
    }
}
