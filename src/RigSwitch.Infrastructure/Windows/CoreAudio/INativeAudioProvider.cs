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
}
