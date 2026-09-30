# Preserve Offline and Disconnected Devices Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve user-configured monitors and audio devices when powered off or disconnected, displaying their last-known friendly names and preventing them from being wiped from presets.

**Architecture:** Add persistent `CachedDeviceNames` to `UserSettings` updated on enumeration; enhance `DeviceOptionResolver` to format offline device options with cached names and preserve inactive CoreAudio endpoints; update `MainSettingsViewModel.Monitors.cs` to populate offline monitor tiles from presets so they persist across synchronization; and update UI tile styling for offline status.

**Tech Stack:** .NET 10, C# 13, WPF, XUnit, NSubstitute.

## Global Constraints

- Always run linting and typechecking after making code changes: `npm run lint` and `npx tsc --noEmit`.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is enabled on all projects.
- Preserve all existing comments and docstrings.

---

### Task 1: Add CachedDeviceNames to UserSettings

**Files:**
- Modify: `src/RigSwitch.Core/Models/UserSettings.cs`
- Modify: `tests/RigSwitch.Tests.Unit/WorkstationPresetTests.cs` (or create `tests/RigSwitch.Tests.Unit/UserSettingsTests.cs`)

**Interfaces:**
- Consumes: Nothing
- Produces: `UserSettings.CachedDeviceNames: Dictionary<string, string>`

- [ ] **Step 1: Write the failing test**
Create a test in `tests/RigSwitch.Tests.Unit/UserSettingsTests.cs`:
```csharp
namespace RigSwitch.Tests.Unit;

using System.Text.Json;
using RigSwitch.Core.Models;
using Xunit;

public sealed class UserSettingsTests
{
    [Fact]
    public void CachedDeviceNames_DefaultsToEmptyCaseInsensitiveDictionary()
    {
        var settings = new UserSettings();
        Assert.NotNull(settings.CachedDeviceNames);
        Assert.Empty(settings.CachedDeviceNames);

        settings.CachedDeviceNames["MONITOR-1"] = "Main Display";
        Assert.Equal("Main Display", settings.CachedDeviceNames["monitor-1"]);
    }

    [Fact]
    public void CachedDeviceNames_SerializesAndDeserializesCorrectly()
    {
        var settings = new UserSettings();
        settings.CachedDeviceNames["SAM0F12"] = "Samsung Odyssey";
        settings.CachedDeviceNames["{guid-1}"] = "SteelSeries Sonar";

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<UserSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal("Samsung Odyssey", restored.CachedDeviceNames["sam0f12"]);
        Assert.Equal("SteelSeries Sonar", restored.CachedDeviceNames["{guid-1}"]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test --filter "FullyQualifiedName~UserSettingsTests"`
Expected: FAIL due to missing `CachedDeviceNames` property.

- [ ] **Step 3: Write minimal implementation**
In `src/RigSwitch.Core/Models/UserSettings.cs`:
```csharp
    /// <summary>
    /// Gets or sets cached friendly display names for known devices, keyed by hardware or endpoint ID.
    /// </summary>
    public Dictionary<string, string> CachedDeviceNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
```

- [ ] **Step 4: Run test to verify it passes**
Run: `dotnet test --filter "FullyQualifiedName~UserSettingsTests"`
Expected: PASS

- [ ] **Step 5: Run lint and typecheck**
Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 6: Commit**
```bash
git add src/RigSwitch.Core/Models/UserSettings.cs tests/RigSwitch.Tests.Unit/UserSettingsTests.cs
git commit -m "feat: add persistent CachedDeviceNames dictionary to UserSettings"
```

---

### Task 2: Enhance DeviceOptionResolver for Inactive and Offline Devices

**Files:**
- Modify: `src/RigSwitch.App/ViewModels/DeviceOptionResolver.cs`
- Create/Modify: `tests/RigSwitch.Tests.Unit/DeviceOptionResolverTests.cs`

**Interfaces:**
- Consumes: `UserSettings.CachedDeviceNames`, `UserSettings.CustomDeviceNames`
- Produces:
  - `DeviceOptionResolver.FormatOfflineAudioLabel(string id, string? detectedName, IDictionary<string, string>? customNames, IDictionary<string, string>? cachedNames)`
  - `DeviceOptionResolver.FormatOfflineDisplayLabel(string id, IDictionary<string, string>? customNames, IDictionary<string, string>? cachedNames)`
  - `DeviceOptionResolver.ResolveAudioOption(...)` (overload taking custom and cached names)
  - `DeviceOptionResolver.EnsureDisplayOption(...)` (overload taking custom and cached names)

- [ ] **Step 1: Write the failing tests**
Create `tests/RigSwitch.Tests.Unit/DeviceOptionResolverTests.cs`:
```csharp
namespace RigSwitch.Tests.Unit;

using System.Collections.ObjectModel;
using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

public sealed class DeviceOptionResolverTests
{
    [Fact]
    public void ResolveAudioOption_WhenEndpointIsInactive_AddsToOptionsWithFriendlyNameAndDisconnectedTag()
    {
        // Arrange
        const string targetId = "{0.0.0.00000000}.{11111111-2222-3333-4444-555555555555}";
        var endpoints = new List<AudioEndpointInfo>
        {
            new(targetId, "Headphones", "Realtek", DevicePresenceState.Unplugged, false, false)
        };
        var options = new ObservableCollection<DeviceSelectionOption>();
        var customNames = new Dictionary<string, string> { [targetId] = "My Headset" };
        var cachedNames = new Dictionary<string, string>();

        // Act
        var result = DeviceOptionResolver.ResolveAudioOption(targetId, endpoints, options, customNames, cachedNames);

        // Assert
        Assert.Equal(targetId, result);
        var addedOption = Assert.Single(options);
        Assert.Equal(targetId, addedOption.Id);
        Assert.Equal("My Headset (Headphones) (Disconnected)", addedOption.DisplayName);
    }

    [Fact]
    public void ResolveAudioOption_WhenEndpointIsCompletelyMissing_UsesCachedNameAndTag()
    {
        // Arrange
        const string targetId = "{0.0.0.00000000}.{99999999-8888-7777-6666-555555555555}";
        var endpoints = new List<AudioEndpointInfo>();
        var options = new ObservableCollection<DeviceSelectionOption>();
        var customNames = new Dictionary<string, string>();
        var cachedNames = new Dictionary<string, string> { [targetId] = "Wireless Gaming DAC" };

        // Act
        var result = DeviceOptionResolver.ResolveAudioOption(targetId, endpoints, options, customNames, cachedNames);

        // Assert
        Assert.Equal(targetId, result);
        var addedOption = Assert.Single(options);
        Assert.Equal(targetId, addedOption.Id);
        Assert.Equal("Wireless Gaming DAC (Disconnected)", addedOption.DisplayName);
    }

    [Fact]
    public void EnsureDisplayOption_WhenMonitorMissing_UsesCachedNameAndTag()
    {
        // Arrange
        const string monitorId = "MSI4DD0";
        var options = new ObservableCollection<DeviceSelectionOption>();
        var customNames = new Dictionary<string, string>();
        var cachedNames = new Dictionary<string, string> { [monitorId] = "MSI MPG 341CQPX" };

        // Act
        DeviceOptionResolver.EnsureDisplayOption(monitorId, options, customNames, cachedNames);

        // Assert
        var addedOption = Assert.Single(options);
        Assert.Equal(monitorId, addedOption.Id);
        Assert.Equal("MSI MPG 341CQPX [MSI4DD0] (Disconnected)", addedOption.DisplayName);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test --filter "FullyQualifiedName~DeviceOptionResolverTests"`
Expected: FAIL due to missing overloads or mismatched label formatting.

- [ ] **Step 3: Implement updated DeviceOptionResolver**
Update `src/RigSwitch.App/ViewModels/DeviceOptionResolver.cs`:
- Add `FormatOfflineAudioLabel`:
  ```csharp
    public static string FormatOfflineAudioLabel(
        string id,
        string? detectedName,
        IDictionary<string, string>? customNames,
        IDictionary<string, string>? cachedNames)
    {
        string nick = string.Empty;
        if (customNames != null && customNames.TryGetValue(id, out var custom) && !string.IsNullOrWhiteSpace(custom))
        {
            nick = custom;
        }

        string baseName = detectedName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseName) && cachedNames != null && cachedNames.TryGetValue(id, out var cached))
        {
            baseName = cached;
        }

        if (!string.IsNullOrWhiteSpace(nick) && !string.IsNullOrWhiteSpace(baseName))
            return $"{nick} ({baseName}) (Disconnected)";
        if (!string.IsNullOrWhiteSpace(nick))
            return $"{nick} (Disconnected)";
        if (!string.IsNullOrWhiteSpace(baseName))
            return $"{baseName} (Disconnected)";

        return $"{id} (Disconnected)";
    }
  ```
- Add `FormatOfflineDisplayLabel`:
  ```csharp
    public static string FormatOfflineDisplayLabel(
        string monitorId,
        IDictionary<string, string>? customNames,
        IDictionary<string, string>? cachedNames)
    {
        string nick = string.Empty;
        if (customNames != null && customNames.TryGetValue(monitorId, out var custom) && !string.IsNullOrWhiteSpace(custom))
        {
            nick = custom;
        }

        string baseName = string.Empty;
        if (cachedNames != null && cachedNames.TryGetValue(monitorId, out var cached) && !string.IsNullOrWhiteSpace(cached))
        {
            baseName = cached;
        }

        if (!string.IsNullOrWhiteSpace(nick) && !string.IsNullOrWhiteSpace(baseName))
            return $"{nick} ({baseName}) [{monitorId}] (Disconnected)";
        if (!string.IsNullOrWhiteSpace(nick))
            return $"{nick} [{monitorId}] (Disconnected)";
        if (!string.IsNullOrWhiteSpace(baseName))
            return $"{baseName} [{monitorId}] (Disconnected)";

        return $"{monitorId} (Disconnected)";
    }
  ```
- Update `ResolveAudioOption`:
  ```csharp
    public static string ResolveAudioOption(
        string targetId,
        IEnumerable<AudioEndpointInfo> endpoints,
        ObservableCollection<DeviceSelectionOption> options,
        IDictionary<string, string>? customNames = null,
        IDictionary<string, string>? cachedNames = null)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return string.Empty;

        var match = endpoints.FirstOrDefault(a => MatchesEndpoint(a.Id, targetId));
        if (match != null)
        {
            if (!options.Any(opt => opt.Id == match.Id))
            {
                var label = FormatOfflineAudioLabel(match.Id, match.Name, customNames, cachedNames);
                options.Add(new DeviceSelectionOption(match.Id, label));
            }
            return match.Id;
        }

        if (!options.Any(opt => opt.Id == targetId))
        {
            var label = FormatOfflineAudioLabel(targetId, null, customNames, cachedNames);
            options.Add(new DeviceSelectionOption(targetId, label));
        }

        return targetId;
    }
  ```
- Update `EnsureDisplayOption`:
  ```csharp
    public static void EnsureDisplayOption(
        string monitorId,
        ObservableCollection<DeviceSelectionOption> options,
        IDictionary<string, string>? customNames = null,
        IDictionary<string, string>? cachedNames = null)
    {
        if (!string.IsNullOrWhiteSpace(monitorId) && !options.Any(opt => opt.Id == monitorId))
        {
            var label = FormatOfflineDisplayLabel(monitorId, customNames, cachedNames);
            options.Add(new DeviceSelectionOption(monitorId, label));
        }
    }
  ```

- [ ] **Step 4: Run test to verify it passes**
Run: `dotnet test --filter "FullyQualifiedName~DeviceOptionResolverTests"`
Expected: PASS

- [ ] **Step 5: Run lint and typecheck**
Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 6: Commit**
```bash
git add src/RigSwitch.App/ViewModels/DeviceOptionResolver.cs tests/RigSwitch.Tests.Unit/DeviceOptionResolverTests.cs
git commit -m "feat: enhance DeviceOptionResolver with friendly name caching and offline status"
```

---

### Task 3: Update MainSettingsViewModel and Monitor Tile Management

**Files:**
- Modify: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs`
- Modify: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.Monitors.cs`
- Create/Modify: `tests/RigSwitch.Tests.Unit/MainSettingsViewModelOfflineDeviceTests.cs`

**Interfaces:**
- Consumes: `UserSettings.CachedDeviceNames`, `DeviceOptionResolver`
- Produces:
  - Cache updates during `EnumerateDisplaysAsync` and `EnumerateAudioEndpointsAsync`
  - `PopulateMonitorTiles` creating offline tiles with `IsActive = false` for preset monitors not detected
  - `SyncMonitorTilesToActivePresets` preserving all tiles in Desk/SimRig piles

- [ ] **Step 1: Write the failing tests**
Create `tests/RigSwitch.Tests.Unit/MainSettingsViewModelOfflineDeviceTests.cs`:
```csharp
namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using Xunit;

public sealed class MainSettingsViewModelOfflineDeviceTests
{
    private readonly IProfileSwitchCoordinator _coordinator;
    private readonly ISettingsStorageService _settingsService;
    private readonly IDisplayConfigurationService _displayService;
    private readonly IAudioEndpointDirector _audioDirector;
    private readonly IGlobalHotkeyService _hotkeyService;

    public MainSettingsViewModelOfflineDeviceTests()
    {
        _coordinator = Substitute.For<IProfileSwitchCoordinator>();
        _settingsService = Substitute.For<ISettingsStorageService>();
        _displayService = Substitute.For<IDisplayConfigurationService>();
        _audioDirector = Substitute.For<IAudioEndpointDirector>();
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
    }

    [Fact]
    public void PopulateMonitorTiles_WhenPresetContainsOfflineMonitor_CreatesOfflineTileInCorrectPile()
    {
        var settings = new UserSettings();
        var deskPreset = settings.GetActivePreset(ProfileMode.Desk);
        deskPreset.TargetMonitorIds = ["ACTIVE_MON1", "OFFLINE_MON2"];
        settings.CachedDeviceNames["OFFLINE_MON2"] = "Sim Rig Secondary";

        var displays = new List<DisplayDeviceInfo>
        {
            new("ACTIVE_MON1", @"\\.\DISPLAY1", "Main Desk", "NVIDIA", true, true)
        };

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            new RigSwitch.App.Services.TrayIconService(_coordinator, _settingsService));

        vm.PopulateMonitorTiles(displays, settings);

        Assert.Equal(2, vm.MonitorTiles.Count);
        var activeTile = vm.MonitorTiles.First(t => t.MonitorId == "ACTIVE_MON1");
        Assert.True(activeTile.IsActive);
        Assert.Equal(MonitorPileAssignment.Desk, activeTile.Pile);

        var offlineTile = vm.MonitorTiles.First(t => t.MonitorId == "OFFLINE_MON2");
        Assert.False(offlineTile.IsActive);
        Assert.Equal("Sim Rig Secondary", offlineTile.FriendlyName);
        Assert.Equal(MonitorPileAssignment.Desk, offlineTile.Pile);
    }

    [Fact]
    public void SyncMonitorTilesToActivePresets_RetainsOfflineTilesInPreset()
    {
        var settings = new UserSettings();
        var deskPreset = settings.GetActivePreset(ProfileMode.Desk);
        deskPreset.TargetMonitorIds = ["MON_ONLINE", "MON_OFFLINE"];

        using var vm = new MainSettingsViewModel(
            _coordinator,
            _settingsService,
            _displayService,
            _audioDirector,
            _hotkeyService,
            new RigSwitch.App.Services.TrayIconService(_coordinator, _settingsService));

        vm.MonitorTiles.Add(new DisplayMonitorTileViewModel(1, "MON_ONLINE", "Online", MonitorPileAssignment.Desk, true, true));
        vm.MonitorTiles.Add(new DisplayMonitorTileViewModel(2, "MON_OFFLINE", "Offline", MonitorPileAssignment.Desk, false, false));

        vm.SyncMonitorTilesToActivePresets();

        Assert.Contains("MON_OFFLINE", deskPreset.TargetMonitorIds);
        Assert.Equal(["MON_ONLINE", "MON_OFFLINE"], deskPreset.TargetMonitorIds);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test --filter "FullyQualifiedName~MainSettingsViewModelOfflineDeviceTests"`
Expected: FAIL because `PopulateMonitorTiles` doesn't yet create offline tiles for preset monitors missing from `displays`.

- [ ] **Step 3: Implement ViewModel logic**
1. In `MainSettingsViewModel.cs`:
   - Pass `_settings.CustomDeviceNames` and `_settings.CachedDeviceNames` to `DeviceOptionResolver.ResolveAudioOption` and `EnsureDisplayOption`.
   - Update `_settings.CachedDeviceNames[d.MonitorId] = d.FriendlyName` when populating displays.
   - Update `_settings.CachedDeviceNames[a.Id] = a.Name` when populating audio endpoints.
2. In `MainSettingsViewModel.Monitors.cs`:
   - In `PopulateMonitorTiles`:
     ```csharp
     // After populating detected displays:
     var allPresetMonitorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
     foreach (var p in settings.DeskPresets)
     {
         foreach (var id in p.TargetMonitorIds) allPresetMonitorIds.Add(id);
     }
     foreach (var p in settings.RigPresets)
     {
         foreach (var id in p.TargetMonitorIds) allPresetMonitorIds.Add(id);
     }

     var existingTileIds = new HashSet<string>(MonitorTiles.Select(t => t.MonitorId), StringComparer.OrdinalIgnoreCase);
     foreach (var monitorId in allPresetMonitorIds)
     {
         if (existingTileIds.Contains(monitorId)) continue;

         var pile = MonitorPileAssignment.Unassigned;
         if (deskMonitorIds.Contains(monitorId, StringComparer.OrdinalIgnoreCase))
             pile = MonitorPileAssignment.Desk;
         else if (rigMonitorIds.Contains(monitorId, StringComparer.OrdinalIgnoreCase))
             pile = MonitorPileAssignment.SimRig;

         string friendlyName = monitorId;
         if (settings.CustomDeviceNames.TryGetValue(monitorId, out var nick) && !string.IsNullOrWhiteSpace(nick))
             friendlyName = nick;
         else if (settings.CachedDeviceNames.TryGetValue(monitorId, out var cached) && !string.IsNullOrWhiteSpace(cached))
             friendlyName = cached;

         var tile = new DisplayMonitorTileViewModel(
             displayNumber: number++,
             monitorId: monitorId,
             friendlyName: friendlyName,
             pile: pile,
             isPrimary: false,
             isActive: false,
             movePileAction: OnTilePileMoved);

         MonitorTiles.Add(tile);
         existingTileIds.Add(monitorId);
     }
     ```

- [ ] **Step 4: Run test to verify it passes**
Run: `dotnet test --filter "FullyQualifiedName~MainSettingsViewModelOfflineDeviceTests"`
Expected: PASS

- [ ] **Step 5: Run lint and typecheck**
Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 6: Commit**
```bash
git add src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs src/RigSwitch.App/ViewModels/MainSettingsViewModel.Monitors.cs tests/RigSwitch.Tests.Unit/MainSettingsViewModelOfflineDeviceTests.cs
git commit -m "feat: populate and preserve offline monitor tiles and cache device names"
```

---

### Task 4: UI Styling for Offline Monitor Tiles and Verification

**Files:**
- Modify: `src/RigSwitch.App/Views/MainSettingsWindow.xaml`
- Add UI verification test in `tests/RigSwitch.Tests.Unit/ReviewFixesTests.cs`

**Interfaces:**
- Consumes: `DisplayMonitorTileViewModel.IsActive`
- Produces: Visual offline indicator in `MonitorTileTemplate`

- [ ] **Step 1: Write test or check UI instantiability**
Verify `MainSettingsWindow_CanInstantiateAndShow` in `ReviewFixesTests.cs` covers the modified template.

- [ ] **Step 2: Update MonitorTileTemplate in MainSettingsWindow.xaml**
In `src/RigSwitch.App/Views/MainSettingsWindow.xaml`:
- In `MonitorTileTemplate`, add a DataTrigger on `IsActive == False`:
  - Dim border/background or tile opacity (e.g. `Opacity` to 0.75).
  - Add an offline status badge or text:
    `<TextBlock Text="(Offline)" FontSize="10" Foreground="{StaticResource TextMuted}" HorizontalAlignment="Center" Visibility="{Binding IsActive, Converter={StaticResource InverseBooleanToVisibilityConverter}}" />` or using Triggers.

- [ ] **Step 3: Run all unit tests**
Run: `dotnet test`
Expected: ALL PASS (268+ tests)

- [ ] **Step 4: Run lint and typecheck**
Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 5: Commit**
```bash
git add src/RigSwitch.App/Views/MainSettingsWindow.xaml tests/RigSwitch.Tests.Unit/ReviewFixesTests.cs
git commit -m "feat: add visual indicator for offline monitor tiles"
```
