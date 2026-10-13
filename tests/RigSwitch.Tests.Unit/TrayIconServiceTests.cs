namespace RigSwitch.Tests.Unit;

using System.Drawing;
using System.Windows.Forms;
using NSubstitute;
using RigSwitch.App.Services;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using Xunit;

[Collection("WPF UI Tests")]
public sealed class TrayIconServiceTests : IDisposable
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsStorage;
    private TrayIconService? _trayIconService;

    public TrayIconServiceTests()
    {
        _coordinator = Substitute.For<IProfileSwitchCoordinator>();
        _coordinator.CurrentProfile.Returns(ProfileMode.Desk);
        _coordinator.CurrentAudioOutputMode.Returns(AudioOutputMode.Primary);
        _settingsStorage = Substitute.For<ISettingsStorageService>();
        _settingsStorage.LoadSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UserSettings()));
    }

    public void Dispose()
    {
        _trayIconService?.Dispose();
    }

    [Fact]
    public void LoadProfileIcon_Desk_ReturnsValidIcon()
    {
        using var icon = TrayIconService.LoadProfileIcon(ProfileMode.Desk);

        Assert.NotNull(icon);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
    }

    [Fact]
    public void LoadProfileIcon_SimRig_ReturnsValidIcon()
    {
        using var icon = TrayIconService.LoadProfileIcon(ProfileMode.SimRig);

        Assert.NotNull(icon);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
    }

    [Fact]
    public void GetAudioToggleMenuText_ReturnsExpectedTextForBothModes()
    {
        var primaryText = TrayIconService.GetAudioToggleMenuText(AudioOutputMode.Primary);
        var secondaryText = TrayIconService.GetAudioToggleMenuText(AudioOutputMode.Secondary);

        Assert.Contains("Speakers", primaryText);
        Assert.Contains("Toggle Audio", primaryText);

        Assert.Contains("Headset", secondaryText);
        Assert.Contains("Toggle Audio", secondaryText);
    }

    [Fact]
    public void Initialize_CreatesAudioToggleMenuItem()
    {
        _coordinator.CurrentAudioOutputMode.Returns(AudioOutputMode.Primary);
        _trayIconService = new TrayIconService(_coordinator, _settingsStorage);

        _trayIconService.Initialize(() => { });

        Assert.NotNull(_trayIconService.AudioToggleMenuItem);
        Assert.Contains("Speakers", _trayIconService.AudioToggleMenuItem.Text);
        Assert.Contains("Toggle Audio", _trayIconService.AudioToggleMenuItem.Text);
    }

    [Fact]
    public void UpdateAudioState_UpdatesMenuItemTextAndTooltip()
    {
        _coordinator.CurrentAudioOutputMode.Returns(AudioOutputMode.Primary);
        _trayIconService = new TrayIconService(_coordinator, _settingsStorage);
        _trayIconService.Initialize(() => { });

        _trayIconService.UpdateAudioState(AudioOutputMode.Secondary);

        Assert.NotNull(_trayIconService.AudioToggleMenuItem);
        Assert.Contains("Headset", _trayIconService.AudioToggleMenuItem.Text);
        Assert.NotNull(_trayIconService.NotifyIcon);
        Assert.Contains("Headset", _trayIconService.NotifyIcon.Text);
    }

    [Fact]
    public async Task AudioToggleMenuItem_Click_InvokesToggleAudioOutputMode()
    {
        _coordinator.CurrentAudioOutputMode.Returns(AudioOutputMode.Primary);
        _coordinator.ToggleAudioOutputModeAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(AudioOutputMode.Secondary));

        _trayIconService = new TrayIconService(_coordinator, _settingsStorage);
        _trayIconService.Initialize(() => { });

        Assert.NotNull(_trayIconService.AudioToggleMenuItem);

        // PerformClick on the STA thread
        _trayIconService.AudioToggleMenuItem.PerformClick();

        // Wait briefly for background Task.Run to invoke coordinator
        var timeout = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < timeout)
        {
            if (_coordinator.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IProfileSwitchCoordinator.ToggleAudioOutputModeAsync)))
            {
                break;
            }
            await Task.Delay(20);
        }

        await _coordinator.Received(1).ToggleAudioOutputModeAsync(Arg.Any<CancellationToken>());
    }
}
