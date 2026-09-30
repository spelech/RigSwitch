namespace RigSwitch.App.Views;

using System.Windows;
using RigSwitch.Core.Models;

/// <summary>
/// Specifies the user's action choice in response to an update notification.
/// </summary>
public enum UpdateUserChoice
{
    /// <summary>
    /// Cancel or dismiss the notification dialog without persistent action.
    /// </summary>
    Cancel,

    /// <summary>
    /// Open the update release or download URL in the web browser.
    /// </summary>
    Download,

    /// <summary>
    /// Delay update prompts for 24 hours.
    /// </summary>
    Delay,

    /// <summary>
    /// Permanently ignore this specific release version.
    /// </summary>
    Ignore
}

/// <summary>
/// Interaction logic for <see cref="UpdateNotificationWindow.xaml"/>.
/// </summary>
public partial class UpdateNotificationWindow : Window
{
    /// <summary>
    /// Gets the action choice selected by the user.
    /// </summary>
    public UpdateUserChoice Choice { get; private set; } = UpdateUserChoice.Cancel;

    /// <summary>
    /// Gets the update details record for this notification.
    /// </summary>
    public UpdateInfo UpdateInfo { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNotificationWindow"/> class.
    /// </summary>
    /// <param name="updateInfo">The update details to present to the user.</param>
    public UpdateNotificationWindow(UpdateInfo updateInfo)
    {
        ArgumentNullException.ThrowIfNull(updateInfo);
        InitializeComponent();

        UpdateInfo = updateInfo;
        VersionText.Text = $"Installed: v{updateInfo.CurrentVersion} ➔ Latest: v{updateInfo.LatestVersion}";
        ReleaseNotesTextBox.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
            ? "No detailed release notes provided for this release."
            : updateInfo.ReleaseNotes;
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        Choice = UpdateUserChoice.Download;
        DialogResult = true;
        Close();
    }

    private void OnDelayClick(object sender, RoutedEventArgs e)
    {
        Choice = UpdateUserChoice.Delay;
        DialogResult = true;
        Close();
    }

    private void OnIgnoreClick(object sender, RoutedEventArgs e)
    {
        Choice = UpdateUserChoice.Ignore;
        DialogResult = true;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Choice = UpdateUserChoice.Cancel;
        DialogResult = false;
        Close();
    }
}
