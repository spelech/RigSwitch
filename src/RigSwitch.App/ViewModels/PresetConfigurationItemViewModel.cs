namespace RigSwitch.App.ViewModels;

using System.Diagnostics;
using System.Windows.Input;
using RigSwitch.Core.Models;

/// <summary>
/// Wraps a <see cref="WorkstationPreset"/> for MVVM data binding, preset configuration, and hardware routing.
/// </summary>
public sealed class PresetConfigurationItemViewModel : ViewModelBase
{
    private readonly Action<PresetConfigurationItemViewModel>? _onActivated;
    private string _name;
    private string _targetMonitorId;
    private string _primaryAudioId;
    private string _fallbackAudioId;
    private string _directHotkey;
    private bool _isActive;

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
        set => SetProperty(ref _targetMonitorId, value);
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
    public PresetConfigurationItemViewModel(
        WorkstationPreset preset,
        int presetIndex,
        string groupName,
        bool isActive,
        Action<PresetConfigurationItemViewModel>? onActivated = null,
        Action? openDisplaySettingsAction = null)
    {
        ArgumentNullException.ThrowIfNull(preset);
        PresetIndex = presetIndex;
        GroupName = groupName;
        _isActive = isActive;
        _onActivated = onActivated;

        _name = preset.Name;
        _targetMonitorId = preset.TargetMonitorId;
        _primaryAudioId = preset.PrimaryAudioId;
        _fallbackAudioId = preset.FallbackAudioId;
        _directHotkey = preset.DirectHotkey;

        OpenDisplaySettingsCommand = new RelayCommand(openDisplaySettingsAction ?? LaunchDisplaySettings);
    }

    /// <summary>
    /// Applies the modified properties back to the target workstation preset record.
    /// </summary>
    /// <param name="target">The workstation preset record to update.</param>
    public void ApplyTo(WorkstationPreset target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Name = Name;
        target.TargetMonitorId = TargetMonitorId;
        target.PrimaryAudioId = PrimaryAudioId;
        target.FallbackAudioId = FallbackAudioId;
        target.DirectHotkey = DirectHotkey;
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
