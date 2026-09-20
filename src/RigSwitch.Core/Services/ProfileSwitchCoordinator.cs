namespace RigSwitch.Core.Services;

using RigSwitch.Core.Enums;
using RigSwitch.Core.Events;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Coordinates atomic workstation profile transitions across display, audio, and configuration subsystems.
/// </summary>
public sealed class ProfileSwitchCoordinator : IProfileSwitchCoordinator, IDisposable
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
            var displays = await _displayConfigService.EnumerateDisplaysAsync(cancellationToken).ConfigureAwait(false);
            var isTargetPresent = displays.Any(d =>
                string.Equals(d.MonitorId, targetMonitorId, StringComparison.OrdinalIgnoreCase));

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
                if (!string.IsNullOrWhiteSpace(settings.DeskPrimaryAudioId) &&
                    endpoints.Any(e => string.Equals(e.Id, settings.DeskPrimaryAudioId, StringComparison.OrdinalIgnoreCase) && e.State == DevicePresenceState.Active))
                {
                    resolvedAudioId = settings.DeskPrimaryAudioId;
                }
                else if (!string.IsNullOrWhiteSpace(settings.DeskFallbackAudioId) &&
                         endpoints.Any(e => string.Equals(e.Id, settings.DeskFallbackAudioId, StringComparison.OrdinalIgnoreCase) && e.State == DevicePresenceState.Active))
                {
                    resolvedAudioId = settings.DeskFallbackAudioId;
                }
                else
                {
                    var firstActive = endpoints.FirstOrDefault(e => e.State == DevicePresenceState.Active);
                    resolvedAudioId = firstActive?.Id ?? settings.DeskPrimaryAudioId;
                }
            }
            else
            {
                resolvedAudioId = settings.RigPrimaryAudioId;
            }

            if (!string.IsNullOrWhiteSpace(resolvedAudioId))
            {
                await _audioDirector.SetDefaultPlaybackEndpointAsync(resolvedAudioId, cancellationToken).ConfigureAwait(false);
            }

            if (settings.HiddenAudioEndpointIds is { Count: > 0 })
            {
                await _audioDirector.SyncHiddenEndpointsAsync(settings.HiddenAudioEndpointIds, cancellationToken).ConfigureAwait(false);
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
