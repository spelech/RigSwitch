namespace RigSwitch.Infrastructure.Windows.Processes;

/// <summary>
/// Abstraction for a running native operating system process.
/// </summary>
public interface INativeProcess : IDisposable
{
    /// <summary>
    /// Gets the unique process identifier.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Gets the name of the process.
    /// </summary>
    string ProcessName { get; }

    /// <summary>
    /// Gets a value indicating whether the process has already exited.
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// Sends a close message to the main window of the process.
    /// </summary>
    /// <returns><c>true</c> if the close message was sent; otherwise <c>false</c>.</returns>
    bool CloseMainWindow();

    /// <summary>
    /// Waits asynchronously for the process to exit up to the specified timeout.
    /// </summary>
    /// <param name="timeout">The maximum duration to wait for process exit.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><c>true</c> if the process exited within the timeout; otherwise <c>false</c>.</returns>
    Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately stops the associated process and optionally its child processes.
    /// </summary>
    /// <param name="entireProcessTree"><c>true</c> to terminate child processes; otherwise <c>false</c>.</param>
    void Kill(bool entireProcessTree = true);
}

/// <summary>
/// Abstraction for querying and starting native operating system processes.
/// </summary>
public interface INativeProcessProvider
{
    /// <summary>
    /// Checks whether the specified file exists on disk.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns><c>true</c> if the file exists; otherwise <c>false</c>.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Starts a process using the specified executable path and optional arguments.
    /// </summary>
    /// <param name="fileName">The executable path.</param>
    /// <param name="arguments">Optional arguments.</param>
    /// <returns>The started <see cref="INativeProcess"/> instance, or <c>null</c> if startup failed.</returns>
    INativeProcess? Start(string fileName, string arguments = "");

    /// <summary>
    /// Gets an existing running process by its process identifier, if running.
    /// </summary>
    /// <param name="processId">The process identifier.</param>
    /// <returns>The <see cref="INativeProcess"/> instance if found and running; otherwise <c>null</c>.</returns>
    INativeProcess? GetProcessById(int processId);

    /// <summary>
    /// Gets all running processes matching the specified process name.
    /// </summary>
    /// <param name="processName">The process name without file extension.</param>
    /// <returns>A collection of matching <see cref="INativeProcess"/> instances.</returns>
    IReadOnlyList<INativeProcess> GetProcessesByName(string processName);
}
