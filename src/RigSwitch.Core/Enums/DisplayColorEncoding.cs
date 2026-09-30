namespace RigSwitch.Core.Enums;

/// <summary>
/// Specifies the color encoding format reported by the Windows CCD API.
/// </summary>
public enum DisplayColorEncoding
{
    /// <summary>
    /// Color encoding is unspecified or standard RGB.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Standard RGB color encoding.
    /// </summary>
    Rgb = 1,

    /// <summary>
    /// YCbCr 4:4:4 color encoding.
    /// </summary>
    Ycbcr444 = 2,

    /// <summary>
    /// YCbCr 4:2:2 color encoding.
    /// </summary>
    Ycbcr422 = 3,

    /// <summary>
    /// YCbCr 4:2:0 color encoding.
    /// </summary>
    Ycbcr420 = 4,

    /// <summary>
    /// Intensity color encoding.
    /// </summary>
    Intensity = 5,
}
