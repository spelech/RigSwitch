namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.App.Services;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Ipc;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Windows.CoreAudio;
using Xunit;

public sealed class IpcCommandHandlerTests
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsService;
    private readonly IAudioEndpointWatchdogService _watchdogService;
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly Func<string?, string?, Task<string>> _screenshotCapturer;
    private readonly UserSettings _testSettings;

    public IpcCommandHandlerTests()
    {
        _coordinator = Substitute.For<IProfileSwitchCoordinator>();
        _settingsService = Substitute.For<ISettingsStorageService>();
        _watchdogService = Substitute.For<IAudioEndpointWatchdogService>();
        _displayService = Substitute.For<IDisplayConfigurationService>();
        _audioDirector = Substitute.For<IAudioEndpointDirector>();
        _screenshotCapturer = Substitute.For<Func<string?, string?, Task<string>>>();

        _testSettings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{DAC-SHAKER-1}"]
        };

        _settingsService.LoadSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(_testSettings);

        _coordinator.CurrentProfile.Returns(ProfileMode.Desk);
        _coordinator.CurrentAudioOutputMode.Returns(AudioOutputMode.Primary);
        _coordinator.CurrentPreset.Returns(new WorkstationPreset { Name = "Dual Work" });

        _watchdogService.IsRunning.Returns(true);

        _displayService.EnumerateDisplaysAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DisplayDeviceInfo>
            {
                new("MON1", "PATH1", "Asus ROG", "GPU0", isActive: true, isPrimary: true)
            });

        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AudioEndpointInfo>
            {
                new("{DAC-SHAKER-1}", "SimHub Shaker", "USB Audio", DevicePresenceState.Active, isDefaultPlayback: false, isDefaultCommunications: false),
                new("{SPEAKERS-1}", "Main Speakers", "Realtek", DevicePresenceState.Active, isDefaultPlayback: true, isDefaultCommunications: false)
            });
    }

    [Fact]
    public async Task HandleRequestAsync_Status_ReturnsCompleteDictionary()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Status);
        var response = await handler.HandleRequestAsync(request);

        Assert.True(response.Success);
        Assert.Equal(request.Id, response.Id);
        Assert.NotNull(response.Data);

        Assert.Equal("Desk", response.Data["profile"]?.ToString());
        Assert.Equal("Primary", response.Data["audioMode"]?.ToString());
        Assert.Equal(true, response.Data["watchdogActive"]);
        Assert.Equal("Dual Work", response.Data["activePresetName"]?.ToString());
        Assert.NotNull(response.Data["displays"]);
        Assert.NotNull(response.Data["audioEndpoints"]);
        Assert.NotNull(response.Data["blacklistedEndpoints"]);
    }

    [Theory]
    [InlineData("desk", ProfileMode.Desk)]
    [InlineData("Desk", ProfileMode.Desk)]
    [InlineData("rig", ProfileMode.SimRig)]
    [InlineData("SimRig", ProfileMode.SimRig)]
    public async Task HandleRequestAsync_Switch_CallsCoordinatorWithCorrectProfile(string target, ProfileMode expectedProfile)
    {
        _coordinator.SwitchProfileAsync(expectedProfile, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Switch, new Dictionary<string, string?>
        {
            ["target"] = target
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.True(response.Success);
        await _coordinator.Received(1).SwitchProfileAsync(expectedProfile, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequestAsync_Switch_WhenTargetInvalid_ReturnsFailure()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Switch, new Dictionary<string, string?>
        {
            ["target"] = "invalid-profile"
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.False(response.Success);
        Assert.Contains("Invalid", response.Message);
        await _coordinator.DidNotReceive().SwitchProfileAsync(Arg.Any<ProfileMode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequestAsync_Audio_Toggle_CallsCoordinator()
    {
        _coordinator.ToggleAudioOutputModeAsync(Arg.Any<CancellationToken>())
            .Returns(AudioOutputMode.Secondary);

        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Audio, new Dictionary<string, string?>
        {
            ["action"] = "toggle"
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Secondary", response.Data["audioMode"]?.ToString());
        await _coordinator.Received(1).ToggleAudioOutputModeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequestAsync_Audio_ExplicitAction_TogglesOnlyWhenNeeded()
    {
        // Currently in Primary
        _coordinator.CurrentAudioOutputMode.Returns(AudioOutputMode.Primary);
        _coordinator.ToggleAudioOutputModeAsync(Arg.Any<CancellationToken>())
            .Returns(AudioOutputMode.Secondary);

        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        // Requesting speakers when already in Primary should not toggle
        var res1 = await handler.HandleRequestAsync(new IpcRequest(IpcCommands.Audio, new() { ["action"] = "speakers" }));
        Assert.True(res1.Success);
        await _coordinator.DidNotReceive().ToggleAudioOutputModeAsync(Arg.Any<CancellationToken>());

        // Requesting chat when in Primary should toggle
        var res2 = await handler.HandleRequestAsync(new IpcRequest(IpcCommands.Audio, new() { ["action"] = "chat" }));
        Assert.True(res2.Success);
        await _coordinator.Received(1).ToggleAudioOutputModeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequestAsync_Blacklist_List_ReturnsBlacklistedEndpoints()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Blacklist, new Dictionary<string, string?>
        {
            ["action"] = "list"
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        var list = Assert.IsAssignableFrom<IEnumerable<string>>(response.Data["blacklistedEndpoints"]);
        Assert.Contains("{DAC-SHAKER-1}", list);
    }

    [Fact]
    public async Task HandleRequestAsync_Blacklist_Add_UpdatesSettingsAndPersists()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Blacklist, new Dictionary<string, string?>
        {
            ["action"] = "add",
            ["id"] = "{DAC-SHAKER-2}"
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.True(response.Success);
        Assert.Contains("{DAC-SHAKER-2}", _testSettings.BlacklistedAudioEndpointIds);
        await _settingsService.Received(1).SaveSettingsAsync(_testSettings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequestAsync_Blacklist_Remove_RemovesFromSettingsAndPersists()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Blacklist, new Dictionary<string, string?>
        {
            ["action"] = "remove",
            ["id"] = "{DAC-SHAKER-1}"
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.True(response.Success);
        Assert.DoesNotContain("{DAC-SHAKER-1}", _testSettings.BlacklistedAudioEndpointIds);
        await _settingsService.Received(1).SaveSettingsAsync(_testSettings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequestAsync_Screenshot_InvokesCapturerAndReturnsPath()
    {
        _screenshotCapturer.Invoke("presets", @"C:\temp\screen.png")
            .Returns(@"C:\temp\screen.png");

        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Screenshot, new Dictionary<string, string?>
        {
            ["view"] = "presets",
            ["out"] = @"C:\temp\screen.png"
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(@"C:\temp\screen.png", response.Data["path"]?.ToString());
        await _screenshotCapturer.Received(1).Invoke("presets", @"C:\temp\screen.png");
    }

    [Fact]
    public async Task HandleRequestAsync_Screenshot_WhenCapturerNull_ReturnsClearFailure()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            screenshotCapturer: null);

        var request = new IpcRequest(IpcCommands.Screenshot);
        var response = await handler.HandleRequestAsync(request);

        Assert.False(response.Success);
        Assert.Contains("not available", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleRequestAsync_Doctor_AggregatesHardwareAndWatchdogStatus()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Doctor);
        var response = await handler.HandleRequestAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        Assert.NotNull(response.Data["displays"]);
        Assert.NotNull(response.Data["audioEndpoints"]);
        Assert.Equal(true, response.Data["watchdogActive"]);
        Assert.NotNull(response.Data["blacklistedEndpoints"]);
        Assert.NotNull(response.Data["blacklistMatches"]);
    }

    [Fact]
    public async Task HandleRequestAsync_Switch_WhenCoordinatorFails_ReturnsFailure()
    {
        _coordinator.SwitchProfileAsync(ProfileMode.Desk, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Switch, new Dictionary<string, string?>
        {
            ["target"] = "desk"
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.False(response.Success);
        Assert.Contains("Failed to switch profile", response.Message);
    }

    [Fact]
    public async Task HandleRequestAsync_Audio_InvalidAction_ReturnsFailure()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Audio, new Dictionary<string, string?>
        {
            ["action"] = "invalid-action"
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.False(response.Success);
        Assert.Contains("Invalid audio action", response.Message);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    public async Task HandleRequestAsync_Blacklist_MissingId_ReturnsFailure(string action)
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Blacklist, new Dictionary<string, string?>
        {
            ["action"] = action
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.False(response.Success);
        Assert.Contains("Endpoint ID is required", response.Message);
    }

    [Fact]
    public async Task HandleRequestAsync_Blacklist_InvalidAction_ReturnsFailure()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Blacklist, new Dictionary<string, string?>
        {
            ["action"] = "unknown-action"
        });

        var response = await handler.HandleRequestAsync(request);

        Assert.False(response.Success);
        Assert.Contains("Invalid blacklist action", response.Message);
    }

    [Fact]
    public async Task HandleRequestAsync_Screenshot_WhenCapturerThrows_ReturnsFailure()
    {
        _screenshotCapturer.Invoke(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns<string>(_ => throw new InvalidOperationException("WPF render failure"));

        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(IpcCommands.Screenshot);
        var response = await handler.HandleRequestAsync(request);

        Assert.False(response.Success);
        Assert.Contains("WPF render failure", response.Message);
    }

    [Fact]
    public async Task HandleRequestAsync_EmptyCommand_ReturnsFailure()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest(string.Empty);
        var response = await handler.HandleRequestAsync(request);

        Assert.False(response.Success);
        Assert.Contains("Command cannot be empty", response.Message);
    }

    [Fact]
    public async Task HandleRequestAsync_UnknownCommand_ReturnsFailure()
    {
        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        var request = new IpcRequest("nonexistent-cmd");
        var response = await handler.HandleRequestAsync(request);

        Assert.False(response.Success);
        Assert.Contains("Unknown command", response.Message);
    }

    [Fact]
    public async Task NamedPipe_IntegrationWithCommandHandler_DispatchesAndRespondsCorrectly()
    {
        var pipeName = $"RigSwitch-HandlerTest-{Guid.NewGuid():N}";
        await using var server = new RigSwitch.Infrastructure.Ipc.NamedPipeIpcServer(pipeName);

        var handler = new IpcCommandHandler(
            _coordinator,
            _settingsService,
            _watchdogService,
            _displayService,
            _audioDirector,
            _screenshotCapturer);

        server.RegisterHandler(req => handler.HandleRequestAsync(req));
        await server.StartAsync();

        var client = new RigSwitch.Infrastructure.Ipc.NamedPipeIpcClient(pipeName);

        // 1. Status request
        var statusRes = await client.SendAsync(new IpcRequest(IpcCommands.Status), TimeSpan.FromSeconds(2));
        Assert.True(statusRes.Success);
        Assert.Equal("Desk", statusRes.Data?["profile"]?.ToString());

        // 2. Doctor request
        var doctorRes = await client.SendAsync(new IpcRequest(IpcCommands.Doctor), TimeSpan.FromSeconds(2));
        Assert.True(doctorRes.Success);
        Assert.NotNull(doctorRes.Data?["displays"]);

        await server.StopAsync();
    }
}
