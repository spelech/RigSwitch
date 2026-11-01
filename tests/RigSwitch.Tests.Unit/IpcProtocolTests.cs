namespace RigSwitch.Tests.Unit;

using System.Text.Json;
using RigSwitch.Core.Ipc;
using Xunit;

public sealed class IpcProtocolTests
{
    [Fact]
    public void IpcRequest_Defaults_GeneratesValidGuidIdAndEmptyArgsCaseInsensitive()
    {
        var request = new IpcRequest();

        Assert.False(string.IsNullOrWhiteSpace(request.Id));
        Assert.True(Guid.TryParse(request.Id, out _));
        Assert.Equal(string.Empty, request.Command);
        Assert.NotNull(request.Args);
        Assert.Empty(request.Args);

        request.Args["Profile"] = "Desk";
        Assert.Equal("Desk", request.Args["profile"]);
    }

    [Fact]
    public void IpcRequest_ParameterizedConstructor_SetsCommandAndCopiesArgsWithCaseInsensitiveComparer()
    {
        var initialArgs = new Dictionary<string, string?>
        {
            ["Name"] = "Gaming",
            ["Preset"] = "1"
        };

        var request = new IpcRequest(IpcCommands.Switch, initialArgs);

        Assert.False(string.IsNullOrWhiteSpace(request.Id));
        Assert.Equal(IpcCommands.Switch, request.Command);
        Assert.Equal("Gaming", request.Args["name"]);
        Assert.Equal("1", request.Args["PRESET"]);
    }

    [Fact]
    public void IpcRequest_SerializationRoundtrip_PreservesAllFieldsAndCaseInsensitivity()
    {
        var request = new IpcRequest(IpcCommands.Audio, new Dictionary<string, string?>
        {
            ["Action"] = "toggle",
            ["DeviceId"] = "{audio-guid}"
        });

        var json = JsonSerializer.Serialize(request);
        var restored = JsonSerializer.Deserialize<IpcRequest>(json);

        Assert.NotNull(restored);
        Assert.Equal(request.Id, restored.Id);
        Assert.Equal(IpcCommands.Audio, restored.Command);
        Assert.Equal("toggle", restored.Args["action"]);
        Assert.Equal("{audio-guid}", restored.Args["DEVICEID"]);
    }

    [Fact]
    public void IpcResponse_Ok_CreatesSuccessfulResponse()
    {
        var data = new Dictionary<string, object?>
        {
            ["profile"] = "Desk",
            ["activePreset"] = 0
        };

        var response = IpcResponse.Ok("req-123", data, "Profile query successful");

        Assert.Equal("req-123", response.Id);
        Assert.True(response.Success);
        Assert.Equal("Profile query successful", response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal("Desk", response.Data["profile"]);
    }

    [Fact]
    public void IpcResponse_Fail_CreatesFailedResponse()
    {
        var response = IpcResponse.Fail("req-456", "Unknown command");

        Assert.Equal("req-456", response.Id);
        Assert.False(response.Success);
        Assert.Equal("Unknown command", response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    public void IpcResponse_SerializationRoundtrip_PreservesData()
    {
        var data = new Dictionary<string, object?>
        {
            ["count"] = 2
        };

        var original = IpcResponse.Ok("req-789", data, "Success");
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<IpcResponse>(json);

        Assert.NotNull(restored);
        Assert.Equal("req-789", restored.Id);
        Assert.True(restored.Success);
        Assert.Equal("Success", restored.Message);
        Assert.NotNull(restored.Data);
        Assert.True(restored.Data.ContainsKey("count"));
    }

    [Fact]
    public void IpcCommands_ConstantsMatchExpectedValues()
    {
        Assert.Equal("status", IpcCommands.Status);
        Assert.Equal("switch", IpcCommands.Switch);
        Assert.Equal("audio", IpcCommands.Audio);
        Assert.Equal("blacklist", IpcCommands.Blacklist);
        Assert.Equal("screenshot", IpcCommands.Screenshot);
        Assert.Equal("doctor", IpcCommands.Doctor);
    }
}
