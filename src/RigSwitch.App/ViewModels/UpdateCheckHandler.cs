namespace RigSwitch.App.ViewModels;

using System.Diagnostics;
using System.Reflection;
using System.Windows;
using RigSwitch.App.Views;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Handles checking for updates and processing user selections from the update notification UI.
/// </summary>
public static class UpdateCheckHandler
{
    /// <summary>
    /// Executes the update check workflow and prompts the user if a new version is detected.
    /// Safely dispatches UI dialog creation to the main WPF STA dispatcher thread.
    /// </summary>
    public static async Task<string> CheckForUpdatesAsync(
        IUpdateCheckService updateCheckService,
        UserSettings settings,
        ISettingsStorageService settingsStorage,
        bool isManual,
        Action<string> onIgnoredVersionChanged)
    {
        ArgumentNullException.ThrowIfNull(updateCheckService);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingsStorage);

        var currentVer = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        var updateInfo = await updateCheckService.CheckForUpdatesAsync(currentVer).ConfigureAwait(true);

        if (!updateInfo.IsUpdateAvailable)
        {
            return isManual
                ? (string.IsNullOrWhiteSpace(updateInfo.ErrorMessage)
                    ? $"You are running the latest version of RigSwitch (v{updateInfo.CurrentVersion})."
                    : $"Update check failed: {updateInfo.ErrorMessage}")
                : string.Empty;
        }

        if (!isManual && string.Equals(updateInfo.LatestVersion, settings.IgnoredReleaseVersion, StringComparison.OrdinalIgnoreCase))
        {
            return $"Update v{updateInfo.LatestVersion} is available (ignored).";
        }

        UpdateUserChoice choice = UpdateUserChoice.Cancel;
        bool? dialogResult = false;

        void ShowDialogOnUiThread()
        {
            var window = new UpdateNotificationWindow(updateInfo);
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null && mainWindow.IsVisible)
            {
                window.Owner = mainWindow;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialogResult = window.ShowDialog();
            choice = window.Choice;
        }

        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            await Application.Current.Dispatcher.InvokeAsync(ShowDialogOnUiThread);
        }
        else
        {
            ShowDialogOnUiThread();
        }

        if (dialogResult == true)
        {
            return await HandleUserChoiceAsync(choice, updateInfo, settings, settingsStorage, onIgnoredVersionChanged).ConfigureAwait(true);
        }

        return $"New update available: v{updateInfo.LatestVersion}!";
    }

    /// <summary>
    /// Applies the user's choice (Download, Delay, Ignore) to settings or system browser.
    /// </summary>
    public static async Task<string> HandleUserChoiceAsync(
        UpdateUserChoice choice,
        UpdateInfo updateInfo,
        UserSettings settings,
        ISettingsStorageService settingsStorage,
        Action<string> onIgnoredVersionChanged)
    {
        ArgumentNullException.ThrowIfNull(updateInfo);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingsStorage);

        switch (choice)
        {
            case UpdateUserChoice.Download:
                string targetUrl = !string.IsNullOrWhiteSpace(updateInfo.DownloadUrl) ? updateInfo.DownloadUrl : updateInfo.HtmlUrl;
                if (!string.IsNullOrWhiteSpace(targetUrl))
                {
                    if (Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
                            return "Opened release download page in browser.";
                        }
                        catch (Exception ex)
                        {
                            return $"Failed to open browser: {ex.Message}";
                        }
                    }

                    return "Invalid download URL provided.";
                }
                break;

            case UpdateUserChoice.Delay:
                settings.UpdateCheckSkippedUntil = DateTime.UtcNow.AddDays(1);
                await settingsStorage.SaveSettingsAsync(settings).ConfigureAwait(true);
                return "Update notification delayed for 24 hours.";

            case UpdateUserChoice.Ignore:
                settings.IgnoredReleaseVersion = updateInfo.LatestVersion;
                onIgnoredVersionChanged?.Invoke(updateInfo.LatestVersion);
                await settingsStorage.SaveSettingsAsync(settings).ConfigureAwait(true);
                return $"Version v{updateInfo.LatestVersion} added to ignored releases.";
        }

        return string.Empty;
    }
}
