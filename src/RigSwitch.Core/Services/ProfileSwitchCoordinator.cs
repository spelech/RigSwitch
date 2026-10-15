namespace RigSwitch.Core.Services;

using System.Text.RegularExpressions;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Events;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Coordinates atomic workstation profile transitions across display, audio, and configuration subsystems.
/// </summary>
public sealed partial class ProfileSwitchCoordinator : IProfileSwitchCoordinator, IDisposable
{
    private readonly IDisplayConfigurationService _displayConfigService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly ISettingsStorageService _settingsStorageService;
    private readonly IApplicationLifecycleHookService? _appHookService;
    private readonly Action<string>? _onIntentionalAudioSwitch;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;
    private UserSettings? _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileSwitchCoordinator"/> class.
    /// </summary>
    /// <param name="displayConfigService">The display configuration service.</param>
    /// <param name="audioDirector">The audio endpoint director.</param>
    /// <param name="settingsStorageService">The settings storage service.</param>
    /// <param name="initialProfile">The initial workstation profile mode.</param>
    /// <param name="appHookService">Optional application lifecycle hook service.</param>
    /// <param name="onIntentionalAudioSwitch">Optional callback invoked with the resolved endpoint ID immediately before switching playback audio.</param>
    public ProfileSwitchCoordinator(
        IDisplayConfigurationService displayConfigService,
        IAudioEndpointDirector audioDirector,
        ISettingsStorageService settingsStorageService,
        ProfileMode initialProfile = ProfileMode.Desk,
        IApplicationLifecycleHookService? appHookService = null,
        Action<string>? onIntentionalAudioSwitch = null)
    {
        ArgumentNullException.ThrowIfNull(displayConfigService);
        ArgumentNullException.ThrowIfNull(audioDirector);
        ArgumentNullException.ThrowIfNull(settingsStorageService);

        _displayConfigService = displayConfigService;
        _audioDirector = audioDirector;
        _settingsStorageService = settingsStorageService;
        _appHookService = appHookService;
        _onIntentionalAudioSwitch = onIntentionalAudioSwitch;
        CurrentProfile = initialProfile;
    }

    /// <inheritdoc />
    public ProfileMode CurrentProfile { get; private set; }

    /// <inheritdoc />
    public int CurrentPresetIndex { get; private set; }

    /// <inheritdoc />
    public WorkstationPreset CurrentPreset { get; private set; } = new();

    /// <inheritdoc />
    public AudioOutputMode CurrentAudioOutputMode => _settings?.ActiveAudioOutputMode ?? AudioOutputMode.Primary;

    /// <inheritdoc />
    public void SetCurrentProfile(ProfileMode profile)
    {
        CurrentProfile = profile;
    }

    /// <inheritdoc />
    public void SetCurrentPreset(ProfileMode profile, int presetIndex)
    {
        CurrentProfile = profile;
        CurrentPresetIndex = presetIndex;
    }

    /// <inheritdoc />
    public Task<bool> SwitchToPresetAsync(ProfileMode targetProfile, int presetIndex, CancellationToken cancellationToken = default)
    {
        return SwitchCoreAsync(targetProfile, presetIndex, cancellationToken);
    }

    /// <inheritdoc />
    public event EventHandler<ProfileChangedEventArgs>? ProfileChanged;

    /// <inheritdoc />
    public Task<bool> SwitchProfileAsync(ProfileMode targetProfile, CancellationToken cancellationToken = default)
    {
        return SwitchCoreAsync(targetProfile, targetPresetIndex: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AudioOutputMode> ToggleAudioOutputModeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = await _settingsStorageService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            _settings = settings;

            var newMode = settings.ActiveAudioOutputMode == AudioOutputMode.Primary
                ? AudioOutputMode.Secondary
                : AudioOutputMode.Primary;
            settings.ActiveAudioOutputMode = newMode;

            var preset = settings.GetActivePreset(CurrentProfile);
            CurrentPreset = preset;

            var endpoints = await _audioDirector.EnumerateAudioEndpointsAsync(cancellationToken).ConfigureAwait(false);
            var endpointList = endpoints as IList<AudioEndpointInfo> ?? endpoints.ToList();
            var blacklisted = settings.BlacklistedAudioEndpointIds ?? [];
            var allowedEndpoints = endpointList.Where(e => !blacklisted.Any(b => MatchesEndpoint(b, e.Id))).ToList();
            var playbackEndpoints = allowedEndpoints.Where(e => e.Flow == AudioDeviceFlow.Playback).ToList();
            var captureEndpoints = allowedEndpoints.Where(e => e.Flow == AudioDeviceFlow.Capture).ToList();

            string? resolvedAudioId;
            string? resolvedMicId;

            if (preset.AudioPriorityIds is { Count: > 0 })
            {
                resolvedAudioId = ResolveNextCycledRankedEndpoint(preset.AudioPriorityIds, playbackEndpoints, blacklisted, isPlayback: true);
            }
            else
            {
                var targetAudioId = newMode == AudioOutputMode.Secondary && !string.IsNullOrWhiteSpace(preset.SecondaryAudioId)
                    ? preset.SecondaryAudioId
                    : preset.PrimaryAudioId;
                resolvedAudioId = ResolveAudioEndpoint(targetAudioId, preset.FallbackAudioId, playbackEndpoints, blacklisted);
            }

            if (preset.MicrophonePriorityIds is { Count: > 0 })
            {
                resolvedMicId = ResolveNextCycledRankedEndpoint(preset.MicrophonePriorityIds, captureEndpoints, blacklisted, isPlayback: false);
            }
            else
            {
                var targetMicId = newMode == AudioOutputMode.Secondary && !string.IsNullOrWhiteSpace(preset.SecondaryMicrophoneId)
                    ? preset.SecondaryMicrophoneId
                    : preset.PrimaryMicrophoneId;
                resolvedMicId = ResolveAudioEndpoint(targetMicId, preset.FallbackMicrophoneId, captureEndpoints, blacklisted);
            }

            if (!string.IsNullOrWhiteSpace(resolvedAudioId))
            {
                _onIntentionalAudioSwitch?.Invoke(resolvedAudioId);
                await _audioDirector.SetDefaultPlaybackEndpointAsync(resolvedAudioId, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(resolvedMicId))
            {
                await _audioDirector.SetDefaultCaptureEndpointAsync(resolvedMicId, cancellationToken).ConfigureAwait(false);
            }

            await _settingsStorageService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
            return newMode;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> SwitchCoreAsync(ProfileMode targetProfile, int? targetPresetIndex, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var previousProfile = CurrentProfile;
        WorkstationPreset? previousPreset = null;
        WorkstationPreset? preset = null;

        try
        {
            // Step 1: Load current settings and resolve target preset
            var settings = await _settingsStorageService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            _settings = settings;
            previousPreset = settings.GetActivePreset(previousProfile);

            if (targetPresetIndex.HasValue)
            {
                if (targetProfile == ProfileMode.Desk)
                {
                    settings.ActiveDeskPresetIndex = targetPresetIndex.Value;
                }
                else
                {
                    settings.ActiveRigPresetIndex = targetPresetIndex.Value;
                }
            }

            preset = settings.GetActivePreset(targetProfile);
            var activePresetIndex = targetProfile == ProfileMode.Desk
                ? settings.ActiveDeskPresetIndex
                : settings.ActiveRigPresetIndex;

            var inactiveProfile = targetProfile == ProfileMode.Desk
                ? ProfileMode.SimRig
                : ProfileMode.Desk;
            var inactivePreset = settings.GetActivePreset(inactiveProfile);
            var inactiveMonitorIds = inactivePreset.TargetMonitorIds;

            var targetMonitorIds = preset.TargetMonitorIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (targetMonitorIds.Count == 0 && preset.MonitorPlacements is { Count: > 0 })
            {
                var orderedPlacements = preset.MonitorPlacements
                    .OrderByDescending(p => p.IsPrimary)
                    .Where(p => !string.IsNullOrWhiteSpace(p.MonitorId))
                    .Select(p => p.MonitorId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                targetMonitorIds = orderedPlacements;
            }

            // Step 2: Safety Gate (Reachability Verification)
            if (targetMonitorIds.Count == 0)
            {
                var errorMsg = $"No target display configured for profile '{targetProfile}'. Please select your display in Settings.";
                ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, activePreset: preset, success: false, errorMessage: errorMsg));
                return false;
            }

            var displays = await _displayConfigService.EnumerateDisplaysAsync(cancellationToken).ConfigureAwait(false);
            var missingMonitors = targetMonitorIds.Where(id => !IsDisplayConnected(id, displays)).ToList();

            if (missingMonitors.Count > 0)
            {
                var errorMsg = $"Target monitor(s) '{string.Join(", ", missingMonitors)}' for profile '{targetProfile}' was not found among connected displays.";
                ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, activePreset: preset, success: false, errorMessage: errorMsg));
                return false;
            }

            // Step 3: Apply Display Topology
            if (preset.MonitorPlacements is { Count: > 0 })
            {
                await _displayConfigService.ApplyDisplayTopologyAsync(targetMonitorIds, inactiveMonitorIds, preset.MonitorPlacements, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _displayConfigService.ApplyDisplayTopologyAsync(targetMonitorIds, inactiveMonitorIds, cancellationToken).ConfigureAwait(false);
            }

            // Step 3B: Apply Native HDR Mode (per-monitor placement or preset-level)
            foreach (var monitorId in targetMonitorIds)
            {
                var placementHdr = preset.MonitorPlacements?.FirstOrDefault(p => string.Equals(p.MonitorId, monitorId, StringComparison.OrdinalIgnoreCase))?.HdrMode;
                var effectiveHdrMode = placementHdr is not null && placementHdr != PresetHdrMode.Retain
                    ? placementHdr.Value
                    : preset.HdrMode;

                if (effectiveHdrMode != PresetHdrMode.Retain)
                {
                    bool enableHdr = effectiveHdrMode == PresetHdrMode.Enable;
                    try
                    {
                        await _displayConfigService.SetHdrStateAsync(monitorId, enableHdr, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceWarning($"Failed to apply HDR mode ({effectiveHdrMode}) on monitor '{monitorId}': {ex.Message}");
                    }
                }
            }

            // Step 4: Multi-tier / Ranked Audio Resolution & Routing
            var endpoints = await _audioDirector.EnumerateAudioEndpointsAsync(cancellationToken).ConfigureAwait(false);
            var endpointList = endpoints as IList<AudioEndpointInfo> ?? endpoints.ToList();
            var blacklisted = settings.BlacklistedAudioEndpointIds ?? [];
            var allowedEndpoints = endpointList.Where(e => !blacklisted.Any(b => MatchesEndpoint(b, e.Id))).ToList();
            var playbackEndpoints = allowedEndpoints.Where(e => e.Flow == AudioDeviceFlow.Playback).ToList();
            var captureEndpoints = allowedEndpoints.Where(e => e.Flow == AudioDeviceFlow.Capture).ToList();

            string? resolvedAudioId;
            if (preset.AudioPriorityIds is { Count: > 0 })
            {
                resolvedAudioId = ResolveRankedAudioEndpoint(preset.AudioPriorityIds, playbackEndpoints, blacklisted);
            }
            else
            {
                var targetAudioId = settings.ActiveAudioOutputMode == AudioOutputMode.Secondary && !string.IsNullOrWhiteSpace(preset.SecondaryAudioId)
                    ? preset.SecondaryAudioId
                    : preset.PrimaryAudioId;
                resolvedAudioId = ResolveAudioEndpoint(targetAudioId, preset.FallbackAudioId, playbackEndpoints, blacklisted);
            }

            if (!string.IsNullOrWhiteSpace(resolvedAudioId))
            {
                _onIntentionalAudioSwitch?.Invoke(resolvedAudioId);
                await _audioDirector.SetDefaultPlaybackEndpointAsync(resolvedAudioId, cancellationToken).ConfigureAwait(false);
            }

            string? resolvedMicId;
            if (preset.MicrophonePriorityIds is { Count: > 0 })
            {
                resolvedMicId = ResolveRankedAudioEndpoint(preset.MicrophonePriorityIds, captureEndpoints, blacklisted);
            }
            else
            {
                var targetMicId = settings.ActiveAudioOutputMode == AudioOutputMode.Secondary && !string.IsNullOrWhiteSpace(preset.SecondaryMicrophoneId)
                    ? preset.SecondaryMicrophoneId
                    : preset.PrimaryMicrophoneId;
                resolvedMicId = ResolveAudioEndpoint(targetMicId, preset.FallbackMicrophoneId, captureEndpoints, blacklisted);
            }

            if (!string.IsNullOrWhiteSpace(resolvedMicId))
            {
                await _audioDirector.SetDefaultCaptureEndpointAsync(resolvedMicId, cancellationToken).ConfigureAwait(false);
            }

            // Apply Volume Controls (if configured)
            if (preset.PlaybackVolume.Mode == PresetVolumeBehavior.Custom)
            {
                var targetPlaybackId = resolvedAudioId;
                if (string.IsNullOrWhiteSpace(targetPlaybackId))
                {
                    targetPlaybackId = playbackEndpoints.FirstOrDefault(e => e.IsDefaultPlayback)?.Id
                        ?? allowedEndpoints.FirstOrDefault(e => e.Flow == AudioDeviceFlow.Playback && e.State == DevicePresenceState.Active)?.Id;
                }

                if (!string.IsNullOrWhiteSpace(targetPlaybackId))
                {
                    try
                    {
                        await _audioDirector.SetEndpointVolumeAsync(
                            targetPlaybackId,
                            preset.PlaybackVolume.VolumePercent,
                            preset.PlaybackVolume.IsMuted,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceWarning($"Failed to apply playback volume for preset '{preset.Name}': {ex.Message}");
                    }
                }
            }

            if (preset.MicrophoneVolume.Mode == PresetVolumeBehavior.Custom)
            {
                var targetMicIdVol = resolvedMicId;
                if (string.IsNullOrWhiteSpace(targetMicIdVol))
                {
                    targetMicIdVol = captureEndpoints.FirstOrDefault(e => e.IsDefaultCapture)?.Id
                        ?? allowedEndpoints.FirstOrDefault(e => e.Flow == AudioDeviceFlow.Capture && e.State == DevicePresenceState.Active)?.Id;
                }

                if (!string.IsNullOrWhiteSpace(targetMicIdVol))
                {
                    try
                    {
                        await _audioDirector.SetEndpointVolumeAsync(
                            targetMicIdVol,
                            preset.MicrophoneVolume.VolumePercent,
                            preset.MicrophoneVolume.IsMuted,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceWarning($"Failed to apply microphone volume for preset '{preset.Name}': {ex.Message}");
                    }
                }
            }

            if (settings.HiddenAudioEndpointIds is { Count: > 0 })
            {
                try
                {
                    await _audioDirector.SyncHiddenEndpointsAsync(settings.HiddenAudioEndpointIds, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning($"Failed to synchronize hidden audio endpoints: {ex.Message}");
                }
            }

            // Step 5: Application Lifecycle Hooks
            if (_appHookService != null)
            {
                if (previousPreset != null)
                {
                    try
                    {
                        await _appHookService.CloseHooksForPresetAsync(previousPreset, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceWarning($"Failed to close application hooks for preset '{previousPreset.Name}': {ex.Message}");
                    }
                }

                try
                {
                    await _appHookService.LaunchHooksForPresetAsync(preset, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning($"Failed to launch application hooks for preset '{preset.Name}': {ex.Message}");
                }
            }

            // Step 6: State & Settings Persistence
            CurrentProfile = targetProfile;
            CurrentPresetIndex = activePresetIndex;
            CurrentPreset = preset;
            settings.LastActiveProfile = targetProfile;
            await _settingsStorageService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

            ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, activePreset: preset, success: true));
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, activePreset: preset, success: false, errorMessage: ex.Message));
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string? ResolveRankedAudioEndpoint(
        IReadOnlyList<string> rankedIds,
        IList<AudioEndpointInfo> allowedEndpoints,
        IReadOnlyList<string>? blacklisted = null)
    {
        foreach (var candidateId in rankedIds)
        {
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                continue;
            }

            if (blacklisted?.Any(b => MatchesEndpoint(b, candidateId)) == true)
            {
                continue;
            }

            var activeMatch = allowedEndpoints.FirstOrDefault(e => MatchesEndpoint(e.Id, candidateId) && e.State == DevicePresenceState.Active);
            if (activeMatch != null)
            {
                return activeMatch.Id;
            }
        }

        var firstActive = allowedEndpoints.FirstOrDefault(e => e.State == DevicePresenceState.Active);
        return firstActive?.Id;
    }

    private static string? ResolveNextCycledRankedEndpoint(
        IReadOnlyList<string> rankedIds,
        IList<AudioEndpointInfo> allowedEndpoints,
        IReadOnlyList<string>? blacklisted,
        bool isPlayback)
    {
        var activeRanked = new List<AudioEndpointInfo>();
        foreach (var candidateId in rankedIds)
        {
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                continue;
            }

            if (blacklisted?.Any(b => MatchesEndpoint(b, candidateId)) == true)
            {
                continue;
            }

            var match = allowedEndpoints.FirstOrDefault(e => MatchesEndpoint(e.Id, candidateId) && e.State == DevicePresenceState.Active);
            if (match != null && !activeRanked.Any(x => MatchesEndpoint(x.Id, match.Id)))
            {
                activeRanked.Add(match);
            }
        }

        if (activeRanked.Count == 0)
        {
            return null;
        }

        if (activeRanked.Count == 1)
        {
            return activeRanked[0].Id;
        }

        int currentIdx = activeRanked.FindIndex(e => isPlayback ? e.IsDefaultPlayback : e.IsDefaultCapture);
        int nextIdx = currentIdx >= 0 ? (currentIdx + 1) % activeRanked.Count : 0;
        return activeRanked[nextIdx].Id;
    }

    private static string? ResolveAudioEndpoint(
        string? primaryAudioId,
        string? fallbackAudioId,
        IEnumerable<AudioEndpointInfo> endpoints,
        IReadOnlyList<string>? blacklisted = null)
    {
        if (string.IsNullOrWhiteSpace(primaryAudioId) && string.IsNullOrWhiteSpace(fallbackAudioId))
        {
            return null;
        }

        var isPrimaryBlacklisted = blacklisted?.Any(b => MatchesEndpoint(b, primaryAudioId)) == true;
        var isFallbackBlacklisted = blacklisted?.Any(b => MatchesEndpoint(b, fallbackAudioId)) == true;

        var effectivePrimary = isPrimaryBlacklisted ? null : primaryAudioId;
        var effectiveFallback = isFallbackBlacklisted ? null : fallbackAudioId;

        var candidates = endpoints as IList<AudioEndpointInfo> ?? endpoints.ToList();
        var allowedCandidates = blacklisted != null
            ? candidates.Where(e => !blacklisted.Any(b => MatchesEndpoint(b, e.Id))).ToList()
            : candidates;

        var primaryMatch = allowedCandidates.FirstOrDefault(e => MatchesEndpoint(e.Id, effectivePrimary) && e.State == DevicePresenceState.Active);
        var fallbackMatch = allowedCandidates.FirstOrDefault(e => MatchesEndpoint(e.Id, effectiveFallback) && e.State == DevicePresenceState.Active);

        if (!string.IsNullOrWhiteSpace(effectivePrimary) && primaryMatch != null)
        {
            return primaryMatch.Id;
        }

        if (!string.IsNullOrWhiteSpace(effectiveFallback) && fallbackMatch != null)
        {
            return fallbackMatch.Id;
        }

        var firstActive = allowedCandidates.FirstOrDefault(e => e.State == DevicePresenceState.Active);
        if (firstActive != null)
        {
            return firstActive.Id;
        }

        var targetId = !string.IsNullOrWhiteSpace(effectivePrimary) ? effectivePrimary : effectiveFallback;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return null;
        }

        var directMatch = allowedCandidates.FirstOrDefault(e => MatchesEndpoint(e.Id, targetId));
        return directMatch?.Id ?? targetId;
    }

    private static bool IsDisplayConnected(string targetMonitorId, IEnumerable<DisplayDeviceInfo> displays)
    {
        return displays.Any(d => d.Matches(targetMonitorId));
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

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }
}
