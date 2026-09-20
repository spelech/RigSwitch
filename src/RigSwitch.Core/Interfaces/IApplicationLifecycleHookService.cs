namespace RigSwitch.Core.Interfaces;

using RigSwitch.Core.Models;

/// <summary>
/// Defines lifecycle management services for executing and closing preset application hooks.
/// </summary>
public interface IApplicationLifecycleHookService : IDisposable
{
    /// <summary>
    /// Launches all application hooks configured for the specified preset.
    /// </summary>
    /// <param name="preset">The workstation preset whose hooks should be launched.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous launch operation.</returns>
    Task LaunchHooksForPresetAsync(WorkstationPreset preset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes all application hooks configured for the specified preset that are flagged for closure on switch-away.
    /// </summary>
    /// <param name="preset">The workstation preset whose hooks should be closed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous closure operation.</returns>
    Task CloseHooksForPresetAsync(WorkstationPreset preset, CancellationToken cancellationToken = default);
}
