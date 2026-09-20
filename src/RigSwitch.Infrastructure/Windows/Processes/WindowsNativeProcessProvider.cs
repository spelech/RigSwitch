namespace RigSwitch.Infrastructure.Windows.Processes;

using System.Diagnostics;
using System.IO;

/// <summary>
/// Production wrapper around <see cref="Process"/> implementing <see cref="INativeProcess"/>.
/// </summary>
public sealed class WindowsNativeProcess : INativeProcess
{
    private readonly Process _process;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsNativeProcess"/> class.
    /// </summary>
    /// <param name="process">The underlying system process.</param>
    public WindowsNativeProcess(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    /// <inheritdoc />
    public int Id => _process.Id;

    /// <inheritdoc />
    public string ProcessName => _process.ProcessName;

    /// <inheritdoc />
    public bool HasExited
    {
        get
        {
            try
            {
                return _process.HasExited;
            }
            catch
            {
                return true;
            }
        }
    }

    /// <inheritdoc />
    public bool CloseMainWindow()
    {
        try
        {
            return _process.CloseMainWindow();
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            await _process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return HasExited;
        }
        catch
        {
            return HasExited;
        }
    }

    /// <inheritdoc />
    public void Kill(bool entireProcessTree = true)
    {
        try
        {
            _process.Kill(entireProcessTree);
        }
        catch (InvalidOperationException)
        {
            // Process has already exited.
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to kill process {_process.ProcessName} (PID {_process.Id}): {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _process.Dispose();
    }
}

/// <summary>
/// Production implementation of <see cref="INativeProcessProvider"/> targeting Windows.
/// </summary>
public sealed class WindowsNativeProcessProvider : INativeProcessProvider
{
    /// <inheritdoc />
    public bool FileExists(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    /// <inheritdoc />
    public INativeProcess? Start(string fileName, string arguments = "")
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = string.IsNullOrWhiteSpace(arguments),
                WorkingDirectory = Path.GetDirectoryName(fileName) ?? string.Empty
            };

            var process = Process.Start(startInfo);
            return process != null ? new WindowsNativeProcess(process) : null;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to start process '{fileName}' with arguments '{arguments}': {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public INativeProcess? GetProcessById(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                process.Dispose();
                return null;
            }

            return new WindowsNativeProcess(process);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<INativeProcess> GetProcessesByName(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            var results = new List<INativeProcess>(processes.Length);

            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        results.Add(new WindowsNativeProcess(process));
                        continue;
                    }
                }
                catch
                {
                    // Ignore processes that exited between query and check
                }

                process.Dispose();
            }

            return results;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to query processes by name '{processName}': {ex.Message}");
            return [];
        }
    }
}
