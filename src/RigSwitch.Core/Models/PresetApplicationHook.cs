namespace RigSwitch.Core.Models;

/// <summary>
/// Represents an application lifecycle hook that is launched or terminated on workstation preset transitions.
/// </summary>
public sealed record PresetApplicationHook
{
    /// <summary>
    /// Gets the unique identifier for this lifecycle hook.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the absolute path to the executable file.
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional command line arguments to pass to the executable.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the application should be closed when switching away from this preset.
    /// </summary>
    public bool CloseOnSwitchAway { get; set; } = true;
}
