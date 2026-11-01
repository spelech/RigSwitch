namespace RigSwitch.Core.Ipc;

/// <summary>
/// Represents an IPC response returned by the RigSwitch host process.
/// </summary>
public sealed class IpcResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IpcResponse"/> class.
    /// </summary>
    public IpcResponse()
    {
    }

    /// <summary>
    /// Gets or sets the unique request identifier this response correlates to.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the requested operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets an optional human-readable message or error description.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets optional payload data returned by the operation.
    /// </summary>
    public Dictionary<string, object?>? Data { get; set; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    /// <param name="id">The correlated request identifier.</param>
    /// <param name="data">Optional payload data.</param>
    /// <param name="message">Optional success message.</param>
    /// <returns>A successful <see cref="IpcResponse"/>.</returns>
    public static IpcResponse Ok(string id, Dictionary<string, object?>? data = null, string? message = null)
    {
        return new IpcResponse
        {
            Id = id,
            Success = true,
            Data = data,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    /// <param name="id">The correlated request identifier.</param>
    /// <param name="message">The failure error message.</param>
    /// <returns>A failed <see cref="IpcResponse"/>.</returns>
    public static IpcResponse Fail(string id, string message)
    {
        return new IpcResponse
        {
            Id = id,
            Success = false,
            Message = message,
            Data = null
        };
    }
}
