namespace RigSwitch.Tests.Unit;

using System.Text.Json;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Storage;
using Xunit;

public sealed class JsonSettingsStorageServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public JsonSettingsStorageServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "RigSwitchTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp test directory
        }
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileDoesNotExist_ReturnsDefaultSettingsAndEnsuresDirectoryExists()
    {
        // Arrange
        var settingsDirectory = Path.Combine(_testDirectory, "NestedSettingsDir");
        var settingsPath = Path.Combine(settingsDirectory, "settings.json");
        var service = new JsonSettingsStorageService(settingsPath);

        Assert.False(Directory.Exists(settingsDirectory));

        // Act
        var settings = await service.LoadSettingsAsync();

        // Assert
        Assert.True(Directory.Exists(settingsDirectory));
        Assert.NotNull(settings);
        Assert.Equal(ProfileMode.Desk, settings.LastActiveProfile);
        Assert.Equal(string.Empty, settings.DeskMonitorId);
        Assert.Equal(string.Empty, settings.RigMonitorId);
        Assert.Equal(string.Empty, settings.DeskPrimaryAudioId);
        Assert.Equal(string.Empty, settings.DeskFallbackAudioId);
        Assert.Equal(string.Empty, settings.RigPrimaryAudioId);
        Assert.Equal("Ctrl+Alt+S", settings.ToggleHotkey);
        Assert.Equal("Ctrl+Alt+D", settings.DeskHotkey);
        Assert.Equal("Ctrl+Alt+R", settings.RigHotkey);
        Assert.True(settings.StartMinimizedToTray);
        Assert.True(settings.ShowToastNotifications);
        Assert.Empty(settings.CustomDeviceNames);
        Assert.Empty(settings.HiddenAudioEndpointIds);
    }

    [Fact]
    public async Task SaveSettingsAsync_WritesValidJsonAndCanBeReloaded()
    {
        // Arrange
        var settingsPath = Path.Combine(_testDirectory, "settings.json");
        var service = new JsonSettingsStorageService(settingsPath);

        var customSettings = new UserSettings
        {
            LastActiveProfile = ProfileMode.SimRig,
            DeskMonitorId = "CUSTOM_DESK_MONITOR",
            RigMonitorId = "CUSTOM_RIG_MONITOR",
            DeskPrimaryAudioId = "CUSTOM_DESK_AUDIO",
            DeskFallbackAudioId = "CUSTOM_FALLBACK_AUDIO",
            RigPrimaryAudioId = "CUSTOM_RIG_AUDIO",
            ToggleHotkey = "Ctrl+Shift+T",
            DeskHotkey = "Ctrl+Shift+D",
            RigHotkey = "Ctrl+Shift+R",
            StartMinimizedToTray = false,
            ShowToastNotifications = false,
            CustomDeviceNames = new Dictionary<string, string>
            {
                ["CUSTOM_DESK_MONITOR"] = "Custom Desk OLED",
                ["CUSTOM_RIG_MONITOR"] = "Custom Rig Ultrawide"
            },
            HiddenAudioEndpointIds =
            [
                "CUSTOM_HIDDEN_DEV_1",
                "CUSTOM_HIDDEN_DEV_2"
            ]
        };

        // Act
        await service.SaveSettingsAsync(customSettings);

        // Assert
        Assert.True(File.Exists(settingsPath));
        var rawJson = await File.ReadAllTextAsync(settingsPath);
        Assert.False(string.IsNullOrWhiteSpace(rawJson));

        var reloaded = await service.LoadSettingsAsync();
        Assert.NotNull(reloaded);
        Assert.Equal(customSettings.LastActiveProfile, reloaded.LastActiveProfile);
        Assert.Equal(customSettings.DeskMonitorId, reloaded.DeskMonitorId);
        Assert.Equal(customSettings.RigMonitorId, reloaded.RigMonitorId);
        Assert.Equal(customSettings.DeskPrimaryAudioId, reloaded.DeskPrimaryAudioId);
        Assert.Equal(customSettings.DeskFallbackAudioId, reloaded.DeskFallbackAudioId);
        Assert.Equal(customSettings.RigPrimaryAudioId, reloaded.RigPrimaryAudioId);
        Assert.Equal(customSettings.ToggleHotkey, reloaded.ToggleHotkey);
        Assert.Equal(customSettings.DeskHotkey, reloaded.DeskHotkey);
        Assert.Equal(customSettings.RigHotkey, reloaded.RigHotkey);
        Assert.Equal(customSettings.StartMinimizedToTray, reloaded.StartMinimizedToTray);
        Assert.Equal(customSettings.ShowToastNotifications, reloaded.ShowToastNotifications);
        Assert.Equal<KeyValuePair<string, string>>(customSettings.CustomDeviceNames, reloaded.CustomDeviceNames);
        Assert.Equal(customSettings.HiddenAudioEndpointIds, reloaded.HiddenAudioEndpointIds);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileCorrupt_FallsBackToDefaultsWithoutCrashing()
    {
        // Arrange
        var settingsPath = Path.Combine(_testDirectory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{ corrupt json syntax: [}");

        var service = new JsonSettingsStorageService(settingsPath);

        // Act
        var settings = await service.LoadSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(ProfileMode.Desk, settings.LastActiveProfile);
        Assert.Equal(string.Empty, settings.DeskMonitorId);
        Assert.Equal(string.Empty, settings.RigMonitorId);
    }

    [Fact]
    public async Task SaveSettingsAsync_ConcurrentCalls_AreSynchronizedWithoutCorruption()
    {
        // Arrange
        var settingsPath = Path.Combine(_testDirectory, "concurrent_settings.json");
        var service = new JsonSettingsStorageService(settingsPath);

        const int concurrencyLevel = 20;
        var tasks = new List<Task>();

        // Act
        for (var i = 0; i < concurrencyLevel; i++)
        {
            var index = i;
            var settings = new UserSettings
            {
                LastActiveProfile = (index % 2 == 0) ? ProfileMode.Desk : ProfileMode.SimRig,
                DeskMonitorId = $"MONITOR_{index}",
                RigMonitorId = $"RIG_MONITOR_{index}"
            };

            tasks.Add(Task.Run(() => service.SaveSettingsAsync(settings)));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.True(File.Exists(settingsPath));
        var loaded = await service.LoadSettingsAsync();
        Assert.NotNull(loaded);
        Assert.StartsWith("MONITOR_", loaded.DeskMonitorId, StringComparison.Ordinal);
        Assert.StartsWith("RIG_MONITOR_", loaded.RigMonitorId, StringComparison.Ordinal);

        // Temp file should not linger after completion
        var tempFile = $"{settingsPath}.tmp";
        Assert.False(File.Exists(tempFile));
    }

    [Fact]
    public void Constructor_WithNullOrEmptyPath_UsesDefaultAppDataPath()
    {
        // Arrange & Act
        var serviceWithNull = new JsonSettingsStorageService(null);
        var serviceWithEmpty = new JsonSettingsStorageService("   ");
        var serviceDefault = new JsonSettingsStorageService();

        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RigSwitch",
            "settings.json");

        // Assert
        Assert.Equal(expectedPath, serviceWithNull.SettingsFilePath);
        Assert.Equal(expectedPath, serviceWithEmpty.SettingsFilePath);
        Assert.Equal(expectedPath, serviceDefault.SettingsFilePath);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var settingsPath = Path.Combine(_testDirectory, "settings.json");
        var service = new JsonSettingsStorageService(settingsPath);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.LoadSettingsAsync(cts.Token));
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var settingsPath = Path.Combine(_testDirectory, "settings.json");
        var service = new JsonSettingsStorageService(settingsPath);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveSettingsAsync(new UserSettings(), cts.Token));
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenLegacyJsonWithoutPresets_AutoMigratesToThreePresetsWithLegacyValuesInPresetZero()
    {
        // Arrange
        var settingsPath = Path.Combine(_testDirectory, "legacy_settings.json");
        var legacyJson = """
        {
            "LastActiveProfile": 1,
            "DeskMonitorId": "LEGACY_DESK_MONITOR",
            "RigMonitorId": "LEGACY_RIG_MONITOR",
            "DeskPrimaryAudioId": "LEGACY_DESK_AUDIO",
            "DeskFallbackAudioId": "LEGACY_FALLBACK_AUDIO",
            "RigPrimaryAudioId": "LEGACY_RIG_AUDIO",
            "ToggleHotkey": "Ctrl+Shift+T"
        }
        """;
        await File.WriteAllTextAsync(settingsPath, legacyJson);
        var service = new JsonSettingsStorageService(settingsPath);

        // Act
        var loaded = await service.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(ProfileMode.SimRig, loaded.LastActiveProfile);
        Assert.Equal("Ctrl+Shift+T", loaded.ToggleHotkey);

        // Desk presets populated and legacy values mapped to preset 0
        Assert.NotNull(loaded.DeskPresets);
        Assert.Equal(3, loaded.DeskPresets.Count);
        Assert.Equal("Work / Primary", loaded.DeskPresets[0].Name);
        Assert.Equal("LEGACY_DESK_MONITOR", loaded.DeskPresets[0].TargetMonitorId);
        Assert.Equal("LEGACY_DESK_AUDIO", loaded.DeskPresets[0].PrimaryAudioId);
        Assert.Equal("LEGACY_FALLBACK_AUDIO", loaded.DeskPresets[0].FallbackAudioId);
        Assert.Equal("Media / Casual", loaded.DeskPresets[1].Name);
        Assert.Equal("Clean Desk", loaded.DeskPresets[2].Name);

        // Rig presets populated and legacy values mapped to preset 0
        Assert.NotNull(loaded.RigPresets);
        Assert.Equal(3, loaded.RigPresets.Count);
        Assert.Equal("GT3 / Circuit", loaded.RigPresets[0].Name);
        Assert.Equal("LEGACY_RIG_MONITOR", loaded.RigPresets[0].TargetMonitorId);
        Assert.Equal("LEGACY_RIG_AUDIO", loaded.RigPresets[0].PrimaryAudioId);
        Assert.Equal("Rally / Drift", loaded.RigPresets[1].Name);
        Assert.Equal("Flight / Space", loaded.RigPresets[2].Name);

        // Backward compatibility properties redirect correctly
        Assert.Equal("LEGACY_DESK_MONITOR", loaded.DeskMonitorId);
        Assert.Equal("LEGACY_RIG_MONITOR", loaded.RigMonitorId);
        Assert.Equal("LEGACY_DESK_AUDIO", loaded.DeskPrimaryAudioId);
        Assert.Equal("LEGACY_FALLBACK_AUDIO", loaded.DeskFallbackAudioId);
        Assert.Equal("LEGACY_RIG_AUDIO", loaded.RigPrimaryAudioId);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenLegacyJsonWithEmptyOrNullPresets_PopulatesThreeDefaultsAndCopiesLegacyValues()
    {
        // Arrange
        var settingsPath = Path.Combine(_testDirectory, "empty_presets_settings.json");
        var legacyJson = """
        {
            "DeskMonitorId": "EXPLICIT_DESK_MON",
            "RigMonitorId": "EXPLICIT_RIG_MON",
            "DeskPrimaryAudioId": "EXPLICIT_DESK_AUD",
            "DeskFallbackAudioId": "EXPLICIT_FALLBACK",
            "RigPrimaryAudioId": "EXPLICIT_RIG_AUD",
            "DeskPresets": [],
            "RigPresets": null
        }
        """;
        await File.WriteAllTextAsync(settingsPath, legacyJson);
        var service = new JsonSettingsStorageService(settingsPath);

        // Act
        var loaded = await service.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.DeskPresets.Count);
        Assert.Equal(3, loaded.RigPresets.Count);
        Assert.Equal("EXPLICIT_DESK_MON", loaded.DeskPresets[0].TargetMonitorId);
        Assert.Equal("EXPLICIT_RIG_MON", loaded.RigPresets[0].TargetMonitorId);
        Assert.Equal("EXPLICIT_DESK_AUD", loaded.DeskPresets[0].PrimaryAudioId);
        Assert.Equal("EXPLICIT_FALLBACK", loaded.DeskPresets[0].FallbackAudioId);
        Assert.Equal("EXPLICIT_RIG_AUD", loaded.RigPresets[0].PrimaryAudioId);
    }
}
