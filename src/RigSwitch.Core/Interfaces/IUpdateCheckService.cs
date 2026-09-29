namespace RigSwitch.Core.Interfaces;

using RigSwitch.Core.Models;

/// <summary>
/// Service interface for querying remote release metadata to check for application updates.
/// </summary>
public interface IUpdateCheckService
{
    /// <summary>
    /// Checks remote release endpoints for newer versions relative to the specified version.
    /// </summary>
    /// <param name="currentVersion">The current running version string (e.g. "1.0.0").</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="UpdateInfo"/> record detailing update availability.</returns>
    Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default);
}
