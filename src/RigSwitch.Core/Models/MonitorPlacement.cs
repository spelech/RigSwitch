namespace RigSwitch.Core.Models;

using RigSwitch.Core.Enums;

/// <summary>
/// Represents the spatial 2D coordinate placement, resolution, primary flag, and HDR mode for a monitor within a preset.
/// </summary>
public sealed record MonitorPlacement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MonitorPlacement"/> record.
    /// </summary>
    public MonitorPlacement()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MonitorPlacement"/> record with specified placement details.
    /// </summary>
    public MonitorPlacement(
        string monitorId,
        int positionX = 0,
        int positionY = 0,
        int width = 1920,
        int height = 1080,
        bool isPrimary = false,
        PresetHdrMode hdrMode = PresetHdrMode.Retain)
    {
        MonitorId = monitorId ?? string.Empty;
        PositionX = positionX;
        PositionY = positionY;
        Width = width > 0 ? width : 1920;
        Height = height > 0 ? height : 1080;
        IsPrimary = isPrimary;
        HdrMode = hdrMode;
    }

    /// <summary>
    /// Gets or sets the EDID or hardware monitor identifier.
    /// </summary>
    public string MonitorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the horizontal desktop coordinate in pixels.
    /// </summary>
    public int PositionX { get; set; }

    /// <summary>
    /// Gets or sets the vertical desktop coordinate in pixels.
    /// </summary>
    public int PositionY { get; set; }

    /// <summary>
    /// Gets or sets the horizontal resolution width in pixels.
    /// </summary>
    public int Width { get; set; } = 1920;

    /// <summary>
    /// Gets or sets the vertical resolution height in pixels.
    /// </summary>
    public int Height { get; set; } = 1080;

    /// <summary>
    /// Gets or sets a value indicating whether this monitor is designated as the primary display at (0,0).
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Gets or sets the HDR mode configured for this monitor in the preset.
    /// </summary>
    public PresetHdrMode HdrMode { get; set; } = PresetHdrMode.Retain;
}
