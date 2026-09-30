namespace RigSwitch.Infrastructure.Windows.CoreAudio;

using RigSwitch.Core.Models;

/// <summary>
/// Provides an abstraction over Windows CoreAudio native COM and registry operations.
/// </summary>
public interface INativeAudioProvider
{
    /// <summary>
    /// Enumerates all render (playback) audio endpoints and their current states.
    /// </summary>
    /// <returns>A read-only list of <see cref="AudioEndpointInfo"/> instances.</returns>
    IReadOnlyList<AudioEndpointInfo> EnumerateRenderEndpoints();

    /// <summary>
    /// Enumerates all capture (recording/microphone) audio endpoints and their current states.
    /// </summary>
    /// <returns>A read-only list of <see cref="AudioEndpointInfo"/> instances.</returns>
    IReadOnlyList<AudioEndpointInfo> EnumerateCaptureEndpoints();

    /// <summary>
    /// Sets the default audio endpoint for the specified role.
    /// </summary>
    /// <param name="endpointId">The endpoint device identifier.</param>
    /// <param name="role">The audio role (eConsole, eMultimedia, eCommunications).</param>
    void SetDefaultEndpoint(string endpointId, ERole role);

    /// <summary>
    /// Sets the visibility (enabled/disabled state) of an endpoint.
    /// </summary>
    /// <param name="endpointId">The endpoint device identifier.</param>
    /// <param name="isVisible">True to enable/show, false to disable/hide.</param>
    void SetEndpointVisibility(string endpointId, bool isVisible);

    /// <summary>
    /// Sets the master volume scalar and mute state for an audio endpoint.
    /// </summary>
    /// <param name="endpointId">The endpoint device identifier.</param>
    /// <param name="scalarLevel">The volume scalar level clamped from 0.0f to 1.0f.</param>
    /// <param name="isMuted">Whether the endpoint is muted.</param>
    void SetEndpointVolume(string endpointId, float scalarLevel, bool isMuted);

    /// <summary>
    /// Retrieves the master volume scalar and mute state for an audio endpoint.
    /// </summary>
    /// <param name="endpointId">The endpoint device identifier.</param>
    /// <returns>A tuple of volume scalar level (0.0f to 1.0f) and mute state.</returns>
    (float ScalarLevel, bool IsMuted) GetEndpointVolume(string endpointId);
}
