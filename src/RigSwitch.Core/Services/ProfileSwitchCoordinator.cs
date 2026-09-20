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
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileSwitchCoordinator"/> class.
    /// </summary>
    /// <param name="displayConfigService">The display configuration service.</param>
    /// <param name="audioDirector">The audio endpoint director.</param>
    /// <param name="settingsStorageService">The settings storage service.</param>
    /// <param name="initialProfile">The initial workstation profile mode.</param>
    public ProfileSwitchCoordinator(
        IDisplayConfigurationService displayConfigService,
        IAudioEndpointDirector audioDirector,
        ISettingsStorageService settingsStorageService,
        ProfileMode initialProfile = ProfileMode.Desk)
    {
        ArgumentNullException.ThrowIfNull(displayConfigService);
        ArgumentNullException.ThrowIfNull(audioDirector);
        ArgumentNullException.ThrowIfNull(settingsStorageService);

        _displayConfigService = displayConfigService;
        _audioDirector = audioDirector;
        _settingsStorageService = settingsStorageService;
        CurrentProfile = initialProfile;
    }

    /// <inheritdoc />
    public ProfileMode CurrentProfile { get; private set; }

    /// <inheritdoc />
    public int CurrentPresetIndex { get; private set; }

    /// <inheritdoc />
    public WorkstationPreset CurrentPreset { get; private set; } = new();

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

    private async Task<bool> SwitchCoreAsync(ProfileMode targetProfile, int? targetPresetIndex, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var previousProfile = CurrentProfile;
        WorkstationPreset? preset = null;

        try
        {
            // Step 1: Load current settings and resolve target preset
            var settings = await _settingsStorageService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);

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
            var inactiveMonitorId = inactivePreset.TargetMonitorId;

            // Step 2: Safety Gate (Reachability Verification)
            if (string.IsNullOrWhiteSpace(preset.TargetMonitorId))
            {
                var errorMsg = $"No target display configured for profile '{targetProfile}'. Please select your display in Settings.";
                ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, activePreset: preset, success: false, errorMessage: errorMsg));
                return false;
            }

            var displays = await _displayConfigService.EnumerateDisplaysAsync(cancellationToken).ConfigureAwait(false);
            var isTargetPresent = IsDisplayConnected(preset.TargetMonitorId, displays);

            if (!isTargetPresent)
            {
                var errorMsg = $"Target monitor '{preset.TargetMonitorId}' for profile '{targetProfile}' was not found among connected displays.";
                ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, activePreset: preset, success: false, errorMessage: errorMsg));
                return false;
            }

            // Step 3: Apply Display Topology
            await _displayConfigService.ApplySingleDisplayTopologyAsync(preset.TargetMonitorId, inactiveMonitorId, cancellationToken).ConfigureAwait(false);

            // Step 4: Multi-tier Audio Resolution & Routing
            var endpoints = await _audioDirector.EnumerateAudioEndpointsAsync(cancellationToken).ConfigureAwait(false);
            var resolvedAudioId = ResolveAudioEndpoint(preset.PrimaryAudioId, preset.FallbackAudioId, endpoints);

            if (!string.IsNullOrWhiteSpace(resolvedAudioId))
            {
                await _audioDirector.SetDefaultPlaybackEndpointAsync(resolvedAudioId, cancellationToken).ConfigureAwait(false);
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

            // Step 5: State & Settings Persistence
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

    private static string? ResolveAudioEndpoint(
        string? primaryAudioId,
        string? fallbackAudioId,
        IEnumerable<AudioEndpointInfo> endpoints)
    {
        if (string.IsNullOrWhiteSpace(primaryAudioId) && string.IsNullOrWhiteSpace(fallbackAudioId))
        {
            return null;
        }

        var endpointList = endpoints as IList<AudioEndpointInfo> ?? endpoints.ToList();
        var primaryMatch = endpointList.FirstOrDefault(e => MatchesEndpoint(e.Id, primaryAudioId) && e.State == DevicePresenceState.Active);
        var fallbackMatch = endpointList.FirstOrDefault(e => MatchesEndpoint(e.Id, fallbackAudioId) && e.State == DevicePresenceState.Active);

        if (!string.IsNullOrWhiteSpace(primaryAudioId) && primaryMatch != null)
        {
            return primaryMatch.Id;
        }

        if (!string.IsNullOrWhiteSpace(fallbackAudioId) && fallbackMatch != null)
        {
            return fallbackMatch.Id;
        }

        var firstActive = endpointList.FirstOrDefault(e => e.State == DevicePresenceState.Active);
        if (firstActive != null)
        {
            return firstActive.Id;
        }

        var targetId = !string.IsNullOrWhiteSpace(primaryAudioId) ? primaryAudioId : fallbackAudioId;
        var directMatch = endpointList.FirstOrDefault(e => MatchesEndpoint(e.Id, targetId));
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
