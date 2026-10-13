namespace RigSwitch.Infrastructure.Ipc;

using RigSwitch.Core.Ipc;

/// <summary>
/// Defines the contract for an IPC client communicating with the RigSwitch host process.
/// </summary>
public interface IIpcClient
{
    /// <summary>
    /// Sends an IPC request to the server and awaits the response.
    /// </summary>
    /// <param name="request">The IPC request to send.</param>
    /// <param name="timeout">Optional timeout for the operation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The IPC response received from the server.</returns>
    Task<IpcResponse> SendAsync(IpcRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the IPC server is currently running and accepting connections.
    /// </summary>
    /// <param name="timeout">Optional timeout for the connection probe.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the server is reachable; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsServerRunningAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
