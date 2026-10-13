namespace RigSwitch.Tests.Unit;

using System.IO;
using System.Text.Json;
using NSubstitute;
using RigSwitch.App.Services;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Ipc;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Ipc;
using RigSwitch.Infrastructure.Windows.CoreAudio;
using Xunit;

public sealed class CliControllerTests
{
    private readonly IIpcClient _ipcClient;
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsService;
    private readonly IAudioEndpointWatchdogService _watchdogService;
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly IpcCommandHandler _headlessHandler;
    private readonly UserSettings _testSettings;

    public CliControllerTests()
    {
        _ipcClient = Substitute.For<IIpcClient>();
        _coordinator = Substitute.For<IProfileSwitchCoordinator>();
        _settingsService = Substitute.For<ISettingsStorageService>();
        _watchdogService = Substitute.For<IAudioEndpointWatchdogService>();
        _displayService = Substitute.For<IDisplayConfigurationService>();
        _audioDirector = Substitute.For<IAudioEndpointDirector>();

        _testSettings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{DAC-TEST-1}"]
        };

        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(_testSettings);

        _coordinator.CurrentProfile.Returns(ProfileMode.Desk);
        _coordinator.CurrentAudioOutputMode.Returns(AudioOutputMode.Primary);
        _coordinator.CurrentPreset.Returns(new WorkstationPreset { Name = "Main Preset", PrimaryAudioId = "Speakers", TargetMonitorId = "MON-1" });
        _watchdogService.IsRunning.Returns(true);

        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DisplayDeviceInfo>
            {
                new("MON-1", @"\\.\DISPLAY1", "Dell UltraSharp", "GPU0", isActive: true, isPrimary: true)
            });

        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AudioEndpointInfo>
            {
                new("Speakers", "Realtek Audio", "Speakers", DevicePresenceState.Active, isDefaultPlayback: true, isDefaultCommunications: false)
            });

        _headlessHandler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("")]
    public async Task RunAsync_Help_OutputsUsageAndReturnsZero(string flag)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        string[] args = string.IsNullOrEmpty(flag) ? [] : [flag];
        int exitCode = await controller.RunAsync(args);

        Assert.Equal(0, exitCode);
        var output = outWriter.ToString();
        Assert.Contains("Usage:", output);
        Assert.Contains("Commands:", output);
        Assert.Contains("status", output);
        Assert.Contains("switch", output);
    }

    [Fact]
    public async Task RunAsync_UnknownCommand_OutputsErrorAndReturnsOne()
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["foobar"]);

        Assert.Equal(1, exitCode);
        var error = errWriter.ToString();
        Assert.Contains("Unknown command 'foobar'", error);
    }

    [Fact]
    public async Task RunAsync_Status_ViaIpc_FormatsOutput()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var data = new Dictionary<string, object?>
        {
            ["profile"] = "Desk",
            ["activePresetName"] = "Productivity",
            ["audioMode"] = "Primary",
            ["watchdogActive"] = true,
            ["displays"] = new List<string> { "MON-1: Dell 4K" },
            ["audioEndpoints"] = new List<string> { "Speakers: Realtek" },
            ["blacklistedEndpoints"] = new List<string> { "{DAC-1}" }
        };

        _ipcClient.SendAsync(Arg.Any<IpcRequest>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-status", data, "Status retrieved."));

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["status"]);

        Assert.Equal(0, exitCode);
        var output = outWriter.ToString();
        Assert.Contains("Profile:", output);
        Assert.Contains("Active Preset:", output);
        Assert.Contains("Audio Output Mode:", output);
        Assert.Contains("Watchdog Status:", output);
        Assert.Contains("Displays:", output);
        Assert.Contains("Audio Endpoints:", output);
        Assert.Contains("Blacklisted DACs:", output);
    }

    [Fact]
    public async Task RunAsync_Status_JsonFlag_OutputsValidJson()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var data = new Dictionary<string, object?>
        {
            ["profile"] = "SimRig",
            ["audioMode"] = "Secondary",
            ["activePresetName"] = "Racing Rig",
            ["watchdogActive"] = true
        };

        _ipcClient.SendAsync(Arg.Any<IpcRequest>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-json", data));

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["status", "--json"]);

        Assert.Equal(0, exitCode);
        var jsonText = outWriter.ToString();
        using var doc = JsonDocument.Parse(jsonText);
        Assert.Equal("SimRig", doc.RootElement.GetProperty("profile").GetString());
        Assert.Equal("Secondary", doc.RootElement.GetProperty("audioMode").GetString());
    }

    [Fact]
    public async Task RunAsync_Status_HeadlessFallback_WhenIpcDisconnected_Succeeds()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["status"]);

        Assert.Equal(0, exitCode);
        var output = outWriter.ToString();
        Assert.Contains("Profile: Desk", output);
        Assert.Contains("Watchdog Status: Active", output);
    }

    [Fact]
    public async Task RunAsync_Switch_Desk_CallsCoordinatorAndOutputsResult()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var switchData = new Dictionary<string, object?>
        {
            ["profile"] = "Desk",
            ["display"] = "MON-1",
            ["audio"] = "Realtek Speakers"
        };

        _ipcClient.SendAsync(Arg.Any<IpcRequest>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-switch", switchData, "Switched successfully."));

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["switch", "desk"]);

        Assert.Equal(0, exitCode);
        var output = outWriter.ToString();
        Assert.Contains("Switched profile to: Desk | Display: MON-1 | Audio: Realtek Speakers", output);
    }

    [Theory]
    [InlineData("switch")]
    [InlineData("switch", "invalid-target")]
    public async Task RunAsync_Switch_MissingOrInvalidTarget_ReturnsOne(params string[] args)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(args);

        Assert.Equal(1, exitCode);
        var error = errWriter.ToString();
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task RunAsync_Audio_Toggle_OutputsNewMode()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var data = new Dictionary<string, object?>
        {
            ["audioMode"] = "Secondary"
        };

        _ipcClient.SendAsync(Arg.Any<IpcRequest>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-audio", data));

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["audio", "toggle"]);

        Assert.Equal(0, exitCode);
        var output = outWriter.ToString();
        Assert.Contains("Audio output mode set to: Secondary", output);
    }

    [Fact]
    public async Task RunAsync_Blacklist_ListAndAdd_Works()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var listData = new Dictionary<string, object?>
        {
            ["blacklistedEndpoints"] = new List<string> { "{DAC-SHAKER}" }
        };

        _ipcClient.SendAsync(Arg.Is<IpcRequest>(r => r.Args["action"] == "list"), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-bl-list", listData));

        var addData = new Dictionary<string, object?>
        {
            ["blacklistedEndpoints"] = new List<string> { "{DAC-SHAKER}", "{DAC-NEW}" }
        };

        _ipcClient.SendAsync(Arg.Is<IpcRequest>(r => r.Args["action"] == "add"), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-bl-add", addData, "Added '{DAC-NEW}' to blacklist."));

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int listExitCode = await controller.RunAsync(["blacklist", "list"]);
        Assert.Equal(0, listExitCode);
        Assert.Contains("{DAC-SHAKER}", outWriter.ToString());

        int addExitCode = await controller.RunAsync(["blacklist", "add", "{DAC-NEW}"]);
        Assert.Equal(0, addExitCode);
        Assert.Contains("Added '{DAC-NEW}' to blacklist.", outWriter.ToString());
    }

    [Fact]
    public async Task RunAsync_Screenshot_WhenHeadless_OutputsClearErrorMessage()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["screenshot"]);

        Assert.Equal(1, exitCode);
        var error = errWriter.ToString();
        Assert.Contains("Screenshot capture requires the RigSwitch GUI application to be running in the system tray.", error);
    }

    [Fact]
    public async Task RunAsync_Doctor_OutputsAudit()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var doctorData = new Dictionary<string, object?>
        {
            ["currentProfile"] = "Desk",
            ["activePreset"] = "Productivity",
            ["audioMode"] = "Primary",
            ["watchdogActive"] = true,
            ["displays"] = new List<string> { "MON-1" },
            ["audioEndpoints"] = new List<string> { "Speakers" },
            ["blacklistedEndpoints"] = new List<string> { "{DAC-1}" },
            ["blacklistMatches"] = new List<string>()
        };

        _ipcClient.SendAsync(Arg.Any<IpcRequest>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-doctor", doctorData, "System diagnostic self-check completed."));

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["doctor"]);

        Assert.Equal(0, exitCode);
        var output = outWriter.ToString();
        Assert.Contains("RigSwitch Diagnostic Doctor", output);
        Assert.Contains("Desk", output);
        Assert.Contains("Active", output);
    }

    [Fact]
    public async Task RunAsync_Screenshot_ViaIpc_FormatsOutput()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var data = new Dictionary<string, object?>
        {
            ["path"] = @"C:\Temp\screenshot.png"
        };

        _ipcClient.SendAsync(Arg.Is<IpcRequest>(r => r.Command == IpcCommands.Screenshot && r.Args["view"] == "presets"), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-ss", data, "Screenshot saved to C:\\Temp\\screenshot.png"));

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["screenshot", "--view", "presets", "--out", @"C:\Temp\screenshot.png"]);

        Assert.Equal(0, exitCode);
        var output = outWriter.ToString();
        Assert.Contains(@"Screenshot saved to: C:\Temp\screenshot.png", output);
    }

    [Fact]
    public async Task RunAsync_Blacklist_Remove_Works()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _ipcClient.SendAsync(Arg.Is<IpcRequest>(r => r.Args["action"] == "remove" && r.Args["id"] == "{DAC-1}"), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-rem", new Dictionary<string, object?> { ["blacklistedEndpoints"] = new List<string>() }, "Removed '{DAC-1}' from blacklist."));

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["blacklist", "remove", "{DAC-1}"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Removed '{DAC-1}' from blacklist.", outWriter.ToString());
    }

    [Fact]
    public async Task RunAsync_Switch_Rig_OutputsFormattedResult()
    {
        _ipcClient.IsServerRunningAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var data = new Dictionary<string, object?>
        {
            ["profile"] = "SimRig",
            ["display"] = "VG34VQL3A",
            ["audio"] = "VG34VQL3A (Sim Rig)"
        };

        _ipcClient.SendAsync(Arg.Is<IpcRequest>(r => r.Args["target"] == "rig"), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(IpcResponse.Ok("req-rig", data));

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var controller = new CliController(_ipcClient, _headlessHandler, outWriter, errWriter);

        int exitCode = await controller.RunAsync(["switch", "rig"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Switched profile to: SimRig | Display: VG34VQL3A | Audio: VG34VQL3A (Sim Rig)", outWriter.ToString());
    }
}

