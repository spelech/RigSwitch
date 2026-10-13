namespace RigSwitch.Tests.Unit;

using System.IO;
using System.Text.Json;
using RigSwitch.Core.Ipc;
using RigSwitch.Infrastructure.Ipc;
using Xunit;

public sealed class NamedPipeIpcTests
{
    private static string CreateUniquePipeName() => $"RigSwitch-Test-{Guid.NewGuid():N}";

    [Fact]
    public async Task Server_WhenStartedAndStopped_UpdatesIsRunning()
    {
        var pipeName = CreateUniquePipeName();
        await using var server = new NamedPipeIpcServer(pipeName);

        Assert.False(server.IsRunning);

        await server.StartAsync();
        Assert.True(server.IsRunning);

        await server.StopAsync();
        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task IsServerRunningAsync_ReturnsExpectedStatus()
    {
        var pipeName = CreateUniquePipeName();
        var client = new NamedPipeIpcClient(pipeName);

        var runningBefore = await client.IsServerRunningAsync(TimeSpan.FromMilliseconds(200));
        Assert.False(runningBefore);

        await using var server = new NamedPipeIpcServer(pipeName);
        await server.StartAsync();

        var runningWhileStarted = await client.IsServerRunningAsync(TimeSpan.FromMilliseconds(500));
        Assert.True(runningWhileStarted);

        await server.StopAsync();

        var runningAfter = await client.IsServerRunningAsync(TimeSpan.FromMilliseconds(200));
        Assert.False(runningAfter);
    }

    [Fact]
    public async Task SendAsync_RoundtripRequestAndResponse_MatchesData()
    {
        var pipeName = CreateUniquePipeName();
        await using var server = new NamedPipeIpcServer(pipeName);

        server.RegisterHandler(request =>
        {
            Assert.Equal(IpcCommands.Switch, request.Command);
            Assert.Equal("Gaming", request.Args["profile"]);

            var data = new Dictionary<string, object?>
            {
                ["appliedPreset"] = 1,
                ["status"] = "ok"
            };

            return Task.FromResult(IpcResponse.Ok(request.Id, data, "Switched successfully"));
        });

        await server.StartAsync();

        var client = new NamedPipeIpcClient(pipeName);
        var request = new IpcRequest(IpcCommands.Switch, new Dictionary<string, string?>
        {
            ["profile"] = "Gaming"
        });

        var response = await client.SendAsync(request, TimeSpan.FromSeconds(2));

        Assert.NotNull(response);
        Assert.Equal(request.Id, response.Id);
        Assert.True(response.Success);
        Assert.Equal("Switched successfully", response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal("ok", response.Data["status"]?.ToString());
    }

    [Fact]
    public async Task SendAsync_WhenHandlerThrows_ReturnsFailureResponseWithoutCrashingServer()
    {
        var pipeName = CreateUniquePipeName();
        await using var server = new NamedPipeIpcServer(pipeName);

        server.RegisterHandler(_ => throw new InvalidOperationException("Simulated coordinator failure"));

        await server.StartAsync();

        var client = new NamedPipeIpcClient(pipeName);
        var request = new IpcRequest(IpcCommands.Doctor);

        var response = await client.SendAsync(request, TimeSpan.FromSeconds(2));

        Assert.NotNull(response);
        Assert.Equal(request.Id, response.Id);
        Assert.False(response.Success);
        Assert.Contains("Simulated coordinator failure", response.Message);

        // Verify server is still alive and answering subsequent requests
        server.RegisterHandler(req => Task.FromResult(IpcResponse.Ok(req.Id, message: "Recovered")));
        var secondResponse = await client.SendAsync(new IpcRequest(IpcCommands.Status), TimeSpan.FromSeconds(2));
        Assert.True(secondResponse.Success);
        Assert.Equal("Recovered", secondResponse.Message);
    }

    [Fact]
    public async Task SendAsync_WhenServerNotRunning_ThrowsTimeoutException()
    {
        var pipeName = CreateUniquePipeName();
        var client = new NamedPipeIpcClient(pipeName);

        var request = new IpcRequest(IpcCommands.Status);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            client.SendAsync(request, TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public async Task SendAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var pipeName = CreateUniquePipeName();
        var client = new NamedPipeIpcClient(pipeName);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new IpcRequest(IpcCommands.Status);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SendAsync(request, TimeSpan.FromSeconds(5), cts.Token));
    }

    [Fact]
    public async Task Server_HandlesMultipleSequentialRequests()
    {
        var pipeName = CreateUniquePipeName();
        await using var server = new NamedPipeIpcServer(pipeName);

        int counter = 0;
        server.RegisterHandler(req =>
        {
            Interlocked.Increment(ref counter);
            return Task.FromResult(IpcResponse.Ok(req.Id, new Dictionary<string, object?> { ["seq"] = counter }));
        });

        await server.StartAsync();

        var client = new NamedPipeIpcClient(pipeName);
        for (int i = 1; i <= 5; i++)
        {
            var res = await client.SendAsync(new IpcRequest(IpcCommands.Status), TimeSpan.FromSeconds(2));
            Assert.True(res.Success);
            Assert.Equal(i.ToString(System.Globalization.CultureInfo.InvariantCulture), res.Data?["seq"]?.ToString());
        }
    }

    [Fact]
    public async Task Server_HandlesMultipleConcurrentRequests()
    {
        var pipeName = CreateUniquePipeName();
        await using var server = new NamedPipeIpcServer(pipeName);

        server.RegisterHandler(async req =>
        {
            await Task.Delay(20);
            return IpcResponse.Ok(req.Id, new Dictionary<string, object?> { ["echo"] = req.Command });
        });

        await server.StartAsync();

        var client = new NamedPipeIpcClient(pipeName);
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var req = new IpcRequest($"cmd-{i}");
            var res = await client.SendAsync(req, TimeSpan.FromSeconds(5));
            Assert.True(res.Success);
            Assert.Equal(req.Id, res.Id);
            Assert.Equal($"cmd-{i}", res.Data?["echo"]?.ToString());
        });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Server_WhenMalformedJsonReceived_ReturnsFailResponse()
    {
        var pipeName = CreateUniquePipeName();
        await using var server = new NamedPipeIpcServer(pipeName);
        await server.StartAsync();

        // Connect raw client stream and send invalid JSON
        await using var rawClient = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
        await rawClient.ConnectAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        using var reader = new StreamReader(rawClient, System.Text.Encoding.UTF8);
        await using var writer = new StreamWriter(rawClient, new System.Text.UTF8Encoding(false));
        writer.NewLine = "\n";

        await writer.WriteLineAsync("{not-valid-json");
        await writer.FlushAsync();

        var responseLine = await reader.ReadLineAsync();
        Assert.NotNull(responseLine);

        var response = JsonSerializer.Deserialize<IpcResponse>(responseLine);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Contains("Malformed JSON", response.Message);
    }

    [Fact]
    public async Task SendAsync_WhenNoHandlerRegistered_ReturnsFailResponse()
    {
        var pipeName = CreateUniquePipeName();
        await using var server = new NamedPipeIpcServer(pipeName);
        await server.StartAsync();

        var client = new NamedPipeIpcClient(pipeName);
        var response = await client.SendAsync(new IpcRequest(IpcCommands.Status), TimeSpan.FromSeconds(2));

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal("No handler registered.", response.Message);
    }

    [Fact]
    public async Task Server_CanRestartCleanly()
    {
        var pipeName = CreateUniquePipeName();
        await using var server = new NamedPipeIpcServer(pipeName);
        server.RegisterHandler(req => Task.FromResult(IpcResponse.Ok(req.Id, message: "OK")));

        await server.StartAsync();
        Assert.True(server.IsRunning);

        var client = new NamedPipeIpcClient(pipeName);
        var res1 = await client.SendAsync(new IpcRequest(IpcCommands.Status), TimeSpan.FromSeconds(2));
        Assert.True(res1.Success);

        await server.StopAsync();
        Assert.False(server.IsRunning);

        await server.StartAsync();
        Assert.True(server.IsRunning);

        var res2 = await client.SendAsync(new IpcRequest(IpcCommands.Status), TimeSpan.FromSeconds(2));
        Assert.True(res2.Success);

        await server.StopAsync();
    }

    [Fact]
    public async Task Server_StartAsync_IsIdempotent()
    {
        var pipeName = CreateUniquePipeName();
        await using var server = new NamedPipeIpcServer(pipeName);

        await server.StartAsync();
        Assert.True(server.IsRunning);

        await server.StartAsync();
        Assert.True(server.IsRunning);

        await server.StopAsync();
    }

    [Fact]
    public async Task Client_SendAsync_ThrowsArgumentNullException_ForNullRequest()
    {
        var pipeName = CreateUniquePipeName();
        var client = new NamedPipeIpcClient(pipeName);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.SendAsync(null!));
    }

    [Fact]
    public async Task Client_IsServerRunningAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        var pipeName = CreateUniquePipeName();
        var client = new NamedPipeIpcClient(pipeName);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.IsServerRunningAsync(TimeSpan.FromSeconds(2), cts.Token));
    }

    [Fact]
    public async Task DisposeAsync_WithoutExplicitStop_StopsServerAndSetsIsRunningFalse()
    {
        var pipeName = CreateUniquePipeName();
        var server = new NamedPipeIpcServer(pipeName);

        await server.StartAsync();
        Assert.True(server.IsRunning);

        await server.DisposeAsync();
        Assert.False(server.IsRunning);
    }
}

