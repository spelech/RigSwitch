namespace RigSwitch.App.ViewModels;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Win32;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;

/// <summary>
/// Wraps a <see cref="WorkstationPreset"/> for MVVM data binding, per-preset 2D monitor topology, and ranked audio/microphone priority routing.
/// </summary>
public sealed class PresetConfigurationItemViewModel : ViewModelBase
{
    private const double StageReferenceWidth = 520.0;
    private const double StageReferenceHeight = 164.0;
    private const double StageScaleFactor = 0.056;
    private const int SnapThresholdPixels = 160;

    private readonly Action<PresetConfigurationItemViewModel>? _onActivated;
    private readonly Action<PresetConfigurationItemViewModel>? _onActivateNow;
    private readonly Action? _onIdentifyMonitors;
    private string _name;
    private string _targetMonitorId;
    private List<string> _targetMonitorIds = [];
    private List<MonitorPlacement> _monitorPlacements = [];
    private List<string> _audioPriorityIds = [];
    private List<string> _microphonePriorityIds = [];
    private string _primaryAudioId;
    private string _secondaryAudioId = string.Empty;
    private string _fallbackAudioId;
    private string _primaryMicrophoneId = string.Empty;
    private string _secondaryMicrophoneId = string.Empty;
    private string _fallbackMicrophoneId = string.Empty;
    private PresetVolumeBehavior _playbackVolumeMode = PresetVolumeBehavior.Retain;
    private int _playbackVolumePercent = 50;
    private bool _playbackVolumeIsMuted;
    private PresetVolumeBehavior _microphoneVolumeMode = PresetVolumeBehavior.Retain;
    private int _microphoneVolumePercent = 50;
    private bool _microphoneVolumeIsMuted;
    private PresetHdrMode _hdrMode = PresetHdrMode.Retain;
    private string _directHotkey;
    private bool _isActive;
    private bool _isSelectedInNav;
    private string _selectedAudioToAddId = string.Empty;
    private string _selectedMicToAddId = string.Empty;

    private IReadOnlyList<DisplayDeviceInfo> _latestDisplays = [];
    private IReadOnlyList<AudioEndpointInfo> _latestAudioEndpoints = [];
    private IDictionary<string, string> _customNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private IDictionary<string, string> _cachedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _blacklistedIds = [];

    /// <summary>
    /// Gets the application lifecycle hooks configured for this preset.
    /// </summary>
    public ObservableCollection<ApplicationHookItemViewModel> ApplicationHooks { get; } = [];

    /// <summary>
    /// Gets the monitors currently enabled on this preset's 2D spatial stage.
    /// </summary>
    public ObservableCollection<PresetMonitorCanvasItemViewModel> StageMonitors { get; } = [];

    /// <summary>
    /// Gets the monitors not enabled in this preset ("Off in Preset" tray).
    /// </summary>
    public ObservableCollection<PresetMonitorCanvasItemViewModel> UnassignedMonitors { get; } = [];

    /// <summary>
    /// Gets the ordered playback audio priority list for this preset.
    /// </summary>
    public ObservableCollection<RankedAudioItemViewModel> AudioRanking { get; } = [];

    /// <summary>
    /// Gets the ordered microphone priority list for this preset.
    /// </summary>
    public ObservableCollection<RankedAudioItemViewModel> MicrophoneRanking { get; } = [];

    /// <summary>
    /// Gets the playback audio options that are not yet in <see cref="AudioRanking"/>.
    /// </summary>
    public ObservableCollection<DeviceSelectionOption> AddableAudioOptions { get; } = [];

    /// <summary>
    /// Gets the microphone options that are not yet in <see cref="MicrophoneRanking"/>.
    /// </summary>
    public ObservableCollection<DeviceSelectionOption> AddableMicrophoneOptions { get; } = [];

    public string SelectedAudioToAddId
    {
        get => _selectedAudioToAddId;
        set => SetProperty(ref _selectedAudioToAddId, value ?? string.Empty);
    }

    public string SelectedMicToAddId
    {
        get => _selectedMicToAddId;
        set => SetProperty(ref _selectedMicToAddId, value ?? string.Empty);
    }

    /// <summary>
    /// Gets the command to add a new application lifecycle hook using a file dialog.
    /// </summary>
    public ICommand AddApplicationHookCommand { get; }

    public ICommand AddAudioToRankingCommand { get; }

    public ICommand AddMicToRankingCommand { get; }

    public ICommand CaptureWindowsLayoutCommand { get; }

    public ICommand IdentifyMonitorsCommand { get; }

    public ICommand ActivatePresetNowCommand { get; }

    /// <summary>
    /// Gets the zero-based index of this preset within its profile mode.
    /// </summary>
    public int PresetIndex { get; }

    /// <summary>
    /// Gets the profile mode this preset belongs to.
    /// </summary>
    public ProfileMode Profile { get; }

    /// <summary>
    /// Gets the mutually exclusive radio button group name.
    /// </summary>
    public string GroupName { get; }

    /// <summary>
    /// Gets or sets whether this preset is currently selected in the left navigation rail.
    /// </summary>
    public bool IsSelectedInNav
    {
        get => _isSelectedInNav;
        set => SetProperty(ref _isSelectedInNav, value);
    }

    /// <summary>
    /// Gets or sets the user-friendly display name of this preset.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(Header));
                OnPropertyChanged(nameof(NavSubLabel));
            }
        }
    }

    public string NavTitle => $"{(Profile == ProfileMode.Desk ? "Desk" : "Sim Rig")} {PresetIndex + 1}";

    public string NavSubLabel => string.IsNullOrWhiteSpace(Name) ? NavTitle : Name;

    /// <summary>
    /// Gets or sets the target monitor hardware identifier or EDID.
    /// </summary>
    public string TargetMonitorId
    {
        get => _targetMonitorId;
        set
        {
            if (SetProperty(ref _targetMonitorId, value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (_targetMonitorIds.Count > 0)
                    {
                        _targetMonitorIds[0] = value;
                    }
                    else
                    {
                        _targetMonitorIds.Add(value);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the list of target monitor hardware identifiers.
    /// </summary>
    public List<string> TargetMonitorIds
    {
        get => _targetMonitorIds;
        set
        {
            _targetMonitorIds = value ?? [];
            if (_targetMonitorIds.Count > 0)
            {
                TargetMonitorId = _targetMonitorIds[0];
            }
        }
    }

    /// <summary>
    /// Gets or sets the primary audio playback endpoint GUID identifier.
    /// </summary>
    public string PrimaryAudioId
    {
        get => _primaryAudioId;
        set => SetProperty(ref _primaryAudioId, value);
    }

    /// <summary>
    /// Gets or sets the secondary audio playback endpoint GUID identifier.
    /// </summary>
    public string SecondaryAudioId
    {
        get => _secondaryAudioId;
        set => SetProperty(ref _secondaryAudioId, value);
    }

    /// <summary>
    /// Gets or sets the fallback audio playback endpoint GUID identifier.
    /// </summary>
    public string FallbackAudioId
    {
        get => _fallbackAudioId;
        set => SetProperty(ref _fallbackAudioId, value);
    }

    /// <summary>
    /// Gets or sets the primary microphone / capture endpoint GUID identifier.
    /// </summary>
    public string PrimaryMicrophoneId
    {
        get => _primaryMicrophoneId;
        set => SetProperty(ref _primaryMicrophoneId, value);
    }

    /// <summary>
    /// Gets or sets the secondary microphone / capture endpoint GUID identifier.
    /// </summary>
    public string SecondaryMicrophoneId
    {
        get => _secondaryMicrophoneId;
        set => SetProperty(ref _secondaryMicrophoneId, value);
    }

    /// <summary>
    /// Gets or sets the fallback microphone / capture endpoint GUID identifier.
    /// </summary>
    public string FallbackMicrophoneId
    {
        get => _fallbackMicrophoneId;
        set => SetProperty(ref _fallbackMicrophoneId, value);
    }

    /// <summary>
    /// Gets or sets the playback volume behavior mode (Retain or Custom).
    /// </summary>
    public PresetVolumeBehavior PlaybackVolumeMode
    {
        get => _playbackVolumeMode;
        set
        {
            if (SetProperty(ref _playbackVolumeMode, value))
            {
                OnPropertyChanged(nameof(IsCustomPlaybackVolume));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether custom playback volume adjustment is enabled.
    /// </summary>
    public bool IsCustomPlaybackVolume => PlaybackVolumeMode == PresetVolumeBehavior.Custom;

    /// <summary>
    /// Gets or sets the target playback volume percent [0, 100].
    /// </summary>
    public int PlaybackVolumePercent
    {
        get => _playbackVolumePercent;
        set => SetProperty(ref _playbackVolumePercent, Math.Clamp(value, 0, 100));
    }

    /// <summary>
    /// Gets or sets a value indicating whether playback should be muted.
    /// </summary>
    public bool PlaybackVolumeIsMuted
    {
        get => _playbackVolumeIsMuted;
        set => SetProperty(ref _playbackVolumeIsMuted, value);
    }

    /// <summary>
    /// Gets or sets the microphone input volume behavior mode (Retain or Custom).
    /// </summary>
    public PresetVolumeBehavior MicrophoneVolumeMode
    {
        get => _microphoneVolumeMode;
        set
        {
            if (SetProperty(ref _microphoneVolumeMode, value))
            {
                OnPropertyChanged(nameof(IsCustomMicrophoneVolume));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether custom microphone volume adjustment is enabled.
    /// </summary>
    public bool IsCustomMicrophoneVolume => MicrophoneVolumeMode == PresetVolumeBehavior.Custom;

    /// <summary>
    /// Gets or sets the target microphone input volume percent [0, 100].
    /// </summary>
    public int MicrophoneVolumePercent
    {
        get => _microphoneVolumePercent;
        set => SetProperty(ref _microphoneVolumePercent, Math.Clamp(value, 0, 100));
    }

    /// <summary>
    /// Gets or sets a value indicating whether microphone input should be muted.
    /// </summary>
    public bool MicrophoneVolumeIsMuted
    {
        get => _microphoneVolumeIsMuted;
        set => SetProperty(ref _microphoneVolumeIsMuted, value);
    }

    /// <summary>
    /// Gets or sets the native Windows HDR switching mode for this preset.
    /// </summary>
    public PresetHdrMode HdrMode
    {
        get => _hdrMode;
        set => SetProperty(ref _hdrMode, value);
    }

    /// <summary>
    /// Gets or sets the direct hotkey combination used to activate this preset.
    /// </summary>
    public string DirectHotkey
    {
        get => _directHotkey;
        set => SetProperty(ref _directHotkey, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this preset is the default/active preset for its profile.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(Header));
                if (value)
                {
                    _onActivated?.Invoke(this);
                }
            }
        }
    }

    /// <summary>
    /// Gets the formatted display header for tab and list representations.
    /// </summary>
    public string Header => IsActive ? $"★ {Name}" : Name;

    /// <summary>
    /// Gets the command that launches Windows Display Settings ("ms-settings:display").
    /// </summary>
    public ICommand OpenDisplaySettingsCommand { get; }

    public PresetConfigurationItemViewModel(
        WorkstationPreset preset,
        int presetIndex,
        string groupName,
        bool isActive,
        Action<PresetConfigurationItemViewModel>? onActivated = null,
        Action? openDisplaySettingsAction = null,
        Func<string?>? selectFileAction = null,
        ProfileMode profile = ProfileMode.Desk,
        Action<PresetConfigurationItemViewModel>? onActivateNow = null,
        Action? onIdentifyMonitors = null)
    {
        ArgumentNullException.ThrowIfNull(preset);
        PresetIndex = presetIndex;
        GroupName = groupName;
        Profile = groupName.Contains("Rig", StringComparison.OrdinalIgnoreCase) ? ProfileMode.SimRig : profile;
        _isActive = isActive;
        _onActivated = onActivated;
        _onActivateNow = onActivateNow;
        _onIdentifyMonitors = onIdentifyMonitors;

        _name = preset.Name;
        _targetMonitorIds = [.. preset.TargetMonitorIds];
        _targetMonitorId = preset.TargetMonitorId;
        _monitorPlacements = preset.MonitorPlacements?.Select(p => p with { }).ToList() ?? [];
        _audioPriorityIds = [.. preset.GetEffectiveAudioPriorityList()];
        _microphonePriorityIds = [.. preset.GetEffectiveMicrophonePriorityList()];
        _primaryAudioId = preset.PrimaryAudioId;
        _secondaryAudioId = preset.SecondaryAudioId ?? string.Empty;
        _fallbackAudioId = preset.FallbackAudioId;
        _primaryMicrophoneId = preset.PrimaryMicrophoneId ?? string.Empty;
        _secondaryMicrophoneId = preset.SecondaryMicrophoneId ?? string.Empty;
        _fallbackMicrophoneId = preset.FallbackMicrophoneId ?? string.Empty;
        _playbackVolumeMode = preset.PlaybackVolume?.Mode ?? PresetVolumeBehavior.Retain;
        _playbackVolumePercent = preset.PlaybackVolume?.VolumePercent ?? 50;
        _playbackVolumeIsMuted = preset.PlaybackVolume?.IsMuted ?? false;
        _microphoneVolumeMode = preset.MicrophoneVolume?.Mode ?? PresetVolumeBehavior.Retain;
        _microphoneVolumePercent = preset.MicrophoneVolume?.VolumePercent ?? 50;
        _microphoneVolumeIsMuted = preset.MicrophoneVolume?.IsMuted ?? false;
        _hdrMode = preset.HdrMode;
        _directHotkey = preset.DirectHotkey;

        if (preset.ApplicationHooks != null)
        {
            foreach (var hook in preset.ApplicationHooks)
            {
                ApplicationHooks.Add(new ApplicationHookItemViewModel(hook, RemoveApplicationHook));
            }
        }

        OpenDisplaySettingsCommand = new RelayCommand(openDisplaySettingsAction ?? LaunchDisplaySettings);
        AddApplicationHookCommand = new RelayCommand(() =>
        {
            var selectedFile = selectFileAction != null ? selectFileAction() : PromptForExecutablePath();
            if (!string.IsNullOrWhiteSpace(selectedFile))
            {
                var newHook = new PresetApplicationHook
                {
                    ExecutablePath = selectedFile,
                    CloseOnSwitchAway = true
                };
                ApplicationHooks.Add(new ApplicationHookItemViewModel(newHook, RemoveApplicationHook));
            }
        });

        AddAudioToRankingCommand = new RelayCommand(() => AddAudioEndpoint(SelectedAudioToAddId));
        AddMicToRankingCommand = new RelayCommand(() => AddMicrophoneEndpoint(SelectedMicToAddId));
        CaptureWindowsLayoutCommand = new RelayCommand(CaptureWindowsLayout);
        IdentifyMonitorsCommand = new RelayCommand(() => _onIdentifyMonitors?.Invoke());
        ActivatePresetNowCommand = new RelayCommand(() => _onActivateNow?.Invoke(this));

        RebuildRankedCollections();
    }

    public void HydrateHardwareContext(
        IReadOnlyList<DisplayDeviceInfo> displays,
        IReadOnlyList<AudioEndpointInfo> audioEndpoints,
        IDictionary<string, string> customNames,
        IDictionary<string, string> cachedNames,
        IReadOnlyList<string> blacklistedIds)
    {
        _latestDisplays = displays ?? [];
        _latestAudioEndpoints = audioEndpoints ?? [];
        _customNames = customNames ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _cachedNames = cachedNames ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _blacklistedIds = blacklistedIds ?? [];

        RebuildMonitorStageAndTray();
        RebuildRankedCollections();
    }

    public void AddMonitorToStage(PresetMonitorCanvasItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (StageMonitors.Contains(item))
        {
            return;
        }

        UnassignedMonitors.Remove(item);
        item.IsOnStage = true;

        if (StageMonitors.Count == 0)
        {
            item.IsPrimary = true;
            item.PositionX = 0;
            item.PositionY = 0;
        }
        else
        {
            int rightEdge = StageMonitors.Max(m => m.PositionX + m.Width);
            item.IsPrimary = false;
            item.PositionX = rightEdge;
            item.PositionY = 0;
        }

        StageMonitors.Add(item);
        SyncMonitorsToBackingLists();
        UpdateCanvasLayoutPositions();
    }

    public void RemoveMonitorFromStage(PresetMonitorCanvasItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!StageMonitors.Remove(item))
        {
            return;
        }

        bool wasPrimary = item.IsPrimary;
        item.IsPrimary = false;
        item.IsOnStage = false;
        UnassignedMonitors.Add(item);

        if (wasPrimary && StageMonitors.Count > 0)
        {
            SetPrimaryMonitor(StageMonitors[0]);
        }
        else
        {
            SyncMonitorsToBackingLists();
            UpdateCanvasLayoutPositions();
        }
    }

    public void SetPrimaryMonitor(PresetMonitorCanvasItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!StageMonitors.Contains(item))
        {
            return;
        }

        int deltaX = -item.PositionX;
        int deltaY = -item.PositionY;

        foreach (var mon in StageMonitors)
        {
            mon.IsPrimary = ReferenceEquals(mon, item);
            mon.PositionX += deltaX;
            mon.PositionY += deltaY;
        }

        SyncMonitorsToBackingLists();
        UpdateCanvasLayoutPositions();
    }

    public void CycleMonitorHdr(PresetMonitorCanvasItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.HdrMode = item.HdrMode switch
        {
            PresetHdrMode.Retain => PresetHdrMode.Enable,
            PresetHdrMode.Enable => PresetHdrMode.Disable,
            _ => PresetHdrMode.Retain
        };
        if (item.IsPrimary)
        {
            HdrMode = item.HdrMode;
        }

        SyncMonitorsToBackingLists();
    }

    public void MoveMonitorOnStage(PresetMonitorCanvasItemViewModel item, int newX, int newY)
    {
        ArgumentNullException.ThrowIfNull(item);
        int snappedX = newX;
        int snappedY = newY;

        foreach (var other in StageMonitors)
        {
            if (ReferenceEquals(other, item))
            {
                continue;
            }

            // Horizontal edge snapping (left-to-right, right-to-left, left-to-left)
            if (Math.Abs((newX + item.Width) - other.PositionX) <= SnapThresholdPixels)
            {
                snappedX = other.PositionX - item.Width;
            }
            else if (Math.Abs(newX - (other.PositionX + other.Width)) <= SnapThresholdPixels)
            {
                snappedX = other.PositionX + other.Width;
            }
            else if (Math.Abs(newX - other.PositionX) <= SnapThresholdPixels)
            {
                snappedX = other.PositionX;
            }

            // Vertical edge snapping (top-to-top, top-to-bottom, bottom-to-top)
            if (Math.Abs(newY - other.PositionY) <= SnapThresholdPixels)
            {
                snappedY = other.PositionY;
            }
            else if (Math.Abs((newY + item.Height) - other.PositionY) <= SnapThresholdPixels)
            {
                snappedY = other.PositionY - item.Height;
            }
            else if (Math.Abs(newY - (other.PositionY + other.Height)) <= SnapThresholdPixels)
            {
                snappedY = other.PositionY + other.Height;
            }
        }

        item.PositionX = snappedX;
        item.PositionY = snappedY;

        if (!_stageLayoutFrozen && item.IsPrimary && (item.PositionX != 0 || item.PositionY != 0))
        {
            SetPrimaryMonitor(item);
            return;
        }

        SyncMonitorsToBackingLists();
        UpdateCanvasLayoutPositions();
    }

    public void CaptureWindowsLayout()
    {
        if (_latestDisplays.Count == 0)
        {
            return;
        }

        StageMonitors.Clear();
        UnassignedMonitors.Clear();
        _monitorPlacements.Clear();

        int num = 1;
        foreach (var d in _latestDisplays)
        {
            string label = ResolveDisplayName(d.MonitorId, d.FriendlyName);
            var existingPlacement = _monitorPlacements.FirstOrDefault(p => string.Equals(p.MonitorId, d.MonitorId, StringComparison.OrdinalIgnoreCase));
            var hdr = existingPlacement?.HdrMode ?? (d.IsHdrEnabled ? PresetHdrMode.Enable : HdrMode);

            var item = new PresetMonitorCanvasItemViewModel(
                displayNumber: num++,
                monitorId: d.MonitorId,
                friendlyName: label,
                positionX: d.PositionX,
                positionY: d.PositionY,
                width: d.Width,
                height: d.Height,
                isPrimary: d.IsPrimary,
                hdrMode: hdr,
                isOnStage: d.IsActive,
                isConnected: true,
                setPrimaryAction: SetPrimaryMonitor,
                cycleHdrAction: CycleMonitorHdr,
                removeFromStageAction: RemoveMonitorFromStage,
                addToStageAction: AddMonitorToStage);

            if (d.IsActive)
            {
                StageMonitors.Add(item);
            }
            else
            {
                UnassignedMonitors.Add(item);
            }
        }

        if (StageMonitors.Count > 0 && !StageMonitors.Any(m => m.IsPrimary))
        {
            StageMonitors[0].IsPrimary = true;
        }

        SyncMonitorsToBackingLists();
        UpdateCanvasLayoutPositions();
    }

    public void AddAudioEndpoint(string? endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return;
        }

        if (_audioPriorityIds.Any(id => DeviceOptionResolver.MatchesEndpoint(id, endpointId)))
        {
            return;
        }

        _audioPriorityIds.Add(endpointId);
        SyncRankingToLegacyAudioProperties();
        RebuildRankedCollections();
    }

    public void MoveAudioUp(RankedAudioItemViewModel item)
    {
        int idx = AudioRanking.IndexOf(item);
        if (idx <= 0)
        {
            return;
        }

        (_audioPriorityIds[idx - 1], _audioPriorityIds[idx]) = (_audioPriorityIds[idx], _audioPriorityIds[idx - 1]);
        SyncRankingToLegacyAudioProperties();
        RebuildRankedCollections();
    }

    public void MoveAudioDown(RankedAudioItemViewModel item)
    {
        int idx = AudioRanking.IndexOf(item);
        if (idx < 0 || idx >= AudioRanking.Count - 1)
        {
            return;
        }

        (_audioPriorityIds[idx], _audioPriorityIds[idx + 1]) = (_audioPriorityIds[idx + 1], _audioPriorityIds[idx]);
        SyncRankingToLegacyAudioProperties();
        RebuildRankedCollections();
    }

    public void RemoveAudioFromRanking(RankedAudioItemViewModel item)
    {
        _audioPriorityIds.RemoveAll(id => DeviceOptionResolver.MatchesEndpoint(id, item.EndpointId));
        SyncRankingToLegacyAudioProperties();
        RebuildRankedCollections();
    }

    public void AddMicrophoneEndpoint(string? endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return;
        }

        if (_microphonePriorityIds.Any(id => DeviceOptionResolver.MatchesEndpoint(id, endpointId)))
        {
            return;
        }

        _microphonePriorityIds.Add(endpointId);
        SyncRankingToLegacyMicrophoneProperties();
        RebuildRankedCollections();
    }

    public void MoveMicrophoneUp(RankedAudioItemViewModel item)
    {
        int idx = MicrophoneRanking.IndexOf(item);
        if (idx <= 0)
        {
            return;
        }

        (_microphonePriorityIds[idx - 1], _microphonePriorityIds[idx]) = (_microphonePriorityIds[idx], _microphonePriorityIds[idx - 1]);
        SyncRankingToLegacyMicrophoneProperties();
        RebuildRankedCollections();
    }

    public void MoveMicrophoneDown(RankedAudioItemViewModel item)
    {
        int idx = MicrophoneRanking.IndexOf(item);
        if (idx < 0 || idx >= MicrophoneRanking.Count - 1)
        {
            return;
        }

        (_microphonePriorityIds[idx], _microphonePriorityIds[idx + 1]) = (_microphonePriorityIds[idx + 1], _microphonePriorityIds[idx]);
        SyncRankingToLegacyMicrophoneProperties();
        RebuildRankedCollections();
    }

    public void RemoveMicrophoneFromRanking(RankedAudioItemViewModel item)
    {
        _microphonePriorityIds.RemoveAll(id => DeviceOptionResolver.MatchesEndpoint(id, item.EndpointId));
        SyncRankingToLegacyMicrophoneProperties();
        RebuildRankedCollections();
    }

    private void RebuildMonitorStageAndTray()
    {
        StageMonitors.Clear();
        UnassignedMonitors.Clear();

        var enabledIds = new List<string>();
        if (_monitorPlacements.Count > 0)
        {
            foreach (var p in _monitorPlacements.OrderByDescending(x => x.IsPrimary))
            {
                if (!string.IsNullOrWhiteSpace(p.MonitorId) && !enabledIds.Contains(p.MonitorId, StringComparer.OrdinalIgnoreCase))
                {
                    enabledIds.Add(p.MonitorId);
                }
            }
        }

        foreach (var id in _targetMonitorIds)
        {
            if (!string.IsNullOrWhiteSpace(id) && !enabledIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                enabledIds.Add(id);
            }
        }

        int displayNum = 1;
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int autoX = 0;

        foreach (var monId in enabledIds)
        {
            var matchedDisplay = _latestDisplays.FirstOrDefault(d => string.Equals(d.MonitorId, monId, StringComparison.OrdinalIgnoreCase));
            var existingPlacement = _monitorPlacements.FirstOrDefault(p => string.Equals(p.MonitorId, monId, StringComparison.OrdinalIgnoreCase));

            int width = existingPlacement?.Width ?? matchedDisplay?.Width ?? 1920;
            int height = existingPlacement?.Height ?? matchedDisplay?.Height ?? 1080;
            bool isPrimary = existingPlacement?.IsPrimary ?? (StageMonitors.Count == 0);
            int posX = existingPlacement?.PositionX ?? (isPrimary ? 0 : autoX);
            int posY = existingPlacement?.PositionY ?? 0;
            var hdr = existingPlacement?.HdrMode ?? HdrMode;

            autoX = Math.Max(autoX, posX + width);

            string friendly = ResolveDisplayName(monId, matchedDisplay?.FriendlyName);
            var vm = new PresetMonitorCanvasItemViewModel(
                displayNumber: displayNum++,
                monitorId: monId,
                friendlyName: friendly,
                positionX: posX,
                positionY: posY,
                width: width,
                height: height,
                isPrimary: isPrimary,
                hdrMode: hdr,
                isOnStage: true,
                isConnected: matchedDisplay != null,
                setPrimaryAction: SetPrimaryMonitor,
                cycleHdrAction: CycleMonitorHdr,
                removeFromStageAction: RemoveMonitorFromStage,
                addToStageAction: AddMonitorToStage);

            StageMonitors.Add(vm);
            seenIds.Add(monId);
        }

        foreach (var d in _latestDisplays)
        {
            if (seenIds.Contains(d.MonitorId))
            {
                continue;
            }

            string friendly = ResolveDisplayName(d.MonitorId, d.FriendlyName);
            var vm = new PresetMonitorCanvasItemViewModel(
                displayNumber: displayNum++,
                monitorId: d.MonitorId,
                friendlyName: friendly,
                positionX: d.PositionX,
                positionY: d.PositionY,
                width: d.Width,
                height: d.Height,
                isPrimary: false,
                hdrMode: HdrMode,
                isOnStage: false,
                isConnected: true,
                setPrimaryAction: SetPrimaryMonitor,
                cycleHdrAction: CycleMonitorHdr,
                removeFromStageAction: RemoveMonitorFromStage,
                addToStageAction: AddMonitorToStage);

            UnassignedMonitors.Add(vm);
            seenIds.Add(d.MonitorId);
        }

        if (StageMonitors.Count > 0 && !StageMonitors.Any(m => m.IsPrimary))
        {
            StageMonitors[0].IsPrimary = true;
        }

        SyncMonitorsToBackingLists();
        UpdateCanvasLayoutPositions();
    }

    private bool _stageLayoutFrozen;
    private double _frozenScale;
    private int _frozenMinX;
    private int _frozenMinY;
    private double _frozenPadLeft;
    private double _frozenPadTop;

    private (double Scale, int MinX, int MinY, double PadLeft, double PadTop) ComputeStageLayout()
    {
        int minX = StageMonitors.Min(m => m.PositionX);
        int minY = StageMonitors.Min(m => m.PositionY);
        int maxX = StageMonitors.Max(m => m.PositionX + m.Width);
        int maxY = StageMonitors.Max(m => m.PositionY + m.Height);

        int totalW = Math.Max(1920, maxX - minX);
        int totalH = Math.Max(1080, maxY - minY);

        double scaleX = (StageReferenceWidth - 36) / totalW;
        double scaleY = (StageReferenceHeight - 28) / totalH;
        double scale = Math.Clamp(Math.Min(scaleX, scaleY), 0.032, StageScaleFactor);

        double contentW = totalW * scale;
        double contentH = totalH * scale;
        double padLeft = Math.Max(14, (StageReferenceWidth - contentW) / 2.0);
        double padTop = Math.Max(12, (StageReferenceHeight - contentH) / 2.0);
        return (scale, minX, minY, padLeft, padTop);
    }

    /// <summary>
    /// Freezes the stage scale and origin for the duration of a drag and returns the
    /// canvas-pixels-per-desktop-pixel scale to use for converting mouse deltas.
    /// </summary>
    public double BeginStageDrag()
    {
        if (StageMonitors.Count == 0)
        {
            return StageScaleFactor;
        }

        var layout = ComputeStageLayout();
        _frozenScale = layout.Scale;
        _frozenMinX = layout.MinX;
        _frozenMinY = layout.MinY;
        _frozenPadLeft = layout.PadLeft;
        _frozenPadTop = layout.PadTop;
        _stageLayoutFrozen = true;
        return layout.Scale;
    }

    public void EndStageDrag()
    {
        if (!_stageLayoutFrozen)
        {
            return;
        }

        _stageLayoutFrozen = false;
        var primary = StageMonitors.FirstOrDefault(m => m.IsPrimary);
        if (primary != null && (primary.PositionX != 0 || primary.PositionY != 0))
        {
            SetPrimaryMonitor(primary);
            return;
        }

        UpdateCanvasLayoutPositions();
    }

    public void UpdateCanvasLayoutPositions()
    {
        if (StageMonitors.Count == 0)
        {
            return;
        }

        double scale;
        int minX;
        int minY;
        double padLeft;
        double padTop;
        if (_stageLayoutFrozen)
        {
            (scale, minX, minY, padLeft, padTop) = (_frozenScale, _frozenMinX, _frozenMinY, _frozenPadLeft, _frozenPadTop);
        }
        else
        {
            (scale, minX, minY, padLeft, padTop) = ComputeStageLayout();
        }

        foreach (var mon in StageMonitors)
        {
            mon.CanvasWidth = Math.Max(124, Math.Round(mon.Width * scale));
            mon.CanvasHeight = Math.Max(62, Math.Round(mon.Height * scale));
            mon.CanvasLeft = Math.Round(padLeft + ((mon.PositionX - minX) * scale));
            mon.CanvasTop = Math.Round(padTop + ((mon.PositionY - minY) * scale));
        }
    }

    private void SyncMonitorsToBackingLists()
    {
        var ordered = StageMonitors
            .OrderByDescending(m => m.IsPrimary)
            .ToList();

        _monitorPlacements = ordered.Select(m => m.ToPlacement()).ToList();
        _targetMonitorIds = ordered.Select(m => m.MonitorId).ToList();
        _targetMonitorId = _targetMonitorIds.Count > 0 ? _targetMonitorIds[0] : string.Empty;
        OnPropertyChanged(nameof(TargetMonitorId));
        OnPropertyChanged(nameof(TargetMonitorIds));
    }

    private void RebuildRankedCollections()
    {
        AudioRanking.Clear();
        for (int i = 0; i < _audioPriorityIds.Count; i++)
        {
            string id = _audioPriorityIds[i];
            var ep = _latestAudioEndpoints.FirstOrDefault(e => DeviceOptionResolver.MatchesEndpoint(e.Id, id));
            string display = ResolveAudioDisplayName(id, ep?.Name);
            AudioRanking.Add(new RankedAudioItemViewModel(
                rank: i + 1,
                endpointId: id,
                displayName: display,
                hardwareName: ep?.Name ?? id,
                state: ep?.State ?? DevicePresenceState.NotPresent,
                isDefaultActive: ep?.IsDefaultPlayback ?? false,
                moveUpAction: MoveAudioUp,
                moveDownAction: MoveAudioDown,
                removeAction: RemoveAudioFromRanking));
        }

        MicrophoneRanking.Clear();
        for (int i = 0; i < _microphonePriorityIds.Count; i++)
        {
            string id = _microphonePriorityIds[i];
            var ep = _latestAudioEndpoints.FirstOrDefault(e => DeviceOptionResolver.MatchesEndpoint(e.Id, id));
            string display = ResolveAudioDisplayName(id, ep?.Name);
            MicrophoneRanking.Add(new RankedAudioItemViewModel(
                rank: i + 1,
                endpointId: id,
                displayName: display,
                hardwareName: ep?.Name ?? id,
                state: ep?.State ?? DevicePresenceState.NotPresent,
                isDefaultActive: ep?.IsDefaultCapture ?? false,
                moveUpAction: MoveMicrophoneUp,
                moveDownAction: MoveMicrophoneDown,
                removeAction: RemoveMicrophoneFromRanking));
        }

        AddableAudioOptions.Clear();
        var allowedAudio = DeviceOptionResolver.FilterBlacklistedEndpoints(_latestAudioEndpoints, _blacklistedIds);
        foreach (var ep in allowedAudio.Where(e => e.Flow == AudioDeviceFlow.Playback && e.State != DevicePresenceState.NotPresent))
        {
            if (!_audioPriorityIds.Any(id => DeviceOptionResolver.MatchesEndpoint(id, ep.Id)))
            {
                AddableAudioOptions.Add(new DeviceSelectionOption(ep.Id, ResolveAudioDisplayName(ep.Id, ep.Name)));
            }
        }
        SelectedAudioToAddId = AddableAudioOptions.FirstOrDefault()?.Id ?? string.Empty;

        AddableMicrophoneOptions.Clear();
        foreach (var ep in allowedAudio.Where(e => e.Flow == AudioDeviceFlow.Capture && e.State != DevicePresenceState.NotPresent))
        {
            if (!_microphonePriorityIds.Any(id => DeviceOptionResolver.MatchesEndpoint(id, ep.Id)))
            {
                AddableMicrophoneOptions.Add(new DeviceSelectionOption(ep.Id, ResolveAudioDisplayName(ep.Id, ep.Name)));
            }
        }
        SelectedMicToAddId = AddableMicrophoneOptions.FirstOrDefault()?.Id ?? string.Empty;
    }

    private void SyncRankingToLegacyAudioProperties()
    {
        PrimaryAudioId = _audioPriorityIds.Count > 0 ? _audioPriorityIds[0] : string.Empty;
        SecondaryAudioId = _audioPriorityIds.Count > 1 ? _audioPriorityIds[1] : string.Empty;
        FallbackAudioId = _audioPriorityIds.Count > 2 ? _audioPriorityIds[2] : string.Empty;
    }

    private void SyncRankingToLegacyMicrophoneProperties()
    {
        PrimaryMicrophoneId = _microphonePriorityIds.Count > 0 ? _microphonePriorityIds[0] : string.Empty;
        SecondaryMicrophoneId = _microphonePriorityIds.Count > 1 ? _microphonePriorityIds[1] : string.Empty;
        FallbackMicrophoneId = _microphonePriorityIds.Count > 2 ? _microphonePriorityIds[2] : string.Empty;
    }

    private string ResolveDisplayName(string monitorId, string? fallbackHardwareName)
    {
        if (_customNames.TryGetValue(monitorId, out var custom) && !string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        if (!string.IsNullOrWhiteSpace(fallbackHardwareName))
        {
            return fallbackHardwareName;
        }

        if (_cachedNames.TryGetValue(monitorId, out var cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        return monitorId;
    }

    private string ResolveAudioDisplayName(string endpointId, string? hardwareName)
    {
        string? custom = null;
        foreach (var (k, v) in _customNames)
        {
            if (DeviceOptionResolver.MatchesEndpoint(k, endpointId) && !string.IsNullOrWhiteSpace(v))
            {
                custom = v;
                break;
            }
        }

        string baseName = hardwareName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseName))
        {
            foreach (var (k, v) in _cachedNames)
            {
                if (DeviceOptionResolver.MatchesEndpoint(k, endpointId) && !string.IsNullOrWhiteSpace(v))
                {
                    baseName = v;
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(custom) && !string.IsNullOrWhiteSpace(baseName))
        {
            return $"{custom} ({baseName})";
        }

        if (!string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        return !string.IsNullOrWhiteSpace(baseName) ? baseName : endpointId;
    }

    private void RemoveApplicationHook(ApplicationHookItemViewModel hook)
    {
        ApplicationHooks.Remove(hook);
    }

    private static string? PromptForExecutablePath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Application to Launch",
            Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>
    /// Applies the modified properties back to the target workstation preset record.
    /// </summary>
    /// <param name="target">The workstation preset record to update.</param>
    public void ApplyTo(WorkstationPreset target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Name = Name;
        target.TargetMonitorIds = [.. TargetMonitorIds];
        target.TargetMonitorId = TargetMonitorId;
        target.MonitorPlacements = _monitorPlacements.Select(p => p with { }).ToList();

        // Keep ranked lists and legacy properties synchronized
        bool legacyAudioMatchesRankTop =
            string.Equals(_audioPriorityIds.ElementAtOrDefault(0) ?? string.Empty, PrimaryAudioId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_audioPriorityIds.ElementAtOrDefault(1) ?? string.Empty, SecondaryAudioId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_audioPriorityIds.ElementAtOrDefault(2) ?? string.Empty, FallbackAudioId, StringComparison.OrdinalIgnoreCase);

        if (_audioPriorityIds.Count > 0 && legacyAudioMatchesRankTop)
        {
            target.AudioPriorityIds = [.. _audioPriorityIds];
            target.SyncLegacyAudioFieldsFromPriorityList();
        }
        else
        {
            target.PrimaryAudioId = PrimaryAudioId;
            target.SecondaryAudioId = SecondaryAudioId;
            target.FallbackAudioId = FallbackAudioId;
            target.AudioPriorityIds = [];
            target.AudioPriorityIds = [.. target.GetEffectiveAudioPriorityList()];
        }

        bool legacyMicMatchesRankTop =
            string.Equals(_microphonePriorityIds.ElementAtOrDefault(0) ?? string.Empty, PrimaryMicrophoneId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_microphonePriorityIds.ElementAtOrDefault(1) ?? string.Empty, SecondaryMicrophoneId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_microphonePriorityIds.ElementAtOrDefault(2) ?? string.Empty, FallbackMicrophoneId, StringComparison.OrdinalIgnoreCase);

        if (_microphonePriorityIds.Count > 0 && legacyMicMatchesRankTop)
        {
            target.MicrophonePriorityIds = [.. _microphonePriorityIds];
            target.SyncLegacyMicrophoneFieldsFromPriorityList();
        }
        else
        {
            target.PrimaryMicrophoneId = PrimaryMicrophoneId;
            target.SecondaryMicrophoneId = SecondaryMicrophoneId;
            target.FallbackMicrophoneId = FallbackMicrophoneId;
            target.MicrophonePriorityIds = [];
            target.MicrophonePriorityIds = [.. target.GetEffectiveMicrophonePriorityList()];
        }

        target.PlaybackVolume = new AudioVolumeSettings(PlaybackVolumeMode, PlaybackVolumePercent, PlaybackVolumeIsMuted);
        target.MicrophoneVolume = new AudioVolumeSettings(MicrophoneVolumeMode, MicrophoneVolumePercent, MicrophoneVolumeIsMuted);
        target.HdrMode = HdrMode;
        target.DirectHotkey = DirectHotkey;
        target.ApplicationHooks = ApplicationHooks.Select(h => h.ToModel()).ToList();
    }

    /// <summary>
    /// Synchronizes this view model with the specified workstation preset record.
    /// </summary>
    /// <param name="preset">The source preset to read from.</param>
    public void UpdateFrom(WorkstationPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        Name = preset.Name;
        TargetMonitorIds = [.. preset.TargetMonitorIds];
        TargetMonitorId = preset.TargetMonitorId;
        _monitorPlacements = preset.MonitorPlacements?.Select(p => p with { }).ToList() ?? [];
        _audioPriorityIds = [.. preset.GetEffectiveAudioPriorityList()];
        _microphonePriorityIds = [.. preset.GetEffectiveMicrophonePriorityList()];
        PrimaryAudioId = preset.PrimaryAudioId;
        SecondaryAudioId = preset.SecondaryAudioId ?? string.Empty;
        FallbackAudioId = preset.FallbackAudioId;
        PrimaryMicrophoneId = preset.PrimaryMicrophoneId ?? string.Empty;
        SecondaryMicrophoneId = preset.SecondaryMicrophoneId ?? string.Empty;
        FallbackMicrophoneId = preset.FallbackMicrophoneId ?? string.Empty;
        PlaybackVolumeMode = preset.PlaybackVolume?.Mode ?? PresetVolumeBehavior.Retain;
        PlaybackVolumePercent = preset.PlaybackVolume?.VolumePercent ?? 50;
        PlaybackVolumeIsMuted = preset.PlaybackVolume?.IsMuted ?? false;
        MicrophoneVolumeMode = preset.MicrophoneVolume?.Mode ?? PresetVolumeBehavior.Retain;
        MicrophoneVolumePercent = preset.MicrophoneVolume?.VolumePercent ?? 50;
        MicrophoneVolumeIsMuted = preset.MicrophoneVolume?.IsMuted ?? false;
        HdrMode = preset.HdrMode;
        DirectHotkey = preset.DirectHotkey;
        ApplicationHooks.Clear();
        if (preset.ApplicationHooks != null)
        {
            foreach (var hook in preset.ApplicationHooks)
            {
                ApplicationHooks.Add(new ApplicationHookItemViewModel(hook, RemoveApplicationHook));
            }
        }

        RebuildMonitorStageAndTray();
        RebuildRankedCollections();
    }

    private static void LaunchDisplaySettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:display")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to open Windows Display Settings: {ex.Message}");
        }
    }
}
