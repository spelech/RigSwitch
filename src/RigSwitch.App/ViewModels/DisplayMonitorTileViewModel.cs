namespace RigSwitch.App.ViewModels;

using System.Windows.Input;
using RigSwitch.Core.Models;

/// <summary>
/// Specifies the workstation profile pile assignment for a monitor tile.
/// </summary>
public enum MonitorPileAssignment
{
    Unassigned,
    Desk,
    SimRig
}

/// <summary>
/// Represents a numbered monitor tile for Windows-like drag-and-drop profile configuration.
/// </summary>
public sealed class DisplayMonitorTileViewModel : ViewModelBase
{
    private int _displayNumber;
    private MonitorPileAssignment _pile;
    private bool _isPrimary;
    private bool _isActive;
    private readonly bool _supportsHdr;
    private readonly bool _isHdrEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayMonitorTileViewModel"/> class.
    /// </summary>
    public DisplayMonitorTileViewModel(
        int displayNumber,
        string monitorId,
        string? friendlyName,
        MonitorPileAssignment pile,
        bool isPrimary = false,
        bool isActive = true,
        bool supportsHdr = false,
        bool isHdrEnabled = false,
        Action<DisplayMonitorTileViewModel, MonitorPileAssignment>? movePileAction = null)
    {
        _displayNumber = displayNumber;
        MonitorId = monitorId ?? string.Empty;
        FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? (monitorId ?? string.Empty) : friendlyName;
        _pile = pile;
        _isPrimary = isPrimary;
        _isActive = isActive;
        _supportsHdr = supportsHdr;
        _isHdrEnabled = isHdrEnabled;

        MoveToUnassignedCommand = new RelayCommand(() => movePileAction?.Invoke(this, MonitorPileAssignment.Unassigned));
        MoveToDeskCommand = new RelayCommand(() => movePileAction?.Invoke(this, MonitorPileAssignment.Desk));
        MoveToRigCommand = new RelayCommand(() => movePileAction?.Invoke(this, MonitorPileAssignment.SimRig));
    }

    /// <summary>
    /// Gets or sets the 1-based Windows display number.
    /// </summary>
    public int DisplayNumber
    {
        get => _displayNumber;
        set => SetProperty(ref _displayNumber, value);
    }

    /// <summary>
    /// Gets the EDID or hardware monitor identifier.
    /// </summary>
    public string MonitorId { get; }

    /// <summary>
    /// Gets the human-readable monitor friendly name.
    /// </summary>
    public string FriendlyName { get; }

    /// <summary>
    /// Gets or sets the target pile assignment.
    /// </summary>
    public MonitorPileAssignment Pile
    {
        get => _pile;
        set
        {
            if (SetProperty(ref _pile, value))
            {
                OnPropertyChanged(nameof(IsUnassigned));
                OnPropertyChanged(nameof(IsDesk));
                OnPropertyChanged(nameof(IsRig));
                OnPropertyChanged(nameof(PileBadgeText));
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this display is designated primary.
    /// </summary>
    public bool IsPrimary
    {
        get => _isPrimary;
        set => SetProperty(ref _isPrimary, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this display is currently active in the Windows display topology.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsUnassigned => Pile == MonitorPileAssignment.Unassigned;
    public bool IsDesk => Pile == MonitorPileAssignment.Desk;
    public bool IsRig => Pile == MonitorPileAssignment.SimRig;

    /// <summary>
    /// Gets a value indicating whether this display hardware supports HDR.
    /// </summary>
    public bool SupportsHdr => _supportsHdr;

    /// <summary>
    /// Gets a value indicating whether HDR is currently enabled on this display.
    /// </summary>
    public bool IsHdrEnabled => _isHdrEnabled;

    /// <summary>
    /// Gets the badge text describing HDR status.
    /// </summary>
    public string HdrBadgeText => IsHdrEnabled ? "HDR ON" : (SupportsHdr ? "HDR" : string.Empty);

    /// <summary>
    /// Gets a value indicating whether this tile has HDR capabilities to badge.
    /// </summary>
    public bool HasHdrBadge => SupportsHdr;

    public string PileBadgeText => Pile switch
    {
        MonitorPileAssignment.Desk => "🖥️ Desk",
        MonitorPileAssignment.SimRig => "🏎️ Sim Rig",
        _ => "Unassigned"
    };

    public ICommand MoveToUnassignedCommand { get; }
    public ICommand MoveToDeskCommand { get; }
    public ICommand MoveToRigCommand { get; }
}
