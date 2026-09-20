namespace RigSwitch.Tests.Harness.Disturbances;

/// <summary>
/// Disturbance that simulates unplugging a display monitor for the lifetime of a test scope.
/// </summary>
public sealed class MonitorUnpluggedDisturbance : IDisposable
{
    private readonly SimulationDisturbanceContext _context;
    private readonly string _monitorId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MonitorUnpluggedDisturbance"/> class and disconnects the monitor.
    /// </summary>
    public MonitorUnpluggedDisturbance(SimulationDisturbanceContext context, string monitorId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(monitorId);

        _context = context;
        _monitorId = monitorId;
        _context.SimulateTargetMonitorDisconnect(_monitorId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _context.SimulateTargetMonitorReconnect(_monitorId);
        _disposed = true;
    }
}
