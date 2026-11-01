namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.App.Services;
using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using Xunit;

public sealed class MainSettingsViewModelAudioTests : IDisposable
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsService;
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly TrayIconService _trayService;

    public MainSettingsViewModelAudioTests()
    {
        _coordinator = Substitute.For<IProfileSwitchCoordinator>();
        _settingsService = Substitute.For<ISettingsStorageService>();
        _displayService = Substitute.For<IDisplayConfigurationService>();
        _audioDirector = Substitute.For<IAudioEndpointDirector>();
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _trayService = new TrayIconService(_coordinator, _settingsService);
    }

    public void Dispose()
    {
        _trayService.Dispose();
    }


    [Fact]
    public async Task RegisterGlobalHotkeys_WhenAudioToggleHotkeyConfigured_RegistersAndInvokesToggleAudio()
    {
        // Arrange
        var settings = new UserSettings
        {
            AudioToggleHotkey = "Ctrl+Alt+A"
        };

        Action? registeredAction = null;
        _hotkeyService.RegisterHotkey("Ctrl+Alt+A", Arg.Do<Action>(a => registeredAction = a)).Returns(true);
        _coordinator.ToggleAudioOutputModeAsync().Returns(Task.FromResult(AudioOutputMode.Secondary));

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            _trayService);

        // Act
        vm.RegisterGlobalHotkeys(settings);

        // Assert: hotkey was registered
        _hotkeyService.Received(1).RegisterHotkey("Ctrl+Alt+A", Arg.Any<Action>());
        Assert.NotNull(registeredAction);

        // Act: invoke the registered callback
        registeredAction!();

        // Assert: ToggleAudioOutputModeAsync was invoked
        await _coordinator.Received(1).ToggleAudioOutputModeAsync();
    }

    [Fact]
    public void RegisterGlobalHotkeys_WhenAudioToggleHotkeyEmpty_DoesNotRegister()
    {
        // Arrange
        var settings = new UserSettings
        {
            AudioToggleHotkey = string.Empty
        };

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            _trayService);

        // Act
        vm.RegisterGlobalHotkeys(settings);

        // Assert
        _hotkeyService.DidNotReceive().RegisterHotkey(string.Empty, Arg.Any<Action>());
    }

    [Fact]
    public void RegisterGlobalHotkeys_WhenAudioToggleHotkeyFails_RecordsWarning()
    {
        // Arrange
        var settings = new UserSettings
        {
            AudioToggleHotkey = "Ctrl+Alt+A"
        };

        _hotkeyService.RegisterHotkey("Ctrl+Alt+A", Arg.Any<Action>()).Returns(false);

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            _trayService);

        // Act
        vm.RegisterGlobalHotkeys(settings);

        // Assert
        Assert.Contains("Ctrl+Alt+A", vm.StatusMessage);
        Assert.StartsWith("Warning: Failed to register hotkey", vm.StatusMessage);
    }

    [Fact]
    public void AudioToggleHotkey_Property_GetterAndSetterWork()
    {
        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            _trayService);

        vm.AudioToggleHotkey = "Ctrl+Shift+A";
        Assert.Equal("Ctrl+Shift+A", vm.AudioToggleHotkey);
    }
}
