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
    public ObservableCollection<DeviceNicknameItemViewModel> DeviceNicknames { get; } = [];
    public ObservableCollection<AudioEndpointVisibilityItemViewModel> AudioEndpointsVisibility { get; } = [];

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
                bool isVisible = !hiddenList.Any(hId => MatchesEndpoint(hId, a.Id));
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

        if (!string.IsNullOrWhiteSpace(settings.ToggleHotkey))
        {
            bool registered = _hotkeyService.RegisterHotkey(settings.ToggleHotkey, () =>
            {
                var nextProfile = _coordinator.CurrentProfile == ProfileMode.Desk
                    ? ProfileMode.SimRig
                    : ProfileMode.Desk;
                _ = _coordinator.SwitchProfileAsync(nextProfile);
            });

            if (!registered)
            {
                failedHotkeys.Add(settings.ToggleHotkey);
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.DeskHotkey))
        {
            bool registered = _hotkeyService.RegisterHotkey(settings.DeskHotkey, () =>
            {
                _ = _coordinator.SwitchProfileAsync(ProfileMode.Desk);
            });

            if (!registered)
            {
                failedHotkeys.Add(settings.DeskHotkey);
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.RigHotkey))
        {
            bool registered = _hotkeyService.RegisterHotkey(settings.RigHotkey, () =>
            {
                _ = _coordinator.SwitchProfileAsync(ProfileMode.SimRig);
            });

            if (!registered)
            {
                failedHotkeys.Add(settings.RigHotkey);
            }
        }

        if (failedHotkeys.Count > 0)
        {
            StatusMessage = $"Warning: Failed to register hotkey(s): {string.Join(", ", failedHotkeys)}";
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
                    _settings.HiddenAudioEndpointIds.RemoveAll(id => MatchesEndpoint(id, item.Id));
                }
                else
                {
                    if (!_settings.HiddenAudioEndpointIds.Any(id => MatchesEndpoint(id, item.Id)))
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
