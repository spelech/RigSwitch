namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Windows.CoreAudio;
using Xunit;

public sealed class AudioEndpointWatchdogServiceTests
{
    private readonly INativeAudioProvider _provider = Substitute.For<INativeAudioProvider>();
    private readonly IAudioEndpointDirector _director = Substitute.For<IAudioEndpointDirector>();
    private readonly ISettingsStorageService _storage = Substitute.For<ISettingsStorageService>();

    [Fact]
    public async Task WhenDefaultSwitchesToBlacklistedDevice_WatchdogRevertsImmediately()
    {
        var settings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{SHAKER-DAC}"],
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}" }]
        };
        _storage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        using var watchdog = new AudioEndpointWatchdogService(_provider, _director, _storage);
        watchdog.Start();

        // Simulate Windows setting default device to bass shaker DAC
        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eRender, ERole.eConsole, "{SHAKER-DAC}");

        // Allow debounce task to complete
        await Task.Delay(350);

        await _director.Received(1).SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenIntentionalSwitchNotified_WatchdogDoesNotRevert()
    {
        var settings = new UserSettings
        {
            EnforcePrimaryAudio = true,
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}", SecondaryAudioId = "{SONAR}" }]
        };
        _storage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        using var watchdog = new AudioEndpointWatchdogService(_provider, _director, _storage);
        watchdog.Start();

        watchdog.NotifyIntentionalSwitch("{SONAR}");

        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eRender, ERole.eConsole, "{SONAR}");

        await Task.Delay(350);

        await _director.DidNotReceive().SetDefaultPlaybackEndpointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenEnforcePrimaryAudioEnabled_AndUnauthorizedSwitchOccurs_WatchdogRevertsToPrimarySpeaker()
    {
        var settings = new UserSettings
        {
            EnforcePrimaryAudio = true,
            ActiveAudioOutputMode = AudioOutputMode.Primary,
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}", SecondaryAudioId = "{SONAR}" }]
        };
        _storage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        using var watchdog = new AudioEndpointWatchdogService(_provider, _director, _storage);
        watchdog.Start();

        // Background app like Sonar switches default audio without user notification
        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eRender, ERole.eConsole, "{SONAR}");

        await Task.Delay(350);

        await _director.Received(1).SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenEnforcePrimaryAudioDisabled_AndValidNonBlacklistedSwitchOccurs_WatchdogDoesNotRevert()
    {
        var settings = new UserSettings
        {
            EnforcePrimaryAudio = false,
            ActiveAudioOutputMode = AudioOutputMode.Primary,
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}" }]
        };
        _storage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        using var watchdog = new AudioEndpointWatchdogService(_provider, _director, _storage);
        watchdog.Start();

        // User manually switches in Windows Sound flyout to headphones
        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eRender, ERole.eConsole, "{HEADPHONES}");

        await Task.Delay(350);

        await _director.DidNotReceive().SetDefaultPlaybackEndpointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenCaptureEndpointChanges_WatchdogIgnoresEvent()
    {
        var settings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{SHAKER-DAC}"],
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}" }]
        };
        _storage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        using var watchdog = new AudioEndpointWatchdogService(_provider, _director, _storage);
        watchdog.Start();

        // Capture (microphone) change should be ignored
        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eCapture, ERole.eConsole, "{SHAKER-DAC}");

        await Task.Delay(350);

        await _director.DidNotReceive().SetDefaultPlaybackEndpointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenWatchdogStopped_EventsAreIgnored()
    {
        var settings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{SHAKER-DAC}"],
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}" }]
        };
        _storage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        using var watchdog = new AudioEndpointWatchdogService(_provider, _director, _storage);
        watchdog.Start();
        watchdog.Stop();

        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eRender, ERole.eConsole, "{SHAKER-DAC}");

        await Task.Delay(350);

        await _director.DidNotReceive().SetDefaultPlaybackEndpointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenMultipleEventsFiredRapidly_WatchdogDebouncesAndRevertsOnce()
    {
        var settings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{SHAKER-DAC}"],
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}" }]
        };
        _storage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        using var watchdog = new AudioEndpointWatchdogService(_provider, _director, _storage);
        watchdog.Start();

        // Windows fires both eConsole and eMultimedia rapidly
        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eRender, ERole.eConsole, "{SHAKER-DAC}");
        await Task.Delay(30);
        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eRender, ERole.eMultimedia, "{SHAKER-DAC}");

        await Task.Delay(350);

        await _director.Received(1).SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
    }
}
