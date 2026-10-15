namespace RigSwitch.App.ViewModels;

using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using RigSwitch.App.Services;
using RigSwitch.App.Views;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Events;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Services;

/// <summary>
/// Primary view model orchestrating hardware profiles, per-preset 2D topology &amp; ranked audio routing, device nicknames, audio visibility, update checks, and preferences.
/// </summary>
public sealed partial class MainSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsStorage;
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly TrayIconService _trayIconService;
    private readonly IUpdateCheckService _updateCheckService;

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
    private string _audioToggleHotkey = "Ctrl+Alt+A";
    private bool _enforcePrimaryAudio = true;
    private bool _startMinimizedToTray = true;
    private bool _showToastNotifications = true;
    private bool _enableAutoUpdateCheck = true;
    private string _ignoredReleaseVersion = string.Empty;
    private bool _disposed;
    private List<AudioEndpointInfo> _cachedRawAudioEndpoints = [];
    private HashSet<string> _referencedDeviceIds = new(StringComparer.OrdinalIgnoreCase);

    private PresetConfigurationItemViewModel? _selectedPreset;
    private int _selectedPageIndex;
    private string _selectedNewNicknameDeviceId = string.Empty;
    private string _newNicknameText = string.Empty;

    public string AppVersionText { get; } = ResolveAppVersionText();

    public ObservableCollection<DisplayDeviceInfo> DetectedDisplays { get; } = [];
    public ObservableCollection<AudioEndpointInfo> DetectedAudioEndpoints { get; } = [];
    public ObservableCollection<DeviceSelectionOption> AvailableDisplayOptions { get; } = [];
    public ObservableCollection<DeviceSelectionOption> AvailableAudioOptions { get; } = [];
    public ObservableCollection<DeviceSelectionOption> AvailableMicrophoneOptions { get; } = [];
    public ObservableCollection<DeviceNicknameItemViewModel> DeviceNicknames { get; } = [];
    public ObservableCollection<DeviceNicknameItemViewModel> ConfiguredNicknames { get; } = [];
    public ObservableCollection<DeviceSelectionOption> UnnicknamedDeviceOptions { get; } = [];
    public ObservableCollection<AudioEndpointVisibilityItemViewModel> AudioEndpointsVisibility { get; } = [];
    public System.ComponentModel.ICollectionView? FilteredAudioEndpoints { get; private set; }

    public int SelectedPageIndex
    {
        get => _selectedPageIndex;
        set
        {
            if (SetProperty(ref _selectedPageIndex, value))
            {
                OnPropertyChanged(nameof(IsPresetViewSelected));
                OnPropertyChanged(nameof(IsAudioPageSelected));
                OnPropertyChanged(nameof(IsNicknamesPageSelected));
                OnPropertyChanged(nameof(IsHotkeysPageSelected));
            }
        }
    }

    public bool IsPresetViewSelected => SelectedPageIndex == 0;
    public bool IsAudioPageSelected => SelectedPageIndex == 2;
    public bool IsNicknamesPageSelected => SelectedPageIndex == 3;
    public bool IsHotkeysPageSelected => SelectedPageIndex == 4;

    public PresetConfigurationItemViewModel? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                foreach (var p in DeskPresets)
                {
                    p.IsSelectedInNav = IsPresetViewSelected && ReferenceEquals(p, value);
                }

                foreach (var p in RigPresets)
                {
                    p.IsSelectedInNav = IsPresetViewSelected && ReferenceEquals(p, value);
                }
            }
        }
    }

    public string SelectedNewNicknameDeviceId
    {
        get => _selectedNewNicknameDeviceId;
        set => SetProperty(ref _selectedNewNicknameDeviceId, value ?? string.Empty);
    }

    public string NewNicknameText
    {
        get => _newNicknameText;
        set => SetProperty(ref _newNicknameText, value ?? string.Empty);
    }

    private string _audioSearchText = string.Empty;
    public string AudioSearchText
    {
        get => _audioSearchText;
        set
        {
            if (SetProperty(ref _audioSearchText, value))
            {
                FilteredAudioEndpoints?.Refresh();
                OnPropertyChanged(nameof(FilterStatusSummary));
            }
        }
    }

    private string _audioFilterSelection = "Relevant";
    public string AudioFilterSelection
    {
        get => _audioFilterSelection;
        set
        {
            if (SetProperty(ref _audioFilterSelection, value))
            {
                FilteredAudioEndpoints?.Refresh();
                OnPropertyChanged(nameof(IsRelevantFilterSelected));
                OnPropertyChanged(nameof(IsAllFilterSelected));
                OnPropertyChanged(nameof(IsActiveFilterSelected));
                OnPropertyChanged(nameof(IsInactiveFilterSelected));
                OnPropertyChanged(nameof(FilterStatusSummary));
            }
        }
    }

    public bool IsRelevantFilterSelected
    {
        get => string.Equals(AudioFilterSelection, "Relevant", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                AudioFilterSelection = "Relevant";
            }
        }
    }

    public bool IsAllFilterSelected
    {
        get => string.Equals(AudioFilterSelection, "All", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(AudioFilterSelection, "Relevant", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                AudioFilterSelection = "All";
            }
        }
    }

    public bool IsActiveFilterSelected
    {
        get => string.Equals(AudioFilterSelection, "Active", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                AudioFilterSelection = "Active";
            }
        }
    }

    public bool IsInactiveFilterSelected
    {
        get => string.Equals(AudioFilterSelection, "Inactive", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(AudioFilterSelection, "Disabled / Unplugged", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(AudioFilterSelection, "Unplugged", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(AudioFilterSelection, "Disabled", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                AudioFilterSelection = "Inactive";
            }
        }
    }

    public string FilterStatusSummary
    {
        get
        {
            int total = AudioEndpointsVisibility.Count;
            int active = AudioEndpointsVisibility.Count(e => e.State == RigSwitch.Core.Enums.DevicePresenceState.Active);
            int inactive = AudioEndpointsVisibility.Count(e => e.State != RigSwitch.Core.Enums.DevicePresenceState.Active);
            int relevant = AudioEndpointsVisibility.Count(e => MatchesAudioFilter(e, string.Empty, "Relevant", _referencedDeviceIds));

            string modeLabel = IsActiveFilterSelected
                ? "Active Only"
                : IsInactiveFilterSelected
                    ? "Disabled / Unplugged"
                    : IsRelevantFilterSelected
                        ? "All Relevant Devices"
                        : "All Registry Ghosts";

            int matchingCount = IsActiveFilterSelected
                ? active
                : IsInactiveFilterSelected
                    ? inactive
                    : IsRelevantFilterSelected
                        ? relevant
                        : total;

            if (!string.IsNullOrWhiteSpace(AudioSearchText))
            {
                int searchMatches = AudioEndpointsVisibility.Count(e => MatchesAudioFilter(e, AudioSearchText, AudioFilterSelection, _referencedDeviceIds));
                return $"Showing {modeLabel} • {searchMatches} of {total} endpoints matching \"{AudioSearchText}\"";
            }

            return $"Showing {modeLabel} • {matchingCount} of {total} endpoints";
        }
    }

    public ICommand SelectRelevantAudioFilterCommand { get; }
    public ICommand SelectAllAudioFilterCommand { get; }
    public ICommand SelectActiveAudioFilterCommand { get; }
    public ICommand SelectInactiveAudioFilterCommand { get; }
    public ICommand NavigateToPresetCommand { get; }
    public ICommand NavigateToSystemPageCommand { get; }
    public ICommand AddNicknameCommand { get; }
    public ICommand RemoveNicknameCommand { get; }

    public static bool MatchesAudioFilter(
        AudioEndpointVisibilityItemViewModel item,
        string searchText,
        string filterSelection,
        IEnumerable<string>? referencedIds = null) =>
        DeviceOptionResolver.MatchesAudioFilter(item, searchText, filterSelection, referencedIds);

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
    public string AudioToggleHotkey { get => _audioToggleHotkey; set => SetProperty(ref _audioToggleHotkey, value); }
    public bool EnforcePrimaryAudio { get => _enforcePrimaryAudio; set => SetProperty(ref _enforcePrimaryAudio, value); }

    public bool StartMinimizedToTray { get => _startMinimizedToTray; set => SetProperty(ref _startMinimizedToTray, value); }
    public bool ShowToastNotifications { get => _showToastNotifications; set => SetProperty(ref _showToastNotifications, value); }
    public bool EnableAutoUpdateCheck { get => _enableAutoUpdateCheck; set => SetProperty(ref _enableAutoUpdateCheck, value); }
    public string IgnoredReleaseVersion { get => _ignoredReleaseVersion; set => SetProperty(ref _ignoredReleaseVersion, value); }

    public ICommand SwitchToDeskCommand { get; }
    public ICommand SwitchToRigCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand RefreshDevicesCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }

    public MainSettingsViewModel(
        IProfileSwitchCoordinator coordinator,
        ISettingsStorageService settingsStorage,
        IDisplayConfigurationService displayService,
        IAudioEndpointDirector audioDirector,
        IGlobalHotkeyService hotkeyService,
        TrayIconService trayIconService,
        IUpdateCheckService? updateCheckService = null)
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
        _updateCheckService = updateCheckService ?? new UpdateCheckService();

        _currentProfile = _coordinator.CurrentProfile;
        _coordinator.ProfileChanged += OnProfileChanged;

        SwitchToDeskCommand = new AsyncRelayCommand(() => SwitchProfileAsync(ProfileMode.Desk), () => !IsBusy);
        SwitchToRigCommand = new AsyncRelayCommand(() => SwitchProfileAsync(ProfileMode.SimRig), () => !IsBusy);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
        RefreshDevicesCommand = new AsyncRelayCommand(() => LoadAsync(), () => !IsBusy);
        IdentifyMonitorsCommand = new RelayCommand(IdentifyMonitors);
        CheckForUpdatesCommand = new AsyncRelayCommand(() => CheckForUpdatesManualAsync(), () => !IsBusy);

        SelectRelevantAudioFilterCommand = new RelayCommand(() => AudioFilterSelection = "Relevant");
        SelectAllAudioFilterCommand = new RelayCommand(() => AudioFilterSelection = "All");
        SelectActiveAudioFilterCommand = new RelayCommand(() => AudioFilterSelection = "Active");
        SelectInactiveAudioFilterCommand = new RelayCommand(() => AudioFilterSelection = "Inactive");

        NavigateToPresetCommand = new RelayCommand(param =>
        {
            if (param is PresetConfigurationItemViewModel presetVm)
            {
                SelectPresetForNavigation(presetVm);
            }
        });

        NavigateToSystemPageCommand = new RelayCommand(param =>
        {
            if (param is string pageTag && int.TryParse(pageTag, out int pageIdx))
            {
                NavigateToSystemPage(pageIdx);
            }
            else if (param is int idx)
            {
                NavigateToSystemPage(idx);
            }
        });

        AddNicknameCommand = new RelayCommand(AddNewNickname);
        RemoveNicknameCommand = new RelayCommand(param =>
        {
            if (param is DeviceNicknameItemViewModel item)
            {
                RemoveNickname(item);
            }
        });

        FilteredAudioEndpoints = CollectionViewSource.GetDefaultView(AudioEndpointsVisibility);
        if (FilteredAudioEndpoints != null)
        {
            FilteredAudioEndpoints.Filter = item =>
                item is AudioEndpointVisibilityItemViewModel endpoint &&
                MatchesAudioFilter(endpoint, AudioSearchText, AudioFilterSelection, _referencedDeviceIds);
        }
    }

    public void SelectPresetForNavigation(PresetConfigurationItemViewModel presetVm)
    {
        SelectedPageIndex = 0;
        SelectedPreset = presetVm;
        foreach (var p in DeskPresets)
        {
            p.IsSelectedInNav = ReferenceEquals(p, presetVm);
        }

        foreach (var p in RigPresets)
        {
            p.IsSelectedInNav = ReferenceEquals(p, presetVm);
        }
    }

    public void NavigateToSystemPage(int pageIndex)
    {
        SelectedPageIndex = pageIndex;
        foreach (var p in DeskPresets)
        {
            p.IsSelectedInNav = false;
        }

        foreach (var p in RigPresets)
        {
            p.IsSelectedInNav = false;
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
            AudioToggleHotkey = _settings.AudioToggleHotkey;
            EnforcePrimaryAudio = _settings.EnforcePrimaryAudio;

            StartMinimizedToTray = _settings.StartMinimizedToTray;
            ShowToastNotifications = _settings.ShowToastNotifications;
            EnableAutoUpdateCheck = _settings.EnableAutoUpdateCheck;
            IgnoredReleaseVersion = _settings.IgnoredReleaseVersion;

            var displays = await _displayService.EnumerateDisplaysAsync(cancellationToken);
            DetectedDisplays.Clear();
            foreach (var d in displays)
            {
                DetectedDisplays.Add(d);
                if (!string.IsNullOrWhiteSpace(d.FriendlyName))
                {
                    _settings.CachedDeviceNames[d.MonitorId] = d.FriendlyName;
                }
            }

            var audioEndpoints = await _audioDirector.EnumerateAudioEndpointsAsync(cancellationToken);
            _cachedRawAudioEndpoints = [.. audioEndpoints];
            DetectedAudioEndpoints.Clear();
            foreach (var a in audioEndpoints)
            {
                if (a.State == DevicePresenceState.Active)
                {
                    DetectedAudioEndpoints.Add(a);
                }
                if (!string.IsNullOrWhiteSpace(a.Name))
                {
                    _settings.CachedDeviceNames[a.Id] = a.Name;
                }
            }

            _referencedDeviceIds = DeviceOptionResolver.CollectReferencedDeviceIds(_settings);

            AvailableDisplayOptions.Clear();
            AvailableDisplayOptions.Add(new DeviceSelectionOption(string.Empty, "— Select Display (Unassigned) —"));
            foreach (var d in displays)
            {
                var stateTag = d.IsPrimary ? " (Primary)" : (d.IsActive ? " (Active)" : "");
                var label = string.IsNullOrWhiteSpace(d.FriendlyName) ? d.MonitorId : $"{d.FriendlyName} [{d.MonitorId}]{stateTag}";
                AvailableDisplayOptions.Add(new DeviceSelectionOption(d.MonitorId, label));
            }

            PopulateAudioSelectionOptions(_cachedRawAudioEndpoints);

            DeviceOptionResolver.EnsureDisplayOption(DeskMonitorId, AvailableDisplayOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
            DeviceOptionResolver.EnsureDisplayOption(RigMonitorId, AvailableDisplayOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);

            DeskPrimaryAudioId = DeviceOptionResolver.ResolveAudioOption(DeskPrimaryAudioId, audioEndpoints, AvailableAudioOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
            DeskFallbackAudioId = DeviceOptionResolver.ResolveAudioOption(DeskFallbackAudioId, audioEndpoints, AvailableAudioOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
            RigPrimaryAudioId = DeviceOptionResolver.ResolveAudioOption(RigPrimaryAudioId, audioEndpoints, AvailableAudioOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);

            DeskPresets.Clear();
            for (int i = 0; i < _settings.DeskPresets.Count; i++)
            {
                var preset = _settings.DeskPresets[i];
                DeviceOptionResolver.EnsureDisplayOption(preset.TargetMonitorId, AvailableDisplayOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.PrimaryAudioId = DeviceOptionResolver.ResolveAudioOption(preset.PrimaryAudioId, audioEndpoints, AvailableAudioOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.SecondaryAudioId = DeviceOptionResolver.ResolveAudioOption(preset.SecondaryAudioId, audioEndpoints, AvailableAudioOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.FallbackAudioId = DeviceOptionResolver.ResolveAudioOption(preset.FallbackAudioId, audioEndpoints, AvailableAudioOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.PrimaryMicrophoneId = DeviceOptionResolver.ResolveAudioOption(preset.PrimaryMicrophoneId, audioEndpoints, AvailableMicrophoneOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.SecondaryMicrophoneId = DeviceOptionResolver.ResolveAudioOption(preset.SecondaryMicrophoneId, audioEndpoints, AvailableMicrophoneOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.FallbackMicrophoneId = DeviceOptionResolver.ResolveAudioOption(preset.FallbackMicrophoneId, audioEndpoints, AvailableMicrophoneOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);

                var presetVm = new PresetConfigurationItemViewModel(
                    preset,
                    i,
                    "DeskActivePreset",
                    i == _settings.ActiveDeskPresetIndex,
                    OnDeskPresetActivated,
                    profile: ProfileMode.Desk,
                    onActivateNow: p => _ = ActivatePresetNowAsync(ProfileMode.Desk, p.PresetIndex),
                    onIdentifyMonitors: IdentifyMonitors);
                presetVm.HydrateHardwareContext(displays, _cachedRawAudioEndpoints, _settings.CustomDeviceNames, _settings.CachedDeviceNames, _settings.BlacklistedAudioEndpointIds);
                DeskPresets.Add(presetVm);
            }

            RigPresets.Clear();
            for (int i = 0; i < _settings.RigPresets.Count; i++)
            {
                var preset = _settings.RigPresets[i];
                DeviceOptionResolver.EnsureDisplayOption(preset.TargetMonitorId, AvailableDisplayOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.PrimaryAudioId = DeviceOptionResolver.ResolveAudioOption(preset.PrimaryAudioId, audioEndpoints, AvailableAudioOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.SecondaryAudioId = DeviceOptionResolver.ResolveAudioOption(preset.SecondaryAudioId, audioEndpoints, AvailableAudioOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.FallbackAudioId = DeviceOptionResolver.ResolveAudioOption(preset.FallbackAudioId, audioEndpoints, AvailableAudioOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.PrimaryMicrophoneId = DeviceOptionResolver.ResolveAudioOption(preset.PrimaryMicrophoneId, audioEndpoints, AvailableMicrophoneOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.SecondaryMicrophoneId = DeviceOptionResolver.ResolveAudioOption(preset.SecondaryMicrophoneId, audioEndpoints, AvailableMicrophoneOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);
                preset.FallbackMicrophoneId = DeviceOptionResolver.ResolveAudioOption(preset.FallbackMicrophoneId, audioEndpoints, AvailableMicrophoneOptions, _settings.CustomDeviceNames, _settings.CachedDeviceNames);

                var presetVm = new PresetConfigurationItemViewModel(
                    preset,
                    i,
                    "RigActivePreset",
                    i == _settings.ActiveRigPresetIndex,
                    OnRigPresetActivated,
                    profile: ProfileMode.SimRig,
                    onActivateNow: p => _ = ActivatePresetNowAsync(ProfileMode.SimRig, p.PresetIndex),
                    onIdentifyMonitors: IdentifyMonitors);
                presetVm.HydrateHardwareContext(displays, _cachedRawAudioEndpoints, _settings.CustomDeviceNames, _settings.CachedDeviceNames, _settings.BlacklistedAudioEndpointIds);
                RigPresets.Add(presetVm);
            }

            if (SelectedPreset == null)
            {
                var initialPreset = CurrentProfile == ProfileMode.SimRig
                    ? (RigPresets.FirstOrDefault(p => p.IsActive) ?? RigPresets.FirstOrDefault())
                    : (DeskPresets.FirstOrDefault(p => p.IsActive) ?? DeskPresets.FirstOrDefault());
                if (initialPreset != null)
                {
                    SelectPresetForNavigation(initialPreset);
                }
            }
            else
            {
                var matchingPreset = (SelectedPreset.Profile == ProfileMode.SimRig ? RigPresets : DeskPresets)
                    .FirstOrDefault(p => p.PresetIndex == SelectedPreset.PresetIndex)
                    ?? DeskPresets.FirstOrDefault();
                if (matchingPreset != null)
                {
                    SelectPresetForNavigation(matchingPreset);
                }
            }

            PopulateNicknameCollections(displays, _cachedRawAudioEndpoints);

            AudioEndpointsVisibility.Clear();
            var hiddenList = _settings.HiddenAudioEndpointIds;
            var blacklistedList = _settings.BlacklistedAudioEndpointIds;
            foreach (var a in audioEndpoints)
            {
                bool isVisible = !hiddenList.Any(hId => DeviceOptionResolver.MatchesEndpoint(hId, a.Id));
                bool isBlacklisted = blacklistedList.Any(bId => DeviceOptionResolver.MatchesEndpoint(bId, a.Id));
                AudioEndpointsVisibility.Add(new AudioEndpointVisibilityItemViewModel(
                    a.Id,
                    a.Name,
                    a.AdapterDescription,
                    isVisible,
                    OnAudioVisibilityChangedAsync,
                    a.State,
                    isBlacklisted,
                    OnAudioBlacklistChangedAsync));
            }
            FilteredAudioEndpoints?.Refresh();
            OnPropertyChanged(nameof(FilterStatusSummary));

            PopulateMonitorTiles(displays, _settings);

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

    public void AddNewNickname()
    {
        if (string.IsNullOrWhiteSpace(SelectedNewNicknameDeviceId) || string.IsNullOrWhiteSpace(NewNicknameText))
        {
            return;
        }

        string targetId = SelectedNewNicknameDeviceId;
        string trimmedNick = NewNicknameText.Trim();

        if (_settings != null)
        {
            _settings.CustomDeviceNames[targetId] = trimmedNick;
            _referencedDeviceIds = DeviceOptionResolver.CollectReferencedDeviceIds(_settings);
        }

        var existingItem = DeviceNicknames.FirstOrDefault(n => string.Equals(n.DeviceId, targetId, StringComparison.OrdinalIgnoreCase));
        if (existingItem != null)
        {
            existingItem.CustomNickname = trimmedNick;
        }
        else
        {
            var newItem = new DeviceNicknameItemViewModel(targetId, "Device", targetId, trimmedNick, RemoveNickname);
            DeviceNicknames.Add(newItem);
        }

        NewNicknameText = string.Empty;
        RefreshNicknameSelections();
        StatusMessage = $"Added nickname '{trimmedNick}'. Click Save Changes to persist.";
    }

    public void RemoveNickname(DeviceNicknameItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.CustomNickname = string.Empty;
        if (_settings != null)
        {
            _settings.CustomDeviceNames.Remove(item.DeviceId);
            _referencedDeviceIds = DeviceOptionResolver.CollectReferencedDeviceIds(_settings);
        }

        RefreshNicknameSelections();
        StatusMessage = $"Removed nickname for '{item.HardwareName}'.";
    }

    private void PopulateNicknameCollections(IReadOnlyList<DisplayDeviceInfo> displays, IReadOnlyList<AudioEndpointInfo> audioEndpoints)
    {
        DeviceNicknames.Clear();
        var seenNicknameIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in displays)
        {
            string nick = _settings?.CustomDeviceNames.GetValueOrDefault(d.MonitorId, string.Empty) ?? string.Empty;
            DeviceNicknames.Add(new DeviceNicknameItemViewModel(d.MonitorId, "Display", d.FriendlyName, nick, RemoveNickname));
            seenNicknameIds.Add(d.MonitorId);
        }

        foreach (var a in audioEndpoints)
        {
            bool isRelevant = a.State != DevicePresenceState.NotPresent ||
                              _referencedDeviceIds.Any(r => DeviceOptionResolver.MatchesEndpoint(r, a.Id)) ||
                              (_settings?.CustomDeviceNames.ContainsKey(a.Id) == true);
            if (!isRelevant)
            {
                continue;
            }

            string kind = a.Flow == AudioDeviceFlow.Capture ? "Microphone / Input" : "Audio Playback";
            string nick = _settings?.CustomDeviceNames.GetValueOrDefault(a.Id, string.Empty) ?? string.Empty;
            DeviceNicknames.Add(new DeviceNicknameItemViewModel(a.Id, kind, a.Name, nick, RemoveNickname));
            seenNicknameIds.Add(a.Id);
        }

        if (_settings != null)
        {
            foreach (var (id, customNick) in _settings.CustomDeviceNames)
            {
                if (!seenNicknameIds.Contains(id) && !string.IsNullOrWhiteSpace(customNick))
                {
                    string cachedName = _settings.CachedDeviceNames.GetValueOrDefault(id, id);
                    string kind = id.Contains('{') ? "Audio (Offline)" : "Display (Offline)";
                    DeviceNicknames.Add(new DeviceNicknameItemViewModel(id, kind, $"{cachedName} (Offline)", customNick, RemoveNickname));
                    seenNicknameIds.Add(id);
                }
            }
        }

        RefreshNicknameSelections();
    }

    private void RefreshNicknameSelections()
    {
        ConfiguredNicknames.Clear();
        UnnicknamedDeviceOptions.Clear();

        foreach (var item in DeviceNicknames)
        {
            if (!string.IsNullOrWhiteSpace(item.CustomNickname))
            {
                ConfiguredNicknames.Add(item);
            }
            else
            {
                UnnicknamedDeviceOptions.Add(new DeviceSelectionOption(item.DeviceId, $"[{item.DeviceType}] {item.HardwareName}"));
            }
        }

        SelectedNewNicknameDeviceId = UnnicknamedDeviceOptions.FirstOrDefault()?.Id ?? string.Empty;
    }

    public async Task ActivatePresetNowAsync(ProfileMode profile, int presetIndex)
    {
        await SaveSettingsAsync();
        IsBusy = true;
        StatusMessage = $"Activating {profile} Preset {presetIndex + 1}...";
        try
        {
            bool ok = await _coordinator.SwitchToPresetAsync(profile, presetIndex);
            StatusMessage = ok
                ? $"Activated {profile} Preset {presetIndex + 1}."
                : $"Failed to activate {profile} Preset {presetIndex + 1}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Activation error: {ex.Message}";
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
            StatusMessage = success ? $"Successfully switched to {targetProfile} profile." : $"Failed to switch to {targetProfile} profile.";
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

            for (int i = 0; i < DeskPresets.Count; i++)
                if (i < settings.DeskPresets.Count) DeskPresets[i].ApplyTo(settings.DeskPresets[i]);
            var activeDeskIdx = DeskPresets.TakeWhile(p => !p.IsActive).Count();
            settings.ActiveDeskPresetIndex = activeDeskIdx < DeskPresets.Count ? activeDeskIdx : 0;

            for (int i = 0; i < RigPresets.Count; i++)
                if (i < settings.RigPresets.Count) RigPresets[i].ApplyTo(settings.RigPresets[i]);
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
            settings.AudioToggleHotkey = AudioToggleHotkey;
            settings.EnforcePrimaryAudio = EnforcePrimaryAudio;

            settings.StartMinimizedToTray = StartMinimizedToTray;
            settings.ShowToastNotifications = ShowToastNotifications;
            settings.EnableAutoUpdateCheck = EnableAutoUpdateCheck;
            settings.IgnoredReleaseVersion = IgnoredReleaseVersion;

            foreach (var nickItem in DeviceNicknames)
            {
                if (!string.IsNullOrWhiteSpace(nickItem.CustomNickname))
                    settings.CustomDeviceNames[nickItem.DeviceId] = nickItem.CustomNickname.Trim();
                else
                    settings.CustomDeviceNames.Remove(nickItem.DeviceId);
            }

            _referencedDeviceIds = DeviceOptionResolver.CollectReferencedDeviceIds(settings);

            await _settingsStorage.SaveSettingsAsync(settings);
            _settings = settings;

            RegisterGlobalHotkeys(settings);
            _trayIconService.ShowToastNotifications = settings.ShowToastNotifications;
            _trayIconService.RefreshPresets(settings);

            if (string.IsNullOrEmpty(StatusMessage) || !StatusMessage.StartsWith("Warning:", StringComparison.Ordinal))
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

    public async Task CheckForUpdatesManualAsync() => await CheckForUpdatesInternalAsync(isManual: true);

    public async Task CheckForUpdatesAutoAsync()
    {
        _settings ??= await _settingsStorage.LoadSettingsAsync();
        if (!_settings.EnableAutoUpdateCheck) return;
        if (_settings.UpdateCheckSkippedUntil.HasValue && DateTime.UtcNow < _settings.UpdateCheckSkippedUntil.Value) return;

        await CheckForUpdatesInternalAsync(isManual: false);
    }

    private async Task CheckForUpdatesInternalAsync(bool isManual)
    {
        IsBusy = true;
        StatusMessage = "Checking for application updates...";
        try
        {
            _settings ??= await _settingsStorage.LoadSettingsAsync();
            var resultMsg = await UpdateCheckHandler.CheckForUpdatesAsync(
                _updateCheckService,
                _settings,
                _settingsStorage,
                isManual,
                tag => IgnoredReleaseVersion = tag);

            if (!string.IsNullOrWhiteSpace(resultMsg)) StatusMessage = resultMsg;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update check error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task HandleUpdateUserChoiceAsync(UpdateUserChoice choice, UpdateInfo updateInfo)
    {
        _settings ??= await _settingsStorage.LoadSettingsAsync();
        var resultMsg = await UpdateCheckHandler.HandleUserChoiceAsync(
            choice,
            updateInfo,
            _settings,
            _settingsStorage,
            tag => IgnoredReleaseVersion = tag);

        if (!string.IsNullOrWhiteSpace(resultMsg)) StatusMessage = resultMsg;
    }

    public void RegisterGlobalHotkeys(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _hotkeyService.UnregisterAll();
        var failedHotkeys = new List<string>();

        void TryRegister(string? hotkey, Action action)
        {
            if (!string.IsNullOrWhiteSpace(hotkey) && !_hotkeyService.RegisterHotkey(hotkey, action))
                failedHotkeys.Add(hotkey);
        }

        TryRegister(settings.ToggleHotkey, () =>
        {
            var next = _coordinator.CurrentProfile == ProfileMode.Desk ? ProfileMode.SimRig : ProfileMode.Desk;
            _ = _coordinator.SwitchProfileAsync(next);
        });
        TryRegister(settings.DeskHotkey, () => _ = _coordinator.SwitchProfileAsync(ProfileMode.Desk));
        TryRegister(settings.RigHotkey, () => _ = _coordinator.SwitchProfileAsync(ProfileMode.SimRig));
        TryRegister(settings.AudioToggleHotkey, () =>
        {
            var task = _coordinator.ToggleAudioOutputModeAsync();
            _ = task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    var mode = t.Result;
                    _trayIconService.UpdateAudioState(mode);
                    _trayIconService.ShowNotification("RigSwitch Audio", $"Switched audio output to {(mode == AudioOutputMode.Primary ? "Speakers" : "Headset")}.");
                }
                else if (t.IsFaulted)
                {
                    _trayIconService.ShowNotification("RigSwitch Audio Error", $"Failed to toggle audio output: {t.Exception?.GetBaseException().Message}");
                }
            }, TaskScheduler.Default);
        });

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
            StatusMessage = $"Warning: Failed to register hotkey(s): {string.Join(", ", failedHotkeys)}";
    }

    private void PopulateAudioSelectionOptions(IEnumerable<AudioEndpointInfo> endpoints)
    {
        var nonBlacklisted = DeviceOptionResolver.FilterBlacklistedEndpoints(endpoints, _settings?.BlacklistedAudioEndpointIds ?? []);

        AvailableAudioOptions.Clear();
        AvailableAudioOptions.Add(new DeviceSelectionOption(string.Empty, "— Select Audio Device (Unassigned) —"));
        foreach (var a in nonBlacklisted.Where(x => x.State == DevicePresenceState.Active && x.Flow == AudioDeviceFlow.Playback))
        {
            string customNick = _settings?.CustomDeviceNames.GetValueOrDefault(a.Id, string.Empty) ?? string.Empty;
            var name = !string.IsNullOrWhiteSpace(customNick) ? $"{customNick} ({a.Name})" : a.Name;
            AvailableAudioOptions.Add(new DeviceSelectionOption(a.Id, name));
        }

        AvailableMicrophoneOptions.Clear();
        AvailableMicrophoneOptions.Add(new DeviceSelectionOption(string.Empty, "— Select Microphone (Unassigned / System Default) —"));
        foreach (var a in nonBlacklisted.Where(x => x.State == DevicePresenceState.Active && x.Flow == AudioDeviceFlow.Capture))
        {
            string customNick = _settings?.CustomDeviceNames.GetValueOrDefault(a.Id, string.Empty) ?? string.Empty;
            var name = !string.IsNullOrWhiteSpace(customNick) ? $"{customNick} ({a.Name})" : a.Name;
            AvailableMicrophoneOptions.Add(new DeviceSelectionOption(a.Id, name));
        }
    }

    private void OnDeskPresetActivated(PresetConfigurationItemViewModel activated)
    {
        foreach (var preset in DeskPresets) if (preset != activated) preset.IsActive = false;
    }

    private void OnRigPresetActivated(PresetConfigurationItemViewModel activated)
    {
        foreach (var preset in RigPresets) if (preset != activated) preset.IsActive = false;
    }

    private async Task OnAudioVisibilityChangedAsync(AudioEndpointVisibilityItemViewModel item, bool isVisible)
    {
        try
        {
            await _audioDirector.SetEndpointVisibilityAsync(item.Id, isVisible);
            if (_settings != null)
            {
                if (isVisible)
                    _settings.HiddenAudioEndpointIds.RemoveAll(id => DeviceOptionResolver.MatchesEndpoint(id, item.Id));
                else if (!_settings.HiddenAudioEndpointIds.Any(id => DeviceOptionResolver.MatchesEndpoint(id, item.Id)))
                    _settings.HiddenAudioEndpointIds.Add(item.Id);

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

    private async Task OnAudioBlacklistChangedAsync(AudioEndpointVisibilityItemViewModel item, bool isBlacklisted)
    {
        try
        {
            if (_settings != null)
            {
                if (isBlacklisted)
                {
                    if (!_settings.BlacklistedAudioEndpointIds.Any(id => DeviceOptionResolver.MatchesEndpoint(id, item.Id)))
                    {
                        _settings.BlacklistedAudioEndpointIds.Add(item.Id);
                    }
                }
                else
                {
                    _settings.BlacklistedAudioEndpointIds.RemoveAll(id => DeviceOptionResolver.MatchesEndpoint(id, item.Id));
                }

                await _settingsStorage.SaveSettingsAsync(_settings);
                PopulateAudioSelectionOptions(_cachedRawAudioEndpoints);
            }
            StatusMessage = $"Audio endpoint '{item.Name}' {(isBlacklisted ? "blacklisted (excluded from routing)" : "removed from blacklist")}.";
        }
        catch (Exception ex)
        {
            item.RevertBlacklisted(!isBlacklisted);
            StatusMessage = $"Failed to update blacklist for {item.Name}: {ex.Message}";
        }
    }

    private void OnProfileChanged(object? sender, ProfileChangedEventArgs e)
    {
        Application.Current?.Dispatcher?.Invoke(() => CurrentProfile = _coordinator.CurrentProfile);
    }

    private static string ResolveAppVersionText()
    {
        var infoVer = typeof(MainSettingsViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVer))
        {
            int plusIdx = infoVer.IndexOf('+');
            string clean = plusIdx > 0 ? infoVer[..plusIdx] : infoVer;
            return clean.StartsWith('v') ? clean : $"v{clean}";
        }

        var ver = typeof(MainSettingsViewModel).Assembly.GetName().Version;
        return ver != null ? $"v{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}" : "v1.4.0";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _coordinator.ProfileChanged -= OnProfileChanged;
    }
}
