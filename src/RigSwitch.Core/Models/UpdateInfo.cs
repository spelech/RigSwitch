namespace RigSwitch.Core.Models;

/// <summary>
/// Represents the result of an application update availability check.
/// </summary>
/// <param name="IsUpdateAvailable">Indicates whether a newer release is available.</param>
/// <param name="CurrentVersion">The currently executing application version string.</param>
/// <param name="LatestVersion">The latest release version string detected on remote server.</param>
/// <param name="ReleaseNotes">Release notes or changelog summary for the latest version.</param>
/// <param name="DownloadUrl">Direct URL to download the installer asset or package.</param>
/// <param name="HtmlUrl">Web address of the release landing page.</param>
/// <param name="ErrorMessage">Optional error or diagnostic message if the check failed.</param>
public sealed record UpdateInfo(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseNotes,
    string DownloadUrl,
    string HtmlUrl,
    string ErrorMessage = "");
