namespace RigSwitch.App.ViewModels;

using System.Windows.Input;
using RigSwitch.Core.Enums;

/// <summary>
/// View model representing a single ranked playback or capture audio endpoint in a preset's priority list.
/// </summary>
public sealed class RankedAudioItemViewModel : ViewModelBase
{
    private int _rank;
    private string _displayName;
    private DevicePresenceState _state;
    private bool _isDefaultActive;

    public RankedAudioItemViewModel(
        int rank,
        string endpointId,
        string displayName,
        string hardwareName,
        DevicePresenceState state = DevicePresenceState.Active,
        bool isDefaultActive = false,
        Action<RankedAudioItemViewModel>? moveUpAction = null,
        Action<RankedAudioItemViewModel>? moveDownAction = null,
        Action<RankedAudioItemViewModel>? removeAction = null)
    {
        _rank = rank;
        EndpointId = endpointId ?? string.Empty;
        _displayName = string.IsNullOrWhiteSpace(displayName) ? EndpointId : displayName;
        HardwareName = hardwareName ?? string.Empty;
        _state = state;
        _isDefaultActive = isDefaultActive;

        MoveUpCommand = new RelayCommand(() => moveUpAction?.Invoke(this));
        MoveDownCommand = new RelayCommand(() => moveDownAction?.Invoke(this));
        RemoveCommand = new RelayCommand(() => removeAction?.Invoke(this));
    }

    public string EndpointId { get; }

    public string HardwareName { get; }

    public int Rank
    {
        get => _rank;
        set
        {
            if (SetProperty(ref _rank, value))
            {
                OnPropertyChanged(nameof(RankBadgeText));
            }
        }
    }

    public string RankBadgeText => $"#{Rank}";

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public DevicePresenceState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                NotifyStatusChanged();
            }
        }
    }

    public bool IsDefaultActive
    {
        get => _isDefaultActive;
        set
        {
            if (SetProperty(ref _isDefaultActive, value))
            {
                NotifyStatusChanged();
            }
        }
    }

    public string StatusText => State == DevicePresenceState.Active
        ? (IsDefaultActive ? "● Active Now" : "● Ready")
        : "● Offline";

    public string StatusBackground => State == DevicePresenceState.Active
        ? (IsDefaultActive ? "#0D2818" : "#132238")
        : "#231B24";

    public string StatusForeground => State == DevicePresenceState.Active
        ? (IsDefaultActive ? "#00E676" : "#60A5FA")
        : "#9BA3B5";

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    public ICommand RemoveCommand { get; }

    private void NotifyStatusChanged()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusForeground));
    }
}
