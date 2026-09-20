namespace RigSwitch.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows.Input;
using RigSwitch.App.Services;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Events;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Primary view model orchestrating hardware profiles, device renaming, audio visibility, and system preferences.
/// </summary>
public sealed class MainSettingsViewModel : ViewModelBase, IDisposable
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

    /// <summary>
    /// Gets the detected connected displays.
    /// </summary>
    public ObservableCollection<DisplayDeviceInfo> DetectedDisplays { get; } = [];

    /// <summary>
    /// Gets the detected audio playback endpoints.
    /// </summary>
    public ObservableCollection<AudioEndpointInfo> DetectedAudioEndpoints { get; } = [];

    /// <summary>
    /// Gets the list of device renamer items.
    /// </summary>
    public ObservableCollection<DeviceNicknameItemViewModel> DeviceNicknames { get; } = [];

    /// <summary>
    /// Gets the collection of audio endpoints with visibility toggles.
    /// </summary>
    public ObservableCollection<AudioEndpointVisibilityItemViewModel> AudioEndpointsVisibility { get; } = [];

    /// <summary>
    /// Gets the currently active workstation profile.
    /// </summary>
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

    /// <summary>
    /// Gets the user-facing badge text for the active profile.
    /// </summary>
    public string CurrentProfileBadgeText => CurrentProfile == ProfileMode.Desk ? "🖥️ Desk Setup" : "🏎️ Sim Rig Setup";

    /// <summary>
    /// Gets a value indicating whether the Desk profile is active.
    /// </summary>
    public bool IsDeskActive => CurrentProfile == ProfileMode.Desk;

    /// <summary>
    /// Gets a value indicating whether the Sim Rig profile is active.
    /// </summary>
    public bool IsRigActive => CurrentProfile == ProfileMode.SimRig;

    /// <summary>
    /// Gets or sets a value indicating whether an asynchronous operation is in progress.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// Gets or sets user-facing status feedback.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Gets or sets the target monitor ID for Desk setup.
    /// </summary>
    public string DeskMonitorId
    {
        get => _deskMonitorId;
        set => SetProperty(ref _deskMonitorId, value);
    }

    /// <summary>
    /// Gets or sets the target monitor ID for Sim Rig setup.
    /// </summary>
    public string RigMonitorId
    {
        get => _rigMonitorId;
        set => SetProperty(ref _rigMonitorId, value);
    }

    /// <summary>
    /// Gets or sets the primary audio endpoint GUID for Desk setup.
    /// </summary>
    public string DeskPrimaryAudioId
    {
        get => _deskPrimaryAudioId;
        set => SetProperty(ref _deskPrimaryAudioId, value);
    }

    /// <summary>
    /// Gets or sets the fallback audio endpoint GUID for Desk setup.
    /// </summary>
    public string DeskFallbackAudioId
    {
        get => _deskFallbackAudioId;
        set => SetProperty(ref _deskFallbackAudioId, value);
    }

    /// <summary>
    /// Gets or sets the primary audio endpoint GUID for Sim Rig setup.
    /// </summary>
    public string RigPrimaryAudioId
    {
        get => _rigPrimaryAudioId;
        set => SetProperty(ref _rigPrimaryAudioId, value);
    }

    /// <summary>
    /// Gets or sets the toggle profiles hotkey string.
    /// </summary>
    public string ToggleHotkey
    {
        get => _toggleHotkey;
        set => SetProperty(ref _toggleHotkey, value);
    }

    /// <summary>
    /// Gets or sets the direct Desk profile hotkey string.
    /// </summary>
    public string DeskHotkey
    {
        get => _deskHotkey;
        set => SetProperty(ref _deskHotkey, value);
    }

    /// <summary>
    /// Gets or sets the direct Sim Rig profile hotkey string.
    /// </summary>
    public string RigHotkey
    {
        get => _rigHotkey;
        set => SetProperty(ref _rigHotkey, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the app starts minimized to the system tray.
    /// </summary>
    public bool StartMinimizedToTray
    {
        get => _startMinimizedToTray;
        set => SetProperty(ref _startMinimizedToTray, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether balloon notifications are shown.
    /// </summary>
    public bool ShowToastNotifications
    {
        get => _showToastNotifications;
        set => SetProperty(ref _showToastNotifications, value);
    }

    /// <summary>
    /// Command to switch workstation to Desk profile.
    /// </summary>
    public ICommand SwitchToDeskCommand { get; }

    /// <summary>
    /// Command to switch workstation to Sim Rig profile.
    /// </summary>
    public ICommand SwitchToRigCommand { get; }

    /// <summary>
    /// Command to save user configuration.
    /// </summary>
    public ICommand SaveSettingsCommand { get; }

    /// <summary>
    /// Command to refresh connected devices.
    /// </summary>
    public ICommand RefreshDevicesCommand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainSettingsViewModel"/> class.
    /// </summary>
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

        SwitchToDeskCommand = new AsyncRelayCommand(
            () => SwitchProfileAsync(ProfileMode.Desk),
            () => !IsBusy);

        SwitchToRigCommand = new AsyncRelayCommand(
            () => SwitchProfileAsync(ProfileMode.SimRig),
            () => !IsBusy);

        SaveSettingsCommand = new AsyncRelayCommand(
            SaveSettingsAsync,
            () => !IsBusy);

        RefreshDevicesCommand = new AsyncRelayCommand(
            () => LoadAsync(),
            () => !IsBusy);
    }

    /// <summary>
    /// Loads settings and detects displays and audio endpoints asynchronously.
    /// </summary>
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
            var hiddenSet = new HashSet<string>(_settings.HiddenAudioEndpointIds, StringComparer.OrdinalIgnoreCase);
            foreach (var a in audioEndpoints)
            {
                bool isVisible = !hiddenSet.Contains(a.Id);
                AudioEndpointsVisibility.Add(new AudioEndpointVisibilityItemViewModel(
                    a.Id,
                    a.Name,
                    a.AdapterDescription,
                    isVisible,
                    OnAudioVisibilityChangedAsync));
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

    /// <summary>
    /// Switches workstation profile asynchronously.
    /// </summary>
    public async Task SwitchProfileAsync(ProfileMode targetProfile)
    {
        IsBusy = true;
        StatusMessage = $"Switching to {targetProfile}...";
        try
        {
            bool success = await _coordinator.SwitchProfileAsync(targetProfile);
            if (success)
            {
                StatusMessage = $"Successfully switched to {targetProfile} profile.";
            }
            else
            {
                StatusMessage = $"Failed to switch to {targetProfile} profile.";
            }
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

    /// <summary>
    /// Persists current view model settings to disk and updates hotkeys.
    /// </summary>
    public async Task SaveSettingsAsync()
    {
        IsBusy = true;
        try
        {
            var settings = _settings ?? new UserSettings();
            settings.DeskMonitorId = DeskMonitorId;
            settings.RigMonitorId = RigMonitorId;
            settings.DeskPrimaryAudioId = DeskPrimaryAudioId;
            settings.DeskFallbackAudioId = DeskFallbackAudioId;
            settings.RigPrimaryAudioId = RigPrimaryAudioId;

            settings.ToggleHotkey = ToggleHotkey;
            settings.DeskHotkey = DeskHotkey;
            settings.RigHotkey = RigHotkey;

            settings.StartMinimizedToTray = StartMinimizedToTray;
            settings.ShowToastNotifications = ShowToastNotifications;

            settings.CustomDeviceNames.Clear();
            foreach (var nickItem in DeviceNicknames)
            {
                if (!string.IsNullOrWhiteSpace(nickItem.CustomNickname))
                {
                    settings.CustomDeviceNames[nickItem.DeviceId] = nickItem.CustomNickname.Trim();
                }
            }

            await _settingsStorage.SaveSettingsAsync(settings);
            _settings = settings;

            RegisterGlobalHotkeys(settings);
            _trayIconService.ShowToastNotifications = settings.ShowToastNotifications;

            StatusMessage = "Settings saved successfully!";
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

    /// <summary>
    /// Registers configured hotkey bindings into the global hotkey service.
    /// </summary>
    public void RegisterGlobalHotkeys(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _hotkeyService.UnregisterAll();

        if (!string.IsNullOrWhiteSpace(settings.ToggleHotkey))
        {
            _hotkeyService.RegisterHotkey(settings.ToggleHotkey, () =>
            {
                var nextProfile = _coordinator.CurrentProfile == ProfileMode.Desk
                    ? ProfileMode.SimRig
                    : ProfileMode.Desk;
                _ = _coordinator.SwitchProfileAsync(nextProfile);
            });
        }

        if (!string.IsNullOrWhiteSpace(settings.DeskHotkey))
        {
            _hotkeyService.RegisterHotkey(settings.DeskHotkey, () =>
            {
                _ = _coordinator.SwitchProfileAsync(ProfileMode.Desk);
            });
        }

        if (!string.IsNullOrWhiteSpace(settings.RigHotkey))
        {
            _hotkeyService.RegisterHotkey(settings.RigHotkey, () =>
            {
                _ = _coordinator.SwitchProfileAsync(ProfileMode.SimRig);
            });
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
                    _settings.HiddenAudioEndpointIds.RemoveAll(id => string.Equals(id, item.Id, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    if (!_settings.HiddenAudioEndpointIds.Any(id => string.Equals(id, item.Id, StringComparison.OrdinalIgnoreCase)))
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

    /// <inheritdoc/>
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
