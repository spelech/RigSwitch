namespace RigSwitch.Infrastructure.Ipc;

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using RigSwitch.Core.Ipc;

/// <summary>
/// Server implementation using Windows Named Pipes for IPC communication.
/// </summary>
public sealed class NamedPipeIpcServer : IIpcServer
{
    public const string DefaultPipeName = "RigSwitch-Ipc";

    private readonly string _pipeName;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly ConcurrentDictionary<Task, byte> _activeClients = new();

    private Func<IpcRequest, Task<IpcResponse>>? _handler;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeIpcServer"/> class.
    /// </summary>
    /// <param name="pipeName">The named pipe name to listen on.</param>
    public NamedPipeIpcServer(string pipeName = DefaultPipeName)
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName) ? DefaultPipeName : pipeName;
    }

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public void RegisterHandler(Func<IpcRequest, Task<IpcResponse>> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isRunning)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _isRunning = true;
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), CancellationToken.None);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;

            if (_cts != null)
            {
                await _cts.CancelAsync().ConfigureAwait(false);

                if (_listenTask != null)
                {
                    try
                    {
                        await _listenTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when listener task cancels
                    }
                    catch (Exception)
                    {
                        // Ignore listen task termination errors during shutdown
                    }
                }

                _cts.Dispose();
                _cts = null;
                _listenTask = null;
            }

            var clients = _activeClients.Keys.ToArray();
            if (clients.Length > 0)
            {
                try
                {
                    await Task.WhenAll(clients).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore client completion timeout during shutdown
                }
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? serverStream = null;
            try
            {
                serverStream = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (serverStream != null)
                {
                    await serverStream.DisposeAsync().ConfigureAwait(false);
                }
                break;
            }
            catch (Exception)
            {
                if (serverStream != null)
                {
                    await serverStream.DisposeAsync().ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                continue;
            }

            var clientStream = serverStream;
            var clientTask = Task.Run(() => ProcessClientAsync(clientStream, cancellationToken), CancellationToken.None);
            TrackClientTask(clientTask);
        }
    }

    private void TrackClientTask(Task clientTask)
    {
        _activeClients.TryAdd(clientTask, 0);
        _ = clientTask.ContinueWith(t => _activeClients.TryRemove(t, out _), TaskScheduler.Default);
    }

    private async Task ProcessClientAsync(NamedPipeServerStream serverStream, CancellationToken cancellationToken)
    {
        await using (serverStream.ConfigureAwait(false))
        {
            try
            {
                using var reader = new StreamReader(serverStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
                await using var writer = new StreamWriter(serverStream, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true);
                writer.NewLine = "\n";

                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                IpcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<IpcRequest>(line);
                }
                catch (Exception ex)
                {
                    var failResponse = IpcResponse.Fail("unknown", $"Malformed JSON request: {ex.Message}");
                    var failJson = JsonSerializer.Serialize(failResponse);
                    await writer.WriteLineAsync(failJson.AsMemory(), cancellationToken).ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (request == null)
                {
                    var failResponse = IpcResponse.Fail("unknown", "Empty or invalid request payload.");
                    var failJson = JsonSerializer.Serialize(failResponse);
                    await writer.WriteLineAsync(failJson.AsMemory(), cancellationToken).ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                IpcResponse response;
                var handler = _handler;
                if (handler != null)
                {
                    try
                    {
                        response = await handler(request).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        response = IpcResponse.Fail(request.Id, $"Handler error: {ex.Message}");
                    }
                }
                else
                {
                    response = IpcResponse.Fail(request.Id, "No handler registered.");
                }

                var responseJson = JsonSerializer.Serialize(response);
                await writer.WriteLineAsync(responseJson.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Client closed pipe prematurely
            }
            catch (OperationCanceledException)
            {
                // Server shutdown / cancel
            }
            catch (Exception)
            {
                // Suppress unexpected exceptions to prevent crashing the server
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _stateLock.Dispose();
    }
}
