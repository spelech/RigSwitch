namespace RigSwitch.Infrastructure.Windows.Processes;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Infrastructure service for launching and gracefully terminating application lifecycle hooks on Windows.
/// </summary>
public sealed class WindowsApplicationLifecycleHookService : IApplicationLifecycleHookService
{
    private static readonly TimeSpan DefaultGracefulCloseTimeout = TimeSpan.FromMilliseconds(2500);
    private readonly INativeProcessProvider _processProvider;
    private readonly ConcurrentDictionary<string, int> _launchedPids = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsApplicationLifecycleHookService"/> class.
    /// </summary>
    /// <param name="processProvider">The native process provider abstraction.</param>
    public WindowsApplicationLifecycleHookService(INativeProcessProvider? processProvider = null)
    {
        _processProvider = processProvider ?? new WindowsNativeProcessProvider();
    }

    /// <inheritdoc />
    public Task LaunchHooksForPresetAsync(WorkstationPreset preset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);

        if (preset.ApplicationHooks.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var hook in preset.ApplicationHooks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(hook.ExecutablePath))
            {
                continue;
            }

            if (!_processProvider.FileExists(hook.ExecutablePath))
            {
                Trace.TraceWarning($"[LifecycleHook] Executable not found at '{hook.ExecutablePath}' for hook '{hook.Id}'. Skipping launch.");
                continue;
            }

            try
            {
                var process = _processProvider.Start(hook.ExecutablePath, hook.Arguments);
                if (process != null)
                {
                    _launchedPids[hook.Id] = process.Id;
                    process.Dispose();
                }
                else
                {
                    Trace.TraceWarning($"[LifecycleHook] Failed to start process for hook '{hook.Id}' ('{hook.ExecutablePath}').");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[LifecycleHook] Error starting process for hook '{hook.Id}': {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task CloseHooksForPresetAsync(WorkstationPreset preset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);

        if (preset.ApplicationHooks.Count == 0)
        {
            return;
        }

        foreach (var hook in preset.ApplicationHooks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!hook.CloseOnSwitchAway || string.IsNullOrWhiteSpace(hook.ExecutablePath))
            {
                continue;
            }

            await CloseHookAsync(hook, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CloseHookAsync(PresetApplicationHook hook, CancellationToken cancellationToken)
    {
        bool closedByPid = false;

        if (_launchedPids.TryRemove(hook.Id, out var pid))
        {
            using var process = _processProvider.GetProcessById(pid);
            if (process != null && !process.HasExited)
            {
                await TerminateProcessGracefullyAsync(process, cancellationToken).ConfigureAwait(false);
                closedByPid = true;
            }
        }

        if (!closedByPid)
        {
            // Fallback: check running processes matching executable name without extension
            var processName = Path.GetFileNameWithoutExtension(hook.ExecutablePath);
            if (!string.IsNullOrWhiteSpace(processName))
            {
                var matchingProcesses = _processProvider.GetProcessesByName(processName);
                foreach (var proc in matchingProcesses)
                {
                    using (proc)
                    {
                        if (!proc.HasExited)
                        {
                            await TerminateProcessGracefullyAsync(proc, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }
    }

    private static async Task TerminateProcessGracefullyAsync(INativeProcess process, CancellationToken cancellationToken)
    {
        try
        {
            var requestedClose = process.CloseMainWindow();
            if (requestedClose)
            {
                var exited = await process.WaitForExitAsync(DefaultGracefulCloseTimeout, cancellationToken).ConfigureAwait(false);
                if (exited)
                {
                    return;
                }
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[LifecycleHook] Exception terminating process PID {process.Id}: {ex.Message}");
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
        _launchedPids.Clear();
    }
}
