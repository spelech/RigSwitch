namespace RigSwitch.Tests.Harness.Disturbances;

using RigSwitch.Core.Enums;

/// <summary>
/// Disturbance that simulates setting an audio endpoint presence state for the lifetime of a test scope.
/// </summary>
public sealed class AudioFallbackDisturbance : IDisposable
{
    private readonly SimulationDisturbanceContext _context;
    private readonly string _endpointId;
    private readonly DevicePresenceState _previousState;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioFallbackDisturbance"/> class and updates device state.
    /// </summary>
    public AudioFallbackDisturbance(
        SimulationDisturbanceContext context,
        string endpointId,
        DevicePresenceState targetState = DevicePresenceState.Unplugged)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        _context = context;
        _endpointId = endpointId;
        _previousState = context.GetAudioDeviceState(_endpointId);
        _context.SimulateAudioDeviceStateChange(_endpointId, targetState);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _context.SimulateAudioDeviceStateChange(_endpointId, _previousState);
        _disposed = true;
    }
}
