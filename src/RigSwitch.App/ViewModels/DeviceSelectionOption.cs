namespace RigSwitch.App.ViewModels;

/// <summary>
/// Represents a selectable display or audio device option in settings dropdowns.
/// </summary>
/// <param name="Id">The hardware monitor ID or audio endpoint ID.</param>
/// <param name="DisplayName">The user-friendly display text for the option.</param>
public sealed record DeviceSelectionOption(string Id, string DisplayName);
