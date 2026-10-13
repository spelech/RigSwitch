namespace RigSwitch.Core.Ipc;

/// <summary>
/// Represents an IPC request sent from a client to the RigSwitch host process.
/// </summary>
public sealed class IpcRequest
{
    private Dictionary<string, string?> _args = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="IpcRequest"/> class.
    /// </summary>
    public IpcRequest()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IpcRequest"/> class with the specified command and optional arguments.
    /// </summary>
    /// <param name="command">The command name.</param>
    /// <param name="args">Optional arguments for the command.</param>
    public IpcRequest(string command, Dictionary<string, string?>? args = null)
    {
        Command = command;
        if (args != null)
        {
            _args = new Dictionary<string, string?>(args, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Gets or sets the unique request identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the command identifier.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key-value arguments for the command.
    /// </summary>
    public Dictionary<string, string?> Args
    {
        get => _args;
        set => _args = value != null
            ? new Dictionary<string, string?>(value, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }
}
