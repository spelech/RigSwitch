namespace RigSwitch.Core.Interfaces;

using RigSwitch.Core.Models;

/// <summary>
/// Directs audio endpoint routing and controls endpoint visibility in the Windows audio subsystem.
/// </summary>
public interface IAudioEndpointDirector
{
    /// <summary>
    /// Enumerates all audio playback endpoints and their current presence and configuration states.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A read-only list of <see cref="AudioEndpointInfo"/> representing the detected audio endpoints.</returns>
    Task<IReadOnlyList<AudioEndpointInfo>> EnumerateAudioEndpointsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the default audio playback endpoint for both multimedia and communications roles.
    /// </summary>
    /// <param name="endpointId">The MMDevice GUID string identifier of the target audio endpoint.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetDefaultPlaybackEndpointAsync(string endpointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the visibility (enabled/disabled state) of a specific audio endpoint.
    /// </summary>
    /// <param name="endpointId">The MMDevice GUID string identifier of the audio endpoint.</param>
    /// <param name="isVisible">Whether the endpoint should be visible/enabled (<c>true</c>) or hidden/disabled (<c>false</c>).</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetEndpointVisibilityAsync(string endpointId, bool isVisible, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes the visibility of audio endpoints against a collection of endpoint GUIDs that should be hidden.
    /// </summary>
    /// <param name="hiddenEndpointIds">The collection of MMDevice GUID string identifiers that should be hidden/disabled.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SyncHiddenEndpointsAsync(IEnumerable<string> hiddenEndpointIds, CancellationToken cancellationToken = default);
}
