namespace RigSwitch.App.ViewModels;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Win32;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;

/// <summary>
/// Wraps a <see cref="WorkstationPreset"/> for MVVM data binding, preset configuration, and hardware routing.
/// </summary>
public sealed class PresetConfigurationItemViewModel : ViewModelBase
{
    private readonly Action<PresetConfigurationItemViewModel>? _onActivated;
    private string _name;
    private string _targetMonitorId;
    private List<string> _targetMonitorIds = [];
    private string _primaryAudioId;
    private string _fallbackAudioId;
    private string _primaryMicrophoneId = string.Empty;
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

    /// <summary>
    /// Gets the application lifecycle hooks configured for this preset.
    /// </summary>
    public ObservableCollection<ApplicationHookItemViewModel> ApplicationHooks { get; } = [];

    /// <summary>
    /// Gets the command to add a new application lifecycle hook using a file dialog.
    /// </summary>
    public ICommand AddApplicationHookCommand { get; }

    /// <summary>
    /// Gets the zero-based index of this preset within its profile mode.
    /// </summary>
    public int PresetIndex { get; }

    /// <summary>
    /// Gets the mutually exclusive radio button group name.
    /// </summary>
    public string GroupName { get; }

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
            }
        }
    }

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

    /// <summary>
    /// Initializes a new instance of the <see cref="PresetConfigurationItemViewModel"/> class.
    /// </summary>
    /// <param name="preset">The workstation preset record to bind.</param>
    /// <param name="presetIndex">The zero-based index of this preset.</param>
    /// <param name="groupName">The radio button group name for mutually exclusive selection.</param>
    /// <param name="isActive">Whether this preset is initially active.</param>
    /// <param name="onActivated">Optional callback invoked when this preset becomes active.</param>
    /// <param name="openDisplaySettingsAction">Optional custom action to invoke for display settings command.</param>
    /// <param name="selectFileAction">Optional custom action to select executable file for testing.</param>
    public PresetConfigurationItemViewModel(
        WorkstationPreset preset,
        int presetIndex,
        string groupName,
        bool isActive,
        Action<PresetConfigurationItemViewModel>? onActivated = null,
        Action? openDisplaySettingsAction = null,
        Func<string?>? selectFileAction = null)
    {
        ArgumentNullException.ThrowIfNull(preset);
        PresetIndex = presetIndex;
        GroupName = groupName;
        _isActive = isActive;
        _onActivated = onActivated;

        _name = preset.Name;
        _targetMonitorIds = [.. preset.TargetMonitorIds];
        _targetMonitorId = preset.TargetMonitorId;
        _primaryAudioId = preset.PrimaryAudioId;
        _fallbackAudioId = preset.FallbackAudioId;
        _primaryMicrophoneId = preset.PrimaryMicrophoneId ?? string.Empty;
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
        target.PrimaryAudioId = PrimaryAudioId;
        target.FallbackAudioId = FallbackAudioId;
        target.PrimaryMicrophoneId = PrimaryMicrophoneId;
        target.FallbackMicrophoneId = FallbackMicrophoneId;
        target.PlaybackVolume = new AudioVolumeSettings(PlaybackVolumeMode, PlaybackVolumePercent, PlaybackVolumeIsMuted);
        target.MicrophoneVolume = new AudioVolumeSettings(MicrophoneVolumeMode, MicrophoneVolumePercent, MicrophoneVolumeIsMuted);
        target.HdrMode = HdrMode;
        target.DirectHotkey = DirectHotkey;
        target.ApplicationHooks = ApplicationHooks.Select(h => h.ToModel()).ToList();
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
