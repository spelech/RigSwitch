namespace RigSwitch.Core.Interfaces;

using RigSwitch.Core.Models;

/// <summary>
/// Provides display enumeration and topology configuration operations via Windows CCD.
/// </summary>
public interface IDisplayConfigurationService
{
    /// <summary>
    /// Enumerates all connected and configured display devices on the system.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A read-only list of <see cref="DisplayDeviceInfo"/> representing the detected display devices.</returns>
    Task<IReadOnlyList<DisplayDeviceInfo>> EnumerateDisplaysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a display topology, activating the specified target displays (the first as primary) and disabling inactive displays.
    /// </summary>
    /// <param name="targetMonitorIds">The list of hardware/EDID identifiers of monitors to activate.</param>
    /// <param name="inactiveMonitorIds">The optional list of hardware/EDID identifiers of monitors to disable.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApplyDisplayTopologyAsync(IReadOnlyList<string> targetMonitorIds, IReadOnlyList<string>? inactiveMonitorIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a single-display topology, activating the specified target display as primary and disabling the inactive display.
    /// </summary>
    /// <param name="targetMonitorId">The hardware/EDID identifier of the monitor to activate and make primary.</param>
    /// <param name="inactiveMonitorId">The optional hardware/EDID identifier of the monitor to disable.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApplySingleDisplayTopologyAsync(string targetMonitorId, string? inactiveMonitorId, CancellationToken cancellationToken = default)
    {
        var targets = string.IsNullOrWhiteSpace(targetMonitorId) ? Array.Empty<string>() : new[] { targetMonitorId };
        var inactives = string.IsNullOrWhiteSpace(inactiveMonitorId) ? null : new[] { inactiveMonitorId };
        return ApplyDisplayTopologyAsync(targets, inactives, cancellationToken);
    }
}
