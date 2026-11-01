namespace RigSwitch.Infrastructure.Ipc;

using RigSwitch.Core.Ipc;

/// <summary>
/// Defines the contract for an IPC server listening for incoming commands.
/// </summary>
public interface IIpcServer : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the server is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts listening for IPC client connections asynchronously.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops listening for IPC client connections asynchronously.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a handler to process incoming IPC requests.
    /// </summary>
    /// <param name="handler">The asynchronous request handler.</param>
    void RegisterHandler(Func<IpcRequest, Task<IpcResponse>> handler);
}
