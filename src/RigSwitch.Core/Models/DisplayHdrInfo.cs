namespace RigSwitch.Core.Models;

using RigSwitch.Core.Enums;

/// <summary>
/// Represents native Windows HDR and Advanced Color status for a display monitor.
/// </summary>
/// <param name="MonitorId">The hardware/EDID monitor identifier.</param>
/// <param name="SupportsHdr">Whether the display hardware and driver support HDR / Advanced Color.</param>
/// <param name="IsHdrEnabled">Whether HDR / Advanced Color is currently enabled on the display.</param>
/// <param name="WideColorEnforced">Whether wide color gamut is currently enforced.</param>
/// <param name="ColorEncoding">The active color encoding format (RGB, YCbCr444, etc.).</param>
/// <param name="BitsPerColorChannel">The active bits per color channel (e.g. 8, 10, 12).</param>
public sealed record DisplayHdrInfo(
    string MonitorId,
    bool SupportsHdr,
    bool IsHdrEnabled,
    bool WideColorEnforced,
    DisplayColorEncoding ColorEncoding,
    int BitsPerColorChannel);
