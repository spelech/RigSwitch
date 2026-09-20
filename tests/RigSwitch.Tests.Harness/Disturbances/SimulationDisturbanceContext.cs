namespace RigSwitch.Tests.Harness.Disturbances;

using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Core.Services;
using RigSwitch.Tests.Harness.Taps;

/// <summary>
/// Configurable simulated display and audio test harness environment with disturbance injection hooks.
/// </summary>
public sealed class SimulationDisturbanceContext
{
    private readonly object _stateLock = new();
    private readonly List<DisplayDeviceInfo> _displays = new();
    private readonly List<AudioEndpointInfo> _audioEndpoints = new();
    private readonly List<DisplayDeviceInfo> _initialDisplaysBackup = new();
    private readonly List<string> _logs = new();
    private TimeSpan _displaySwitchDelay = TimeSpan.Zero;
    private int _ccdErrorCode;

    public SimulationDisturbanceContext(UserSettings? initialSettings = null)
    {
        RingBuffer = new DiagnosticRingBuffer(100);
        Settings = initialSettings ?? new UserSettings();

        while (Settings.DeskPresets.Count < 3)
        {
            Settings.DeskPresets.Add(new WorkstationPreset { Name = $"Desk Preset {Settings.DeskPresets.Count + 1}" });
        }

        while (Settings.RigPresets.Count < 3)
        {
            Settings.RigPresets.Add(new WorkstationPreset { Name = $"Rig Preset {Settings.RigPresets.Count + 1}" });
        }

        Settings.DeskMonitorId = string.IsNullOrEmpty(Settings.DeskMonitorId) ? "DESK_MONITOR_1" : Settings.DeskMonitorId;
        Settings.RigMonitorId = string.IsNullOrEmpty(Settings.RigMonitorId) ? "RIG_MONITOR_1" : Settings.RigMonitorId;
        Settings.DeskPrimaryAudioId = string.IsNullOrEmpty(Settings.DeskPrimaryAudioId) ? "{00000000-0000-0000-0000-000000000001}" : Settings.DeskPrimaryAudioId;
        Settings.DeskFallbackAudioId = string.IsNullOrEmpty(Settings.DeskFallbackAudioId) ? "{00000000-0000-0000-0000-000000000002}" : Settings.DeskFallbackAudioId;
        Settings.RigPrimaryAudioId = string.IsNullOrEmpty(Settings.RigPrimaryAudioId) ? "{00000000-0000-0000-0000-000000000003}" : Settings.RigPrimaryAudioId;

        // Configure DeskPresets 0, 1, 2 with mock display and audio IDs if empty
        for (var i = 0; i < Settings.DeskPresets.Count; i++)
        {
            var preset = Settings.DeskPresets[i];
            preset.TargetMonitorId = string.IsNullOrEmpty(preset.TargetMonitorId) ? Settings.DeskMonitorId : preset.TargetMonitorId;
            preset.PrimaryAudioId = string.IsNullOrEmpty(preset.PrimaryAudioId)
                ? (i == 1 ? Settings.DeskFallbackAudioId : (i == 2 ? "{00000000-0000-0000-0000-000000000004}" : Settings.DeskPrimaryAudioId))
                : preset.PrimaryAudioId;
            preset.FallbackAudioId = string.IsNullOrEmpty(preset.FallbackAudioId) ? Settings.DeskFallbackAudioId : preset.FallbackAudioId;
        }

        // Configure RigPresets 0, 1, 2 with mock display and audio IDs if empty
        for (var i = 0; i < Settings.RigPresets.Count; i++)
        {
            var preset = Settings.RigPresets[i];
            preset.TargetMonitorId = string.IsNullOrEmpty(preset.TargetMonitorId) ? Settings.RigMonitorId : preset.TargetMonitorId;
            preset.PrimaryAudioId = string.IsNullOrEmpty(preset.PrimaryAudioId)
                ? (i == 1 ? "{00000000-0000-0000-0000-000000000005}" : Settings.RigPrimaryAudioId)
                : preset.PrimaryAudioId;
            preset.FallbackAudioId = string.IsNullOrEmpty(preset.FallbackAudioId) ? Settings.DeskFallbackAudioId : preset.FallbackAudioId;
        }

        // Default display topology
        _displays.Add(new DisplayDeviceInfo(
            Settings.DeskMonitorId,
            @"\\.\DISPLAY1",
            "MPG341CQPX OLED",
            "NVIDIA RTX 4070 Ti",
            isActive: true,
            isPrimary: true));

        _displays.Add(new DisplayDeviceInfo(
            Settings.RigMonitorId,
            @"\\.\DISPLAY2",
            "VG34VQL3A",
            "NVIDIA RTX 4070 Ti",
            isActive: false,
            isPrimary: false));

        foreach (var preset in Settings.DeskPresets.Concat(Settings.RigPresets))
        {
            if (!string.IsNullOrEmpty(preset.TargetMonitorId) &&
                !_displays.Any(d => string.Equals(d.MonitorId, preset.TargetMonitorId, StringComparison.OrdinalIgnoreCase)))
            {
                _displays.Add(new DisplayDeviceInfo(
                    preset.TargetMonitorId,
                    $@"\\.\DISPLAY_{_displays.Count + 1}",
                    preset.Name,
                    "NVIDIA RTX 4070 Ti",
                    isActive: false,
                    isPrimary: false));
            }
        }

        _initialDisplaysBackup.AddRange(_displays);

        // Default audio endpoints
        _audioEndpoints.Add(new AudioEndpointInfo(
            Settings.DeskPrimaryAudioId,
            "Speakers (Pebble V3)",
            "Realtek Audio",
            DevicePresenceState.Active,
            isDefaultPlayback: true,
            isDefaultCommunications: false));

        _audioEndpoints.Add(new AudioEndpointInfo(
            Settings.DeskFallbackAudioId,
            "MSI MPG341CQPX",
            "NVIDIA High Definition Audio",
            DevicePresenceState.Active,
            isDefaultPlayback: false,
            isDefaultCommunications: false));

        _audioEndpoints.Add(new AudioEndpointInfo(
            Settings.RigPrimaryAudioId,
            "VG34VQL3A",
            "NVIDIA High Definition Audio",
            DevicePresenceState.Active,
            isDefaultPlayback: false,
            isDefaultCommunications: false));

        _audioEndpoints.Add(new AudioEndpointInfo(
            "{00000000-0000-0000-0000-000000000004}",
            "Wireless Headset",
            "Realtek Audio",
            DevicePresenceState.Active,
            isDefaultPlayback: false,
            isDefaultCommunications: false));

        _audioEndpoints.Add(new AudioEndpointInfo(
            "{00000000-0000-0000-0000-000000000005}",
            "Rig Headset",
            "USB Audio Device",
            DevicePresenceState.Active,
            isDefaultPlayback: false,
            isDefaultCommunications: false));

        foreach (var preset in Settings.DeskPresets.Concat(Settings.RigPresets))
        {
            if (!string.IsNullOrEmpty(preset.PrimaryAudioId) &&
                !_audioEndpoints.Any(e => string.Equals(e.Id, preset.PrimaryAudioId, StringComparison.OrdinalIgnoreCase)))
            {
                _audioEndpoints.Add(new AudioEndpointInfo(
                    preset.PrimaryAudioId,
                    $"{preset.Name} Audio",
                    "Audio Adapter",
                    DevicePresenceState.Active,
                    isDefaultPlayback: false,
                    isDefaultCommunications: false));
            }
        }

        ActiveDefaultAudioId = Settings.DeskPrimaryAudioId;

        DisplayService = new SimulatedDisplayService(this);
        AudioDirector = new SimulatedAudioDirector(this);
        SettingsStorage = new SimulatedSettingsStorage(this);
    }

    public DiagnosticRingBuffer RingBuffer { get; }

    public List<string> Logs
    {
        get
        {
            lock (_stateLock)
            {
                return new List<string>(_logs);
            }
        }
    }

    public UserSettings Settings { get; private set; }

    public IDisplayConfigurationService DisplayService { get; }

    public IAudioEndpointDirector AudioDirector { get; }

    public ISettingsStorageService SettingsStorage { get; }

    public string? ActiveDefaultAudioId { get; private set; }

    public bool IsMonitorActive(string monitorId)
    {
        lock (_stateLock)
        {
            return _displays.Any(d =>
                string.Equals(d.MonitorId, monitorId, StringComparison.OrdinalIgnoreCase) && d.IsActive);
        }
    }

    public DevicePresenceState GetAudioDeviceState(string endpointId)
    {
        lock (_stateLock)
        {
            var match = _audioEndpoints.FirstOrDefault(e =>
                string.Equals(e.Id, endpointId, StringComparison.OrdinalIgnoreCase));
            return match?.State ?? DevicePresenceState.NotPresent;
        }
    }

    public void Log(string message)
    {
        lock (_stateLock)
        {
            _logs.Add($"[{DateTimeOffset.UtcNow:HH:mm:ss.fff}] {message}");
        }
    }

    public void SimulateTargetMonitorDisconnect(string monitorId)
    {
        lock (_stateLock)
        {
            _displays.RemoveAll(d => string.Equals(d.MonitorId, monitorId, StringComparison.OrdinalIgnoreCase));
            Log($"[Disturbance] Target monitor '{monitorId}' disconnected.");
            RingBuffer.Record("Disturbance_MonitorDisconnect", input: monitorId);
        }
    }

    public void SimulateTargetMonitorReconnect(string monitorId)
    {
        lock (_stateLock)
        {
            if (!_displays.Any(d => string.Equals(d.MonitorId, monitorId, StringComparison.OrdinalIgnoreCase)))
            {
                var original = _initialDisplaysBackup.FirstOrDefault(d =>
                    string.Equals(d.MonitorId, monitorId, StringComparison.OrdinalIgnoreCase));

                if (original != null)
                {
                    _displays.Add(original);
                }
                else
                {
                    _displays.Add(new DisplayDeviceInfo(monitorId, @"\\.\DISPLAY_X", "Reconnected Display", "Adapter", false, false));
                }

                Log($"[Disturbance] Target monitor '{monitorId}' reconnected.");
                RingBuffer.Record("Disturbance_MonitorReconnect", input: monitorId);
            }
        }
    }

    public void SimulateAudioDeviceStateChange(string endpointId, DevicePresenceState newState)
    {
        lock (_stateLock)
        {
            var index = _audioEndpoints.FindIndex(e => string.Equals(e.Id, endpointId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                var existing = _audioEndpoints[index];
                _audioEndpoints[index] = new AudioEndpointInfo(
                    existing.Id,
                    existing.Name,
                    existing.AdapterDescription,
                    newState,
                    existing.IsDefaultPlayback,
                    existing.IsDefaultCommunications);

                Log($"[Disturbance] Audio endpoint '{endpointId}' state changed to {newState}.");
                RingBuffer.Record("Disturbance_AudioStateChange", input: endpointId, result: newState);
            }
        }
    }

    public void SimulateDisplaySwitchDelay(TimeSpan delay)
    {
        lock (_stateLock)
        {
            _displaySwitchDelay = delay;
            Log($"[Disturbance] Display switch delay set to {delay.TotalMilliseconds}ms.");
        }
    }

    public void SimulateCcdFailure(int errorCode)
    {
        lock (_stateLock)
        {
            _ccdErrorCode = errorCode;
            Log($"[Disturbance] CCD failure set to 0x{errorCode:X8}.");
        }
    }

    public ProfileSwitchCoordinator CreateCoordinator(ProfileMode initialProfile = ProfileMode.Desk)
    {
        var coordinator = new ProfileSwitchCoordinator(DisplayService, AudioDirector, SettingsStorage, initialProfile);
        coordinator.ProfileChanged += (_, args) =>
        {
            Log($"[Coordinator] ProfileChanged: {args.PreviousProfile} -> {args.NewProfile}, Success={args.Success}, Error={args.ErrorMessage}");
            RingBuffer.Record("ProfileChanged", input: args.NewProfile, result: args.PreviousProfile, success: args.Success, errorMessage: args.ErrorMessage);
        };
        return coordinator;
    }

    private sealed class SimulatedDisplayService : IDisplayConfigurationService
    {
        private readonly SimulationDisturbanceContext _context;

        public SimulatedDisplayService(SimulationDisturbanceContext context)
        {
            _context = context;
        }

        public Task<IReadOnlyList<DisplayDeviceInfo>> EnumerateDisplaysAsync(CancellationToken cancellationToken = default)
        {
            lock (_context._stateLock)
            {
                IReadOnlyList<DisplayDeviceInfo> snapshot = _context._displays.ToList();
                return Task.FromResult(snapshot);
            }
        }

        public async Task ApplySingleDisplayTopologyAsync(
            string targetMonitorId,
            string? inactiveMonitorId,
            CancellationToken cancellationToken = default)
        {
            TimeSpan delay;
            int errorCode;

            lock (_context._stateLock)
            {
                delay = _context._displaySwitchDelay;
                errorCode = _context._ccdErrorCode;
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            if (errorCode != 0)
            {
                var exMsg = $"Simulated CCD failure with error code: 0x{errorCode:X8}";
                _context.Log($"[Display] {exMsg}");
                _context.RingBuffer.Record("ApplySingleDisplayTopology_Fail", input: targetMonitorId, errorMessage: exMsg, success: false);
                throw new InvalidOperationException(exMsg);
            }

            lock (_context._stateLock)
            {
                for (var i = 0; i < _context._displays.Count; i++)
                {
                    var d = _context._displays[i];
                    if (string.Equals(d.MonitorId, targetMonitorId, StringComparison.OrdinalIgnoreCase))
                    {
                        _context._displays[i] = new DisplayDeviceInfo(d.MonitorId, d.DevicePath, d.FriendlyName, d.DisplayAdapter, isActive: true, isPrimary: true);
                    }
                    else if (inactiveMonitorId != null && string.Equals(d.MonitorId, inactiveMonitorId, StringComparison.OrdinalIgnoreCase))
                    {
                        _context._displays[i] = new DisplayDeviceInfo(d.MonitorId, d.DevicePath, d.FriendlyName, d.DisplayAdapter, isActive: false, isPrimary: false);
                    }
                }

                _context.Log($"[Display] Topology applied: Target={targetMonitorId}, Inactive={inactiveMonitorId}");
                _context.RingBuffer.Record("ApplySingleDisplayTopology", input: targetMonitorId, result: inactiveMonitorId, success: true);
            }
        }
    }

    private sealed class SimulatedAudioDirector : IAudioEndpointDirector
    {
        private readonly SimulationDisturbanceContext _context;

        public SimulatedAudioDirector(SimulationDisturbanceContext context)
        {
            _context = context;
        }

        public Task<IReadOnlyList<AudioEndpointInfo>> EnumerateAudioEndpointsAsync(CancellationToken cancellationToken = default)
        {
            lock (_context._stateLock)
            {
                IReadOnlyList<AudioEndpointInfo> snapshot = _context._audioEndpoints.ToList();
                return Task.FromResult(snapshot);
            }
        }

        public Task SetDefaultPlaybackEndpointAsync(string endpointId, CancellationToken cancellationToken = default)
        {
            lock (_context._stateLock)
            {
                _context.ActiveDefaultAudioId = endpointId;
                for (var i = 0; i < _context._audioEndpoints.Count; i++)
                {
                    var ep = _context._audioEndpoints[i];
                    var isMatch = string.Equals(ep.Id, endpointId, StringComparison.OrdinalIgnoreCase);
                    _context._audioEndpoints[i] = new AudioEndpointInfo(ep.Id, ep.Name, ep.AdapterDescription, ep.State, isDefaultPlayback: isMatch, ep.IsDefaultCommunications);
                }

                _context.Log($"[Audio] SetDefaultPlaybackEndpoint: {endpointId}");
                _context.RingBuffer.Record("SetDefaultPlaybackEndpoint", input: endpointId, success: true);
            }

            return Task.CompletedTask;
        }

        public Task SetEndpointVisibilityAsync(string endpointId, bool isVisible, CancellationToken cancellationToken = default)
        {
            lock (_context._stateLock)
            {
                _context.Log($"[Audio] SetEndpointVisibility: {endpointId}, Visible={isVisible}");
                _context.RingBuffer.Record("SetEndpointVisibility", input: endpointId, result: isVisible, success: true);
            }

            return Task.CompletedTask;
        }

        public Task SyncHiddenEndpointsAsync(IEnumerable<string> hiddenEndpointIds, CancellationToken cancellationToken = default)
        {
            lock (_context._stateLock)
            {
                var count = hiddenEndpointIds.Count();
                _context.Log($"[Audio] SyncHiddenEndpoints: count={count}");
                _context.RingBuffer.Record("SyncHiddenEndpoints", input: count, success: true);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SimulatedSettingsStorage : ISettingsStorageService
    {
        private readonly SimulationDisturbanceContext _context;

        public SimulatedSettingsStorage(SimulationDisturbanceContext context)
        {
            _context = context;
        }

        public Task<UserSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        {
            lock (_context._stateLock)
            {
                return Task.FromResult(_context.Settings);
            }
        }

        public Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default)
        {
            lock (_context._stateLock)
            {
                _context.Settings = settings;
                _context.Log($"[Settings] Saved settings. LastActiveProfile={settings.LastActiveProfile}");
                _context.RingBuffer.Record("SaveSettings", input: settings.LastActiveProfile, success: true);
            }

            return Task.CompletedTask;
        }
    }
}
