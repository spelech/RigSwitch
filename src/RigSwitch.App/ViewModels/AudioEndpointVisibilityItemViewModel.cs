namespace RigSwitch.App.ViewModels;

/// <summary>
/// View model representing an audio endpoint with a real-time visibility toggle.
/// </summary>
public sealed class AudioEndpointVisibilityItemViewModel : ViewModelBase
{
    private bool _isVisible;
    private readonly Func<AudioEndpointVisibilityItemViewModel, bool, Task>? _onVisibilityChanged;
    private bool _isBlacklisted;
    private readonly Func<AudioEndpointVisibilityItemViewModel, bool, Task>? _onBlacklistChanged;

    /// <summary>
    /// Gets the unique MMDevice GUID identifier of the audio endpoint.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the friendly product name of the audio endpoint.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the driver or adapter name of the audio endpoint.
    /// </summary>
    public string Adapter { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the audio endpoint is visible and enabled in Windows.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                if (_onVisibilityChanged != null)
                {
                    _ = _onVisibilityChanged(this, value);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the audio endpoint is blacklisted from default profile routing (e.g. bass shaker DAC).
    /// </summary>
    public bool IsBlacklisted
    {
        get => _isBlacklisted;
        set
        {
            if (SetProperty(ref _isBlacklisted, value))
            {
                OnPropertyChanged(nameof(StateLabel));
                OnPropertyChanged(nameof(StateBadgeBackground));
                OnPropertyChanged(nameof(StateBadgeForeground));
                OnPropertyChanged(nameof(StateBadgeBorder));
                if (_onBlacklistChanged != null)
                {
                    _ = _onBlacklistChanged(this, value);
                }
            }
        }
    }

    /// <summary>
    /// Reverts the visibility state without re-triggering the visibility changed callback.
    /// </summary>
    /// <param name="previousState">The previous visibility state to restore.</param>
    public void RevertVisibility(bool previousState)
    {
        _isVisible = previousState;
        OnPropertyChanged(nameof(IsVisible));
    }

    /// <summary>
    /// Reverts the blacklisted state without re-triggering the blacklist changed callback.
    /// </summary>
    /// <param name="previousState">The previous blacklisted state to restore.</param>
    public void RevertBlacklisted(bool previousState)
    {
        _isBlacklisted = previousState;
        OnPropertyChanged(nameof(IsBlacklisted));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StateBadgeBackground));
        OnPropertyChanged(nameof(StateBadgeForeground));
        OnPropertyChanged(nameof(StateBadgeBorder));
    }

    /// <summary>
    /// Gets the hardware presence or connection state of the audio endpoint.
    /// </summary>
    public RigSwitch.Core.Enums.DevicePresenceState State { get; }

    /// <summary>
    /// Gets a human-readable display string for the device presence state.
    /// </summary>
    public string StateLabel => IsBlacklisted ? "Blacklisted / Bass Shaker" : State switch
    {
        RigSwitch.Core.Enums.DevicePresenceState.Active => "Active",
        RigSwitch.Core.Enums.DevicePresenceState.Disabled => "Disabled",
        RigSwitch.Core.Enums.DevicePresenceState.Unplugged => "Unplugged",
        RigSwitch.Core.Enums.DevicePresenceState.NotPresent => "Not Present",
        _ => State.ToString()
    };

    /// <summary>
    /// Gets the badge background hex color string.
    /// </summary>
    public string StateBadgeBackground => IsBlacklisted ? "#2A0E14" : State switch
    {
        RigSwitch.Core.Enums.DevicePresenceState.Active => "#0D2818",
        RigSwitch.Core.Enums.DevicePresenceState.Disabled => "#332200",
        _ => "#1A1A24"
    };

    /// <summary>
    /// Gets the badge foreground hex color string.
    /// </summary>
    public string StateBadgeForeground => IsBlacklisted ? "#FF5252" : State switch
    {
        RigSwitch.Core.Enums.DevicePresenceState.Active => "#00E676",
        RigSwitch.Core.Enums.DevicePresenceState.Disabled => "#FFB300",
        _ => "#8E95A5"
    };

    /// <summary>
    /// Gets the badge border hex color string.
    /// </summary>
    public string StateBadgeBorder => IsBlacklisted ? "#D32F2F" : State switch
    {
        RigSwitch.Core.Enums.DevicePresenceState.Active => "#00C853",
        RigSwitch.Core.Enums.DevicePresenceState.Disabled => "#FF9800",
        _ => "#3A3A4E"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioEndpointVisibilityItemViewModel"/> class.
    /// </summary>
    /// <param name="id">The MMDevice endpoint GUID identifier.</param>
    /// <param name="name">The friendly name of the endpoint.</param>
    /// <param name="adapter">The adapter description.</param>
    /// <param name="isVisible">Initial visibility state.</param>
    /// <param name="onVisibilityChanged">Callback invoked when visibility toggles.</param>
    /// <param name="state">The device presence state (defaulting to Active).</param>
    /// <param name="isBlacklisted">Whether the endpoint is excluded from default routing.</param>
    /// <param name="onBlacklistChanged">Callback invoked when blacklist state toggles.</param>
    public AudioEndpointVisibilityItemViewModel(
        string id,
        string name,
        string adapter,
        bool isVisible,
        Func<AudioEndpointVisibilityItemViewModel, bool, Task>? onVisibilityChanged = null,
        RigSwitch.Core.Enums.DevicePresenceState state = RigSwitch.Core.Enums.DevicePresenceState.Active,
        bool isBlacklisted = false,
        Func<AudioEndpointVisibilityItemViewModel, bool, Task>? onBlacklistChanged = null)
    {
        Id = id;
        Name = name;
        Adapter = adapter;
        _isVisible = isVisible;
        _onVisibilityChanged = onVisibilityChanged;
        State = state;
        _isBlacklisted = isBlacklisted;
        _onBlacklistChanged = onBlacklistChanged;
    }
}
