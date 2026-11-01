namespace RigSwitch.Infrastructure.Ipc;

using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using RigSwitch.Core.Ipc;

/// <summary>
/// Client implementation using Windows Named Pipes for IPC communication.
/// </summary>
public sealed class NamedPipeIpcClient : IIpcClient
{
    public const string DefaultPipeName = "RigSwitch-Ipc";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromMilliseconds(500);

    private readonly string _pipeName;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeIpcClient"/> class.
    /// </summary>
    /// <param name="pipeName">The named pipe name to connect to.</param>
    public NamedPipeIpcClient(string pipeName = DefaultPipeName)
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName) ? DefaultPipeName : pipeName;
    }

    /// <inheritdoc />
    public async Task<IpcResponse> SendAsync(
        IpcRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var effectiveTimeout = timeout ?? DefaultTimeout;
        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var clientStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await using (clientStream.ConfigureAwait(false))
        {
            try
            {
                await clientStream.ConnectAsync(effectiveTimeout, linkedCts.Token).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(
                    string.Format(CultureInfo.InvariantCulture, "Failed to connect to Named Pipe '{0}' within {1}s.", _pipeName, effectiveTimeout.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)),
                    ex);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    string.Format(CultureInfo.InvariantCulture, "Failed to connect to Named Pipe '{0}' within {1}s.", _pipeName, effectiveTimeout.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)));
            }

            using var reader = new StreamReader(clientStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            await using var writer = new StreamWriter(clientStream, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true);
            writer.NewLine = "\n";

            var requestJson = JsonSerializer.Serialize(request);
            try
            {
                await writer.WriteLineAsync(requestJson.AsMemory(), linkedCts.Token).ConfigureAwait(false);
                await writer.FlushAsync(linkedCts.Token).ConfigureAwait(false);

                string? responseJson = await reader.ReadLineAsync(linkedCts.Token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    throw new IOException(string.Format(CultureInfo.InvariantCulture, "Server closed the pipe '{0}' without returning a response.", _pipeName));
                }

                var response = JsonSerializer.Deserialize<IpcResponse>(responseJson);
                return response ?? throw new InvalidOperationException("Failed to deserialize response from server.");
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    string.Format(CultureInfo.InvariantCulture, "Operation timed out after {0}s on pipe '{1}'.", effectiveTimeout.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture), _pipeName));
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsServerRunningAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? DefaultProbeTimeout;
        try
        {
            await using var clientStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await clientStream.ConnectAsync(effectiveTimeout, linkedCts.Token).ConfigureAwait(false);
            return clientStream.IsConnected;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
