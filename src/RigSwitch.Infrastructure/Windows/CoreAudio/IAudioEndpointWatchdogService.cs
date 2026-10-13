namespace RigSwitch.Infrastructure.Windows.CoreAudio;

/// <summary>
/// Provides background watchdog monitoring of Windows CoreAudio default playback endpoints,
/// enforcing primary speaker routing and immediately reverting unauthorized shifts to blacklisted devices.
/// </summary>
public interface IAudioEndpointWatchdogService : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the watchdog service is actively running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts monitoring default audio endpoint changes.
    /// </summary>
    void Start();

#pragma warning disable CA1716 // Identifiers should not match keywords

    /// <summary>
    /// Stops monitoring default audio endpoint changes and cancels any pending debounce operations.
    /// </summary>
    void Stop();

#pragma warning restore CA1716

    /// <summary>
    /// Notifies the watchdog of an intentional endpoint switch so it does not revert the change.
    /// </summary>
    /// <param name="endpointId">The endpoint device identifier being intentionally switched to.</param>
    void NotifyIntentionalSwitch(string endpointId);
}
