namespace RigSwitch.Core.Ipc;

/// <summary>
/// Constants for supported IPC command identifiers.
/// </summary>
public static class IpcCommands
{
    /// <summary>
    /// Retrieves current workstation status and configuration.
    /// </summary>
    public const string Status = "status";

    /// <summary>
    /// Switches workstation profile or preset.
    /// </summary>
    public const string Switch = "switch";

    /// <summary>
    /// Queries or modifies audio endpoint routing.
    /// </summary>
    public const string Audio = "audio";

    /// <summary>
    /// Queries or modifies device blacklists.
    /// </summary>
    public const string Blacklist = "blacklist";

    /// <summary>
    /// Captures UI screenshots.
    /// </summary>
    public const string Screenshot = "screenshot";

    /// <summary>
    /// Runs diagnostic self-checks on system configuration.
    /// </summary>
    public const string Doctor = "doctor";
}
