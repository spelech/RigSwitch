namespace RigSwitch.Core.Models;

using RigSwitch.Core.Enums;

/// <summary>
/// Represents descriptive and state information about an audio playback endpoint.
/// </summary>
public sealed record AudioEndpointInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AudioEndpointInfo"/> record.
    /// </summary>
    public AudioEndpointInfo()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioEndpointInfo"/> record with specified properties.
    /// </summary>
    /// <param name="id">The MMDevice endpoint GUID string identifier.</param>
    /// <param name="name">The friendly name of the endpoint device.</param>
    /// <param name="adapterDescription">The name or description of the audio adapter.</param>
    /// <param name="state">The device presence and connection state.</param>
    /// <param name="isDefaultPlayback">Whether the device is the default multimedia playback device.</param>
    /// <param name="isDefaultCommunications">Whether the device is the default communications device.</param>
    public AudioEndpointInfo(
        string id,
        string name,
        string adapterDescription,
        DevicePresenceState state,
        bool isDefaultPlayback,
        bool isDefaultCommunications)
    {
        Id = id;
        Name = name;
        AdapterDescription = adapterDescription;
        State = state;
        IsDefaultPlayback = isDefaultPlayback;
        IsDefaultCommunications = isDefaultCommunications;
    }

    /// <summary>
    /// Gets the MMDevice endpoint GUID string identifier (e.g., "{D2B56B79-F353-4FB0-81B6-ECEF8E95E57A}").
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the friendly name of the endpoint device (e.g., "Speakers (Pebble V3)", "VG34VQL3A").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the description or driver name of the audio adapter (e.g., "Realtek(R) Audio", "NVIDIA High Definition Audio").
    /// </summary>
    public string AdapterDescription { get; init; } = string.Empty;

    /// <summary>
    /// Gets the device presence and connection state.
    /// </summary>
    public DevicePresenceState State { get; init; } = DevicePresenceState.Active;

    /// <summary>
    /// Gets a value indicating whether this endpoint is the default multimedia playback device.
    /// </summary>
    public bool IsDefaultPlayback { get; init; }

    /// <summary>
    /// Gets a value indicating whether this endpoint is the default communications device.
    /// </summary>
    public bool IsDefaultCommunications { get; init; }
}
