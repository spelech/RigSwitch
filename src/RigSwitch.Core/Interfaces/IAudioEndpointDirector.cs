namespace RigSwitch.Core.Interfaces;

using RigSwitch.Core.Models;

/// <summary>
/// Directs audio endpoint routing and controls endpoint visibility in the Windows audio subsystem.
/// </summary>
public interface IAudioEndpointDirector
{
    /// <summary>
    /// Enumerates all audio endpoints and their current presence and configuration states.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A read-only list of <see cref="AudioEndpointInfo"/> representing the detected audio endpoints.</returns>
    Task<IReadOnlyList<AudioEndpointInfo>> EnumerateAudioEndpointsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates audio endpoints filtered by data flow direction.
    /// </summary>
    /// <param name="flow">Audio data flow filter (playback or capture).</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A read-only list of <see cref="AudioEndpointInfo"/> representing the detected audio endpoints.</returns>
    Task<IReadOnlyList<AudioEndpointInfo>> EnumerateAudioEndpointsAsync(
        RigSwitch.Core.Enums.AudioDeviceFlow flow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the default audio playback endpoint for both multimedia and communications roles.
    /// </summary>
    /// <param name="endpointId">The MMDevice GUID string identifier of the target audio endpoint.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetDefaultPlaybackEndpointAsync(string endpointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the default audio capture (microphone) endpoint for console, multimedia, and communications roles.
    /// </summary>
    /// <param name="endpointId">The MMDevice GUID string identifier of the target capture endpoint.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetDefaultCaptureEndpointAsync(string endpointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the volume percentage and mute state for an audio endpoint.
    /// </summary>
    /// <param name="endpointId">The MMDevice GUID string identifier of the audio endpoint.</param>
    /// <param name="volumePercent">The volume percentage (0-100).</param>
    /// <param name="isMuted">Whether the endpoint should be muted.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetEndpointVolumeAsync(string endpointId, int volumePercent, bool isMuted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current volume percentage and mute state of an audio endpoint.
    /// </summary>
    /// <param name="endpointId">The MMDevice GUID string identifier of the audio endpoint.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A tuple of volume percentage (0-100) and mute state.</returns>
    Task<(int VolumePercent, bool IsMuted)> GetEndpointVolumeAsync(string endpointId, CancellationToken cancellationToken = default);

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
