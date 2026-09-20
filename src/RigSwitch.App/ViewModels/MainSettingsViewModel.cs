namespace RigSwitch.App.ViewModels;

using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using RigSwitch.App.Services;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Events;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Primary view model orchestrating hardware profiles, device renaming, audio visibility, and system preferences.
/// </summary>
public sealed partial class MainSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsStorage;
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly TrayIconService _trayIconService;

    private UserSettings? _settings;
    private ProfileMode _currentProfile;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    private string _deskMonitorId = string.Empty;
    private string _rigMonitorId = string.Empty;
    private string _deskPrimaryAudioId = string.Empty;
    private string _deskFallbackAudioId = string.Empty;
    private string _rigPrimaryAudioId = string.Empty;

    private string _toggleHotkey = "Ctrl+Alt+S";
    private string _deskHotkey = "Ctrl+Alt+D";
    private string _rigHotkey = "Ctrl+Alt+R";
    private bool _startMinimizedToTray = true;
    private bool _showToastNotifications = true;
    private bool _disposed;

    public ObservableCollection<DisplayDeviceInfo> DetectedDisplays { get; } = [];
    public ObservableCollection<AudioEndpointInfo> DetectedAudioEndpoints { get; } = [];
    public ObservableCollection<DeviceSelectionOption> AvailableDisplayOptions { get; } = [];
    public ObservableCollection<DeviceSelectionOption> AvailableAudioOptions { get; } = [];
    public ObservableCollection<DeviceNicknameItemViewModel> DeviceNicknames { get; } = [];
    public ObservableCollection<AudioEndpointVisibilityItemViewModel> AudioEndpointsVisibility { get; } = [];
    public System.ComponentModel.ICollectionView? FilteredAudioEndpoints { get; private set; }

    private string _audioSearchText = string.Empty;
    public string AudioSearchText
    {
        get => _audioSearchText;
        set
        {
            if (SetProperty(ref _audioSearchText, value))
            {
                FilteredAudioEndpoints?.Refresh();
            }
        }
    }

    private string _audioFilterSelection = "All";
    public string AudioFilterSelection
    {
        get => _audioFilterSelection;
        set
        {
            if (SetProperty(ref _audioFilterSelection, value))
            {
                FilteredAudioEndpoints?.Refresh();
                OnPropertyChanged(nameof(IsAllFilterSelected));
                OnPropertyChanged(nameof(IsActiveFilterSelected));
                OnPropertyChanged(nameof(IsInactiveFilterSelected));
            }
        }
    }

    public bool IsAllFilterSelected => string.Equals(AudioFilterSelection, "All", StringComparison.OrdinalIgnoreCase);
    public bool IsActiveFilterSelected => string.Equals(AudioFilterSelection, "Active", StringComparison.OrdinalIgnoreCase);
    public bool IsInactiveFilterSelected => string.Equals(AudioFilterSelection, "Inactive", StringComparison.OrdinalIgnoreCase);

    public ICommand SelectAllAudioFilterCommand { get; }
    public ICommand SelectActiveAudioFilterCommand { get; }
    public ICommand SelectInactiveAudioFilterCommand { get; }

    public static bool MatchesAudioFilter(AudioEndpointVisibilityItemViewModel item, string searchText, string filterSelection)
    {
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            bool nameMatch = item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            bool adapterMatch = item.Adapter.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            if (!nameMatch && !adapterMatch)
            {
                return false;
            }
        }

        if (string.Equals(filterSelection, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return item.State == RigSwitch.Core.Enums.DevicePresenceState.Active;
        }

        if (string.Equals(filterSelection, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            return item.State != RigSwitch.Core.Enums.DevicePresenceState.Active;
        }

        return true;
    }

    public ObservableCollection<PresetConfigurationItemViewModel> DeskPresets { get; } = [];
    public ObservableCollection<PresetConfigurationItemViewModel> RigPresets { get; } = [];

    public ProfileMode CurrentProfile
    {
        get => _currentProfile;
        private set
        {
            if (SetProperty(ref _currentProfile, value))
            {
                OnPropertyChanged(nameof(CurrentProfileBadgeText));
                OnPropertyChanged(nameof(IsDeskActive));
                OnPropertyChanged(nameof(IsRigActive));
            }
        }
    }

    public string CurrentProfileBadgeText => CurrentProfile == ProfileMode.Desk ? "🖥️ Desk Setup" : "🏎️ Sim Rig Setup";
    public bool IsDeskActive => CurrentProfile == ProfileMode.Desk;
    public bool IsRigActive => CurrentProfile == ProfileMode.SimRig;

    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public string DeskMonitorId { get => _deskMonitorId; set => SetProperty(ref _deskMonitorId, value); }
    public string RigMonitorId { get => _rigMonitorId; set => SetProperty(ref _rigMonitorId, value); }
    public string DeskPrimaryAudioId { get => _deskPrimaryAudioId; set => SetProperty(ref _deskPrimaryAudioId, value); }
    public string DeskFallbackAudioId { get => _deskFallbackAudioId; set => SetProperty(ref _deskFallbackAudioId, value); }
    public string RigPrimaryAudioId { get => _rigPrimaryAudioId; set => SetProperty(ref _rigPrimaryAudioId, value); }

    public string ToggleHotkey { get => _toggleHotkey; set => SetProperty(ref _toggleHotkey, value); }
    public string DeskHotkey { get => _deskHotkey; set => SetProperty(ref _deskHotkey, value); }
    public string RigHotkey { get => _rigHotkey; set => SetProperty(ref _rigHotkey, value); }

    public bool StartMinimizedToTray { get => _startMinimizedToTray; set => SetProperty(ref _startMinimizedToTray, value); }
    public bool ShowToastNotifications { get => _showToastNotifications; set => SetProperty(ref _showToastNotifications, value); }

    public ICommand SwitchToDeskCommand { get; }
    public ICommand SwitchToRigCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand RefreshDevicesCommand { get; }

    public MainSettingsViewModel(
        IProfileSwitchCoordinator coordinator,
        ISettingsStorageService settingsStorage,
        IDisplayConfigurationService displayService,
        IAudioEndpointDirector audioDirector,
        IGlobalHotkeyService hotkeyService,
        TrayIconService trayIconService)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(settingsStorage);
        ArgumentNullException.ThrowIfNull(displayService);
        ArgumentNullException.ThrowIfNull(audioDirector);
        ArgumentNullException.ThrowIfNull(hotkeyService);
        ArgumentNullException.ThrowIfNull(trayIconService);

        _coordinator = coordinator;
        _settingsStorage = settingsStorage;
        _displayService = displayService;
        _audioDirector = audioDirector;
        _hotkeyService = hotkeyService;
        _trayIconService = trayIconService;

        _currentProfile = _coordinator.CurrentProfile;
        _coordinator.ProfileChanged += OnProfileChanged;

        SwitchToDeskCommand = new AsyncRelayCommand(() => SwitchProfileAsync(ProfileMode.Desk), () => !IsBusy);
        SwitchToRigCommand = new AsyncRelayCommand(() => SwitchProfileAsync(ProfileMode.SimRig), () => !IsBusy);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
        RefreshDevicesCommand = new AsyncRelayCommand(() => LoadAsync(), () => !IsBusy);

        SelectAllAudioFilterCommand = new RelayCommand(() => AudioFilterSelection = "All");
        SelectActiveAudioFilterCommand = new RelayCommand(() => AudioFilterSelection = "Active");
        SelectInactiveAudioFilterCommand = new RelayCommand(() => AudioFilterSelection = "Inactive");

        FilteredAudioEndpoints = System.Windows.Data.CollectionViewSource.GetDefaultView(AudioEndpointsVisibility);
        if (FilteredAudioEndpoints != null)
        {
            FilteredAudioEndpoints.Filter = item =>
            {
                if (item is AudioEndpointVisibilityItemViewModel endpoint)
                {
                    return MatchesAudioFilter(endpoint, AudioSearchText, AudioFilterSelection);
                }
                return true;
            };
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            _settings = await _settingsStorage.LoadSettingsAsync(cancellationToken);

            CurrentProfile = _coordinator.CurrentProfile;

            DeskMonitorId = _settings.DeskMonitorId;
            RigMonitorId = _settings.RigMonitorId;
            DeskPrimaryAudioId = _settings.DeskPrimaryAudioId;
            DeskFallbackAudioId = _settings.DeskFallbackAudioId;
            RigPrimaryAudioId = _settings.RigPrimaryAudioId;

            ToggleHotkey = _settings.ToggleHotkey;
            DeskHotkey = _settings.DeskHotkey;
            RigHotkey = _settings.RigHotkey;

            StartMinimizedToTray = _settings.StartMinimizedToTray;
            ShowToastNotifications = _settings.ShowToastNotifications;

            var displays = await _displayService.EnumerateDisplaysAsync(cancellationToken);
            DetectedDisplays.Clear();
            foreach (var d in displays)
            {
                DetectedDisplays.Add(d);
            }

            var audioEndpoints = await _audioDirector.EnumerateAudioEndpointsAsync(cancellationToken);
            DetectedAudioEndpoints.Clear();
            foreach (var a in audioEndpoints)
            {
                DetectedAudioEndpoints.Add(a);
            }

            AvailableDisplayOptions.Clear();
            AvailableDisplayOptions.Add(new DeviceSelectionOption(string.Empty, "— Select Display (Unassigned) —"));
            foreach (var d in displays)
            {
                var stateTag = d.IsPrimary ? " (Primary)" : (d.IsActive ? " (Active)" : "");
                var label = string.IsNullOrWhiteSpace(d.FriendlyName)
                    ? d.MonitorId
                    : $"{d.FriendlyName} [{d.MonitorId}]{stateTag}";
                AvailableDisplayOptions.Add(new DeviceSelectionOption(d.MonitorId, label));
            }

            AvailableAudioOptions.Clear();
            AvailableAudioOptions.Add(new DeviceSelectionOption(string.Empty, "— Select Audio Device (Unassigned) —"));
            foreach (var a in audioEndpoints)
            {
                string customNick = _settings.CustomDeviceNames.GetValueOrDefault(a.Id, string.Empty);
                var name = !string.IsNullOrWhiteSpace(customNick) ? $"{customNick} ({a.Name})" : a.Name;
                var stateTag = a.State == RigSwitch.Core.Enums.DevicePresenceState.Active ? "" : $" [{a.State}]";
                AvailableAudioOptions.Add(new DeviceSelectionOption(a.Id, $"{name}{stateTag}"));
            }

            DeviceOptionResolver.EnsureDisplayOption(DeskMonitorId, AvailableDisplayOptions);
            DeviceOptionResolver.EnsureDisplayOption(RigMonitorId, AvailableDisplayOptions);

            DeskPrimaryAudioId = DeviceOptionResolver.ResolveAudioOption(DeskPrimaryAudioId, audioEndpoints, AvailableAudioOptions);
            DeskFallbackAudioId = DeviceOptionResolver.ResolveAudioOption(DeskFallbackAudioId, audioEndpoints, AvailableAudioOptions);
            RigPrimaryAudioId = DeviceOptionResolver.ResolveAudioOption(RigPrimaryAudioId, audioEndpoints, AvailableAudioOptions);

            DeskPresets.Clear();
            for (int i = 0; i < _settings.DeskPresets.Count; i++)
            {
                var preset = _settings.DeskPresets[i];
                DeviceOptionResolver.EnsureDisplayOption(preset.TargetMonitorId, AvailableDisplayOptions);
                preset.PrimaryAudioId = DeviceOptionResolver.ResolveAudioOption(preset.PrimaryAudioId, audioEndpoints, AvailableAudioOptions);
                preset.FallbackAudioId = DeviceOptionResolver.ResolveAudioOption(preset.FallbackAudioId, audioEndpoints, AvailableAudioOptions);

                bool isActive = i == _settings.ActiveDeskPresetIndex;
                DeskPresets.Add(new PresetConfigurationItemViewModel(
                    preset,
                    i,
                    "DeskActivePreset",
                    isActive,
                    OnDeskPresetActivated));
            }

            RigPresets.Clear();
            for (int i = 0; i < _settings.RigPresets.Count; i++)
            {
                var preset = _settings.RigPresets[i];
                DeviceOptionResolver.EnsureDisplayOption(preset.TargetMonitorId, AvailableDisplayOptions);
                preset.PrimaryAudioId = DeviceOptionResolver.ResolveAudioOption(preset.PrimaryAudioId, audioEndpoints, AvailableAudioOptions);
                preset.FallbackAudioId = DeviceOptionResolver.ResolveAudioOption(preset.FallbackAudioId, audioEndpoints, AvailableAudioOptions);

                bool isActive = i == _settings.ActiveRigPresetIndex;
                RigPresets.Add(new PresetConfigurationItemViewModel(
                    preset,
                    i,
                    "RigActivePreset",
                    isActive,
                    OnRigPresetActivated));
            }

            DeviceNicknames.Clear();
            foreach (var d in displays)
            {
                string nick = _settings.CustomDeviceNames.GetValueOrDefault(d.MonitorId, string.Empty);
                DeviceNicknames.Add(new DeviceNicknameItemViewModel(d.MonitorId, "Display", d.FriendlyName, nick));
            }
            foreach (var a in audioEndpoints)
            {
                string nick = _settings.CustomDeviceNames.GetValueOrDefault(a.Id, string.Empty);
                DeviceNicknames.Add(new DeviceNicknameItemViewModel(a.Id, "Audio Playback", a.Name, nick));
            }

            AudioEndpointsVisibility.Clear();
            var hiddenList = _settings.HiddenAudioEndpointIds;
            foreach (var a in audioEndpoints)
            {
                bool isVisible = !hiddenList.Any(hId => DeviceOptionResolver.MatchesEndpoint(hId, a.Id));
                AudioEndpointsVisibility.Add(new AudioEndpointVisibilityItemViewModel(
                    a.Id,
                    a.Name,
                    a.AdapterDescription,
                    isVisible,
                    OnAudioVisibilityChangedAsync,
                    a.State));
            }

            StatusMessage = "Devices and configuration loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading devices: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SwitchProfileAsync(ProfileMode targetProfile)
    {
        IsBusy = true;
        StatusMessage = $"Switching to {targetProfile}...";
        try
        {
            bool success = await _coordinator.SwitchProfileAsync(targetProfile);
            StatusMessage = success
                ? $"Successfully switched to {targetProfile} profile."
                : $"Failed to switch to {targetProfile} profile.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Switch error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveSettingsAsync()
    {
        IsBusy = true;
        try
        {
            var settings = _settings ?? new UserSettings();

            // Update Desk Presets
            for (int i = 0; i < DeskPresets.Count; i++)
            {
                if (i < settings.DeskPresets.Count)
                {
                    DeskPresets[i].ApplyTo(settings.DeskPresets[i]);
                }
            }
            var activeDeskIdx = DeskPresets.TakeWhile(p => !p.IsActive).Count();
            settings.ActiveDeskPresetIndex = activeDeskIdx < DeskPresets.Count ? activeDeskIdx : 0;

            // Update Rig Presets
            for (int i = 0; i < RigPresets.Count; i++)
            {
                if (i < settings.RigPresets.Count)
                {
                    RigPresets[i].ApplyTo(settings.RigPresets[i]);
                }
            }
            var activeRigIdx = RigPresets.TakeWhile(p => !p.IsActive).Count();
            settings.ActiveRigPresetIndex = activeRigIdx < RigPresets.Count ? activeRigIdx : 0;

            settings.DeskMonitorId = settings.GetActivePreset(ProfileMode.Desk).TargetMonitorId;
            settings.DeskPrimaryAudioId = settings.GetActivePreset(ProfileMode.Desk).PrimaryAudioId;
            settings.DeskFallbackAudioId = settings.GetActivePreset(ProfileMode.Desk).FallbackAudioId;
            settings.RigMonitorId = settings.GetActivePreset(ProfileMode.SimRig).TargetMonitorId;
            settings.RigPrimaryAudioId = settings.GetActivePreset(ProfileMode.SimRig).PrimaryAudioId;

            DeskMonitorId = settings.DeskMonitorId;
            DeskPrimaryAudioId = settings.DeskPrimaryAudioId;
            DeskFallbackAudioId = settings.DeskFallbackAudioId;
            RigMonitorId = settings.RigMonitorId;
            RigPrimaryAudioId = settings.RigPrimaryAudioId;

            settings.ToggleHotkey = ToggleHotkey;
            settings.DeskHotkey = DeskHotkey;
            settings.RigHotkey = RigHotkey;

            settings.StartMinimizedToTray = StartMinimizedToTray;
            settings.ShowToastNotifications = ShowToastNotifications;

            foreach (var nickItem in DeviceNicknames)
            {
                if (!string.IsNullOrWhiteSpace(nickItem.CustomNickname))
                {
                    settings.CustomDeviceNames[nickItem.DeviceId] = nickItem.CustomNickname.Trim();
                }
                else
                {
                    settings.CustomDeviceNames.Remove(nickItem.DeviceId);
                }
            }

            await _settingsStorage.SaveSettingsAsync(settings);
            _settings = settings;

            RegisterGlobalHotkeys(settings);
            _trayIconService.ShowToastNotifications = settings.ShowToastNotifications;
            _trayIconService.RefreshPresets(settings);

            if (string.IsNullOrEmpty(StatusMessage) || !StatusMessage.StartsWith("Warning:", StringComparison.Ordinal))
            {
                StatusMessage = "Settings saved successfully!";
            }

            _trayIconService.ShowNotification("RigSwitch", "Settings saved successfully.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RegisterGlobalHotkeys(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _hotkeyService.UnregisterAll();
        var failedHotkeys = new List<string>();

        void TryRegister(string? hotkey, Action action)
        {
            if (!string.IsNullOrWhiteSpace(hotkey) && !_hotkeyService.RegisterHotkey(hotkey, action))
            {
                failedHotkeys.Add(hotkey);
            }
        }

        TryRegister(settings.ToggleHotkey, () =>
        {
            var next = _coordinator.CurrentProfile == ProfileMode.Desk ? ProfileMode.SimRig : ProfileMode.Desk;
            _ = _coordinator.SwitchProfileAsync(next);
        });
        TryRegister(settings.DeskHotkey, () => _ = _coordinator.SwitchProfileAsync(ProfileMode.Desk));
        TryRegister(settings.RigHotkey, () => _ = _coordinator.SwitchProfileAsync(ProfileMode.SimRig));

        for (int i = 0; i < settings.DeskPresets.Count; i++)
        {
            int index = i;
            TryRegister(settings.DeskPresets[i].DirectHotkey, () => _ = _coordinator.SwitchToPresetAsync(ProfileMode.Desk, index));
        }

        for (int i = 0; i < settings.RigPresets.Count; i++)
        {
            int index = i;
            TryRegister(settings.RigPresets[i].DirectHotkey, () => _ = _coordinator.SwitchToPresetAsync(ProfileMode.SimRig, index));
        }

        if (failedHotkeys.Count > 0)
        {
            StatusMessage = $"Warning: Failed to register hotkey(s): {string.Join(", ", failedHotkeys)}";
        }
    }

    private void OnDeskPresetActivated(PresetConfigurationItemViewModel activated)
    {
        foreach (var preset in DeskPresets)
        {
            if (preset != activated)
            {
                preset.IsActive = false;
            }
        }
    }

    private void OnRigPresetActivated(PresetConfigurationItemViewModel activated)
    {
        foreach (var preset in RigPresets)
        {
            if (preset != activated)
            {
                preset.IsActive = false;
            }
        }
    }

    private async Task OnAudioVisibilityChangedAsync(AudioEndpointVisibilityItemViewModel item, bool isVisible)
    {
        try
        {
            await _audioDirector.SetEndpointVisibilityAsync(item.Id, isVisible);

            if (_settings != null)
            {
                if (isVisible)
                {
                    _settings.HiddenAudioEndpointIds.RemoveAll(id => DeviceOptionResolver.MatchesEndpoint(id, item.Id));
                }
                else
                {
                    if (!_settings.HiddenAudioEndpointIds.Any(id => DeviceOptionResolver.MatchesEndpoint(id, item.Id)))
                    {
                        _settings.HiddenAudioEndpointIds.Add(item.Id);
                    }
                }

                await _settingsStorage.SaveSettingsAsync(_settings);
            }

            StatusMessage = $"Audio endpoint '{item.Name}' {(isVisible ? "shown" : "hidden")}.";
        }
        catch (Exception ex)
        {
            item.RevertVisibility(!isVisible);
            StatusMessage = $"Failed to update visibility for {item.Name}: {ex.Message}";
        }
    }

    private void OnProfileChanged(object? sender, ProfileChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            CurrentProfile = _coordinator.CurrentProfile;
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _coordinator.ProfileChanged -= OnProfileChanged;
    }
}
