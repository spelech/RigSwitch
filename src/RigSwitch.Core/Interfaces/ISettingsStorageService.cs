namespace RigSwitch.Core.Interfaces;

using RigSwitch.Core.Models;

/// <summary>
/// Provides persistence services for RigSwitch user settings and profile configurations.
/// </summary>
public interface ISettingsStorageService
{
    /// <summary>
    /// Loads user settings from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The loaded <see cref="UserSettings"/> instance, or default settings if none exist or the file is corrupted.</returns>
    Task<UserSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves user settings to persistent storage atomically.
    /// </summary>
    /// <param name="settings">The <see cref="UserSettings"/> instance to persist.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default);
}
