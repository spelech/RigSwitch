namespace RigSwitch.App.ViewModels;

using System.Windows.Input;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;

/// <summary>
/// View model representing a single monitor item on a preset's 2D spatial topology canvas or in its unassigned tray.
/// </summary>
public sealed class PresetMonitorCanvasItemViewModel : ViewModelBase
{
    private int _positionX;
    private int _positionY;
    private int _width = 1920;
    private int _height = 1080;
    private double _canvasLeft;
    private double _canvasTop;
    private double _canvasWidth = 140;
    private double _canvasHeight = 82;
    private bool _isPrimary;
    private PresetHdrMode _hdrMode = PresetHdrMode.Retain;
    private bool _isOnStage;
    private string _friendlyName;

    public PresetMonitorCanvasItemViewModel(
        int displayNumber,
        string monitorId,
        string friendlyName,
        int positionX = 0,
        int positionY = 0,
        int width = 1920,
        int height = 1080,
        bool isPrimary = false,
        PresetHdrMode hdrMode = PresetHdrMode.Retain,
        bool isOnStage = true,
        bool isConnected = true,
        Action<PresetMonitorCanvasItemViewModel>? setPrimaryAction = null,
        Action<PresetMonitorCanvasItemViewModel>? cycleHdrAction = null,
        Action<PresetMonitorCanvasItemViewModel>? removeFromStageAction = null,
        Action<PresetMonitorCanvasItemViewModel>? addToStageAction = null)
    {
        DisplayNumber = displayNumber;
        MonitorId = monitorId ?? string.Empty;
        _friendlyName = string.IsNullOrWhiteSpace(friendlyName) ? MonitorId : friendlyName;
        _positionX = positionX;
        _positionY = positionY;
        _width = width > 0 ? width : 1920;
        _height = height > 0 ? height : 1080;
        _isPrimary = isPrimary;
        _hdrMode = hdrMode;
        _isOnStage = isOnStage;
        IsConnected = isConnected;

        SetPrimaryCommand = new RelayCommand(() => setPrimaryAction?.Invoke(this));
        CycleHdrCommand = new RelayCommand(() => cycleHdrAction?.Invoke(this));
        RemoveFromStageCommand = new RelayCommand(() => removeFromStageAction?.Invoke(this));
        AddToStageCommand = new RelayCommand(() => addToStageAction?.Invoke(this));
    }

    public int DisplayNumber { get; }

    public string MonitorId { get; }

    public bool IsConnected { get; }

    public string FriendlyName
    {
        get => _friendlyName;
        set => SetProperty(ref _friendlyName, value);
    }

    public int PositionX
    {
        get => _positionX;
        set
        {
            if (SetProperty(ref _positionX, value))
            {
                OnPropertyChanged(nameof(CoordinatesBadge));
                OnPropertyChanged(nameof(ResolutionAndPosText));
            }
        }
    }

    public int PositionY
    {
        get => _positionY;
        set
        {
            if (SetProperty(ref _positionY, value))
            {
                OnPropertyChanged(nameof(CoordinatesBadge));
                OnPropertyChanged(nameof(ResolutionAndPosText));
            }
        }
    }

    public int Width
    {
        get => _width;
        set
        {
            if (SetProperty(ref _width, value > 0 ? value : 1920))
            {
                OnPropertyChanged(nameof(ResolutionText));
                OnPropertyChanged(nameof(ResolutionAndPosText));
            }
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            if (SetProperty(ref _height, value > 0 ? value : 1080))
            {
                OnPropertyChanged(nameof(ResolutionText));
                OnPropertyChanged(nameof(ResolutionAndPosText));
            }
        }
    }

    public double CanvasLeft
    {
        get => _canvasLeft;
        set => SetProperty(ref _canvasLeft, value);
    }

    public double CanvasTop
    {
        get => _canvasTop;
        set => SetProperty(ref _canvasTop, value);
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        set => SetProperty(ref _canvasWidth, value);
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        set => SetProperty(ref _canvasHeight, value);
    }

    public bool IsPrimary
    {
        get => _isPrimary;
        set
        {
            if (SetProperty(ref _isPrimary, value))
            {
                OnPropertyChanged(nameof(PrimaryBadgeText));
                OnPropertyChanged(nameof(BorderBrushHex));
                OnPropertyChanged(nameof(BackgroundHex));
            }
        }
    }

    public PresetHdrMode HdrMode
    {
        get => _hdrMode;
        set
        {
            if (SetProperty(ref _hdrMode, value))
            {
                OnPropertyChanged(nameof(HdrBadgeText));
                OnPropertyChanged(nameof(HdrBadgeBackground));
                OnPropertyChanged(nameof(HdrBadgeForeground));
            }
        }
    }

    public bool IsOnStage
    {
        get => _isOnStage;
        set => SetProperty(ref _isOnStage, value);
    }

    public string PrimaryBadgeText => IsPrimary ? "★ Primary (0,0)" : "☆ Make Primary";

    public string CoordinatesBadge => $"({PositionX}, {PositionY})";

    public string ResolutionText => $"{Width}×{Height}";

    public string ResolutionAndPosText => $"{Width}×{Height} • ({PositionX}, {PositionY})";

    public string HdrBadgeText => HdrMode switch
    {
        PresetHdrMode.Enable => "HDR: Enable",
        PresetHdrMode.Disable => "HDR: Disable",
        _ => "HDR: Retain"
    };

    public string HdrBadgeBackground => HdrMode switch
    {
        PresetHdrMode.Enable => "#2E2510",
        PresetHdrMode.Disable => "#2B161B",
        _ => "#1E2230"
    };

    public string HdrBadgeForeground => HdrMode switch
    {
        PresetHdrMode.Enable => "#FFB300",
        PresetHdrMode.Disable => "#FF5252",
        _ => "#9BA3B5"
    };

    public string BorderBrushHex => IsPrimary ? "#00E676" : "#3B82F6";

    public string BackgroundHex => IsPrimary ? "#11281E" : "#131C2E";

    public ICommand SetPrimaryCommand { get; }

    public ICommand CycleHdrCommand { get; }

    public ICommand RemoveFromStageCommand { get; }

    public ICommand AddToStageCommand { get; }

    public MonitorPlacement ToPlacement() =>
        new(MonitorId, PositionX, PositionY, Width, Height, IsPrimary, HdrMode);
}
