namespace RigSwitch.Tests.Unit;

using System.Net;
using System.Net.Http;
using System.Text;
using NSubstitute;
using RigSwitch.App.Services;
using RigSwitch.App.ViewModels;
using RigSwitch.App.Views;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Services;
using Xunit;

public class UpdateCheckServiceTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V2.0.0", "2.0.0")]
    [InlineData("  1.0.0  ", "1.0.0")]
    [InlineData("", "1.0.0")]
    [InlineData(null, "1.0.0")]
    public void NormalizeVersionString_SanitizesVersionInput(string? input, string expected)
    {
        string result = UpdateCheckService.NormalizeVersionString(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("v2.0.0", "v1.9.9", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    [InlineData("1.0.1-beta", "1.0.0", true)]
    public void IsVersionNewer_CorrectlyComparesVersions(string latest, string current, bool expected)
    {
        bool result = UpdateCheckService.IsVersionNewer(latest, current);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNewerVersionExists_ReturnsUpdateInfoWithAvailableTrue()
    {
        var jsonPayload = """
        {
            "tag_name": "v1.2.0",
            "html_url": "https://github.com/spelech/RigSwitch/releases/tag/v1.2.0",
            "body": "Major stability improvements and new features.",
            "assets": [
                {
                    "name": "RigSwitch-Setup-v1.2.0.exe",
                    "browser_download_url": "https://github.com/spelech/RigSwitch/releases/download/v1.2.0/RigSwitch-Setup-v1.2.0.exe"
                }
            ]
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonPayload);
        using var httpClient = new HttpClient(handler);
        using var service = new UpdateCheckService(httpClient, "https://api.github.com/fake-release");

        var info = await service.CheckForUpdatesAsync("1.0.0");

        Assert.True(info.IsUpdateAvailable);
        Assert.Equal("1.0.0", info.CurrentVersion);
        Assert.Equal("1.2.0", info.LatestVersion);
        Assert.Equal("Major stability improvements and new features.", info.ReleaseNotes);
        Assert.Equal("https://github.com/spelech/RigSwitch/releases/download/v1.2.0/RigSwitch-Setup-v1.2.0.exe", info.DownloadUrl);
        Assert.Equal("https://github.com/spelech/RigSwitch/releases/tag/v1.2.0", info.HtmlUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenCurrentVersionIsLatest_ReturnsUpdateInfoWithAvailableFalse()
    {
        var jsonPayload = """
        {
            "tag_name": "v1.0.0",
            "html_url": "https://github.com/spelech/RigSwitch/releases/tag/v1.0.0",
            "body": "Initial release.",
            "assets": []
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonPayload);
        using var httpClient = new HttpClient(handler);
        using var service = new UpdateCheckService(httpClient, "https://api.github.com/fake-release");

        var info = await service.CheckForUpdatesAsync("1.0.0");

        Assert.False(info.IsUpdateAvailable);
        Assert.Equal("1.0.0", info.CurrentVersion);
        Assert.Equal("1.0.0", info.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenHttpErrorOccurs_ReturnsErrorMsgAndAvailableFalse()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.NotFound, "");
        using var httpClient = new HttpClient(handler);
        using var service = new UpdateCheckService(httpClient, "https://api.github.com/fake-release");

        var info = await service.CheckForUpdatesAsync("1.0.0");

        Assert.False(info.IsUpdateAvailable);
        Assert.Contains("404", info.ErrorMessage);
    }

    [Fact]
    public async Task HandleUserChoiceAsync_WhenDelaySelected_SetsSkippedUntilTimestamp()
    {
        var settings = new UserSettings();
        var storage = Substitute.For<ISettingsStorageService>();
        var info = new UpdateInfo(true, "1.0.0", "1.1.0", "Notes", "http://dl", "http://html");

        var msg = await UpdateCheckHandler.HandleUserChoiceAsync(
            UpdateUserChoice.Delay,
            info,
            settings,
            storage,
            _ => { });

        Assert.NotNull(settings.UpdateCheckSkippedUntil);
        Assert.True(settings.UpdateCheckSkippedUntil > DateTime.UtcNow.AddHours(23));
        await storage.Received(1).SaveSettingsAsync(settings, Arg.Any<CancellationToken>());
        Assert.Contains("24 hours", msg);
    }

    [Fact]
    public async Task HandleUserChoiceAsync_WhenIgnoreSelected_UpdatesIgnoredReleaseVersion()
    {
        var settings = new UserSettings();
        var storage = Substitute.For<ISettingsStorageService>();
        var info = new UpdateInfo(true, "1.0.0", "1.5.0", "Notes", "http://dl", "http://html");
        string updatedTag = string.Empty;

        var msg = await UpdateCheckHandler.HandleUserChoiceAsync(
            UpdateUserChoice.Ignore,
            info,
            settings,
            storage,
            tag => updatedTag = tag);

        Assert.Equal("1.5.0", settings.IgnoredReleaseVersion);
        Assert.Equal("1.5.0", updatedTag);
        await storage.Received(1).SaveSettingsAsync(settings, Arg.Any<CancellationToken>());
        Assert.Contains("1.5.0", msg);
    }

    [Fact]
    public async Task MainSettingsViewModel_CheckForUpdatesManualAsync_WhenUpToDate_UpdatesStatusMessage()
    {
        var mockCheckService = Substitute.For<IUpdateCheckService>();
        mockCheckService.CheckForUpdatesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateInfo(false, "1.0.0", "1.0.0", "", "", ""));

        var coordinator = Substitute.For<IProfileSwitchCoordinator>();
        var settingsStorage = Substitute.For<ISettingsStorageService>();
        settingsStorage.LoadSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new UserSettings());

        var trayService = new TrayIconService(coordinator, settingsStorage);

        using var vm = new MainSettingsViewModel(
            coordinator,
            settingsStorage,
            Substitute.For<IDisplayConfigurationService>(),
            Substitute.For<IAudioEndpointDirector>(),
            Substitute.For<IGlobalHotkeyService>(),
            trayService,
            mockCheckService);

        await vm.CheckForUpdatesManualAsync();

        Assert.Contains("latest version", vm.StatusMessage);
    }

    [Fact]
    public async Task MainSettingsViewModel_CheckForUpdatesAutoAsync_WhenCheckSkipped_DoesNotInvokeCheck()
    {
        var mockCheckService = Substitute.For<IUpdateCheckService>();
        var settingsStorage = Substitute.For<ISettingsStorageService>();
        var settings = new UserSettings
        {
            EnableAutoUpdateCheck = true,
            UpdateCheckSkippedUntil = DateTime.UtcNow.AddHours(5)
        };
        settingsStorage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var coordinator = Substitute.For<IProfileSwitchCoordinator>();
        var trayService = new TrayIconService(coordinator, settingsStorage);

        using var vm = new MainSettingsViewModel(
            coordinator,
            settingsStorage,
            Substitute.For<IDisplayConfigurationService>(),
            Substitute.For<IAudioEndpointDirector>(),
            Substitute.For<IGlobalHotkeyService>(),
            trayService,
            mockCheckService);

        await vm.LoadAsync();
        await vm.CheckForUpdatesAutoAsync();

        await mockCheckService.DidNotReceive().CheckForUpdatesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
