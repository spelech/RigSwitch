namespace RigSwitch.Core.Events;

using RigSwitch.Core.Enums;

/// <summary>
/// Provides event data for profile change completion events.
/// </summary>
public sealed class ProfileChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileChangedEventArgs"/> class.
    /// </summary>
    /// <param name="previousProfile">The workstation profile active prior to the transition.</param>
    /// <param name="newProfile">The target workstation profile requested.</param>
    /// <param name="success">Whether the transition completed successfully.</param>
    /// <param name="errorMessage">An optional error message if the transition failed.</param>
    public ProfileChangedEventArgs(
        ProfileMode previousProfile,
        ProfileMode newProfile,
        bool success = true,
        string? errorMessage = null)
    {
        PreviousProfile = previousProfile;
        NewProfile = newProfile;
        Success = success;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets the workstation profile that was active prior to the switch.
    /// </summary>
    public ProfileMode PreviousProfile { get; }

    /// <summary>
    /// Gets the new or attempted workstation profile.
    /// </summary>
    public ProfileMode NewProfile { get; }

    /// <summary>
    /// Gets a value indicating whether the profile switch succeeded.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets an optional error message explaining why the profile switch failed, or <c>null</c> if it succeeded.
    /// </summary>
    public string? ErrorMessage { get; }
}
