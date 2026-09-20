namespace RigSwitch.Core.Interfaces;

using RigSwitch.Core.Enums;
using RigSwitch.Core.Events;

/// <summary>
/// Coordinates atomic workstation profile transitions across display, audio, and configuration subsystems.
/// </summary>
public interface IProfileSwitchCoordinator
{
    /// <summary>
    /// Gets the currently active workstation profile mode.
    /// </summary>
    ProfileMode CurrentProfile { get; }

    /// <summary>
    /// Occurs when a profile switch operation finishes, indicating success or failure.
    /// </summary>
    event EventHandler<ProfileChangedEventArgs>? ProfileChanged;

    /// <summary>
    /// Switches to the specified target workstation profile asynchronously.
    /// </summary>
    /// <param name="targetProfile">The target workstation profile mode to activate.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the profile transition completed successfully; otherwise, <c>false</c>.</returns>
    Task<bool> SwitchProfileAsync(ProfileMode targetProfile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the currently active workstation profile state without performing hardware reconfiguration.
    /// </summary>
    /// <param name="profile">The workstation profile mode to set.</param>
    void SetCurrentProfile(ProfileMode profile);
}
