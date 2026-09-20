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
    public void SetCurrentProfile(ProfileMode profile)
    {
        CurrentProfile = profile;
    }

    /// <inheritdoc />
    public event EventHandler<ProfileChangedEventArgs>? ProfileChanged;

    /// <inheritdoc />
    public async Task<bool> SwitchProfileAsync(ProfileMode targetProfile, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var previousProfile = CurrentProfile;

        try
        {
            // Step 2: Load current settings
            var settings = await _settingsStorageService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);

            var targetMonitorId = targetProfile == ProfileMode.Desk
                ? settings.DeskMonitorId
                : settings.RigMonitorId;

            var inactiveMonitorId = targetProfile == ProfileMode.Desk
                ? settings.RigMonitorId
                : settings.DeskMonitorId;

            // Step 3: Safety Gate (Reachability Verification)
            if (string.IsNullOrWhiteSpace(targetMonitorId))
            {
                var errorMsg = $"No target display configured for profile '{targetProfile}'. Please select your display in Settings.";
                ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, success: false, errorMessage: errorMsg));
                return false;
            }

            var displays = await _displayConfigService.EnumerateDisplaysAsync(cancellationToken).ConfigureAwait(false);
            var isTargetPresent = IsDisplayConnected(targetMonitorId, displays);

            if (!isTargetPresent)
            {
                var errorMsg = $"Target monitor '{targetMonitorId}' for profile '{targetProfile}' was not found among connected displays.";
                ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, success: false, errorMessage: errorMsg));
                return false;
            }

            // Step 4: Apply Display Topology
            await _displayConfigService.ApplySingleDisplayTopologyAsync(targetMonitorId, inactiveMonitorId, cancellationToken).ConfigureAwait(false);

            // Step 5: Multi-tier Audio Resolution & Routing
            var endpoints = await _audioDirector.EnumerateAudioEndpointsAsync(cancellationToken).ConfigureAwait(false);
            string resolvedAudioId;

            if (targetProfile == ProfileMode.Desk)
            {
                var primaryMatch = endpoints.FirstOrDefault(e => MatchesEndpoint(e.Id, settings.DeskPrimaryAudioId) && e.State == DevicePresenceState.Active);
                var fallbackMatch = endpoints.FirstOrDefault(e => MatchesEndpoint(e.Id, settings.DeskFallbackAudioId) && e.State == DevicePresenceState.Active);

                if (!string.IsNullOrWhiteSpace(settings.DeskPrimaryAudioId) && primaryMatch != null)
                {
                    resolvedAudioId = primaryMatch.Id;
                }
                else if (!string.IsNullOrWhiteSpace(settings.DeskFallbackAudioId) && fallbackMatch != null)
                {
                    resolvedAudioId = fallbackMatch.Id;
                }
                else
                {
                    var firstActive = endpoints.FirstOrDefault(e => e.State == DevicePresenceState.Active);
                    resolvedAudioId = firstActive?.Id ??
                        (endpoints.FirstOrDefault(e => MatchesEndpoint(e.Id, settings.DeskPrimaryAudioId))?.Id ?? settings.DeskPrimaryAudioId);
                }
            }
            else
            {
                var rigMatch = endpoints.FirstOrDefault(e => MatchesEndpoint(e.Id, settings.RigPrimaryAudioId));
                resolvedAudioId = rigMatch?.Id ?? settings.RigPrimaryAudioId;
            }

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

            // Step 6: State & Settings Persistence
            CurrentProfile = targetProfile;
            settings.LastActiveProfile = targetProfile;
            await _settingsStorageService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

            ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, success: true));
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(previousProfile, targetProfile, success: false, errorMessage: ex.Message));
            return false;
        }
        finally
        {
            _gate.Release();
        }
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
