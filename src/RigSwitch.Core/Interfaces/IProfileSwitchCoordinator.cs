namespace RigSwitch.Core.Interfaces;

using RigSwitch.Core.Enums;
using RigSwitch.Core.Events;
using RigSwitch.Core.Models;

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
    /// Gets the index of the currently active preset within the active profile.
    /// </summary>
    int CurrentPresetIndex { get; }

    /// <summary>
    /// Gets the currently active workstation preset.
    /// </summary>
    WorkstationPreset CurrentPreset { get; }

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
    /// Switches to the specified target workstation profile and preset asynchronously.
    /// </summary>
    /// <param name="targetProfile">The target workstation profile mode to activate.</param>
    /// <param name="presetIndex">The zero-based index of the preset to activate within the target profile.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the profile and preset transition completed successfully; otherwise, <c>false</c>.</returns>
    Task<bool> SwitchToPresetAsync(ProfileMode targetProfile, int presetIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the currently active workstation profile state without performing hardware reconfiguration.
    /// </summary>
    /// <param name="profile">The workstation profile mode to set.</param>
    void SetCurrentProfile(ProfileMode profile);

    /// <summary>
    /// Sets the currently active workstation profile and preset state without performing hardware reconfiguration.
    /// </summary>
    /// <param name="profile">The workstation profile mode to set.</param>
    /// <param name="presetIndex">The zero-based index of the preset to set.</param>
    void SetCurrentPreset(ProfileMode profile, int presetIndex);
}
