# Preserve Offline and Disconnected Devices Design Spec

## Overview
When a monitor or audio endpoint is powered off, placed in standby, or unplugged (e.g. secondary Sim Rig displays, VR headsets, external DACs):
1. Dropdowns previously rendered them as cryptic raw GUIDs/EDIDs (e.g. `{0.0.0.0...} (Saved / Disconnected)`) or completely blank if the endpoint was enumerated by Windows CoreAudio in an inactive state (`Unplugged` / `Disabled`).
2. The multi-monitor pile manager (`MonitorTiles`) omitted powered-off monitors completely from the UI. Whenever `SyncMonitorTilesToActivePresets` ran (such as when dragging or moving any tile), any offline monitor was permanently deleted from the active preset's `TargetMonitorIds`.

This design preserves all configured devices across application restarts and device power cycles, retains cached friendly names, and presents offline devices clearly in both selection dropdowns and the monitor pile manager.

---

## 1. Data Model & Persistence

### 1.1 `UserSettings`
Add a persistent cache for device friendly names to [`UserSettings`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.Core/Models/UserSettings.cs):
```csharp
/// <summary>
/// Gets or sets cached friendly display names for known devices, keyed by hardware or endpoint ID.
/// </summary>
public Dictionary<string, string> CachedDeviceNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
```
- Keys are device IDs (EDID strings for monitors, endpoint ID/GUIDs for audio devices).
- Case-insensitive comparison ensures reliable matching across Windows GUID variations.

### 1.2 Automatic Cache Population
In [`MainSettingsViewModel.cs`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs):
- When `_displayService.EnumerateDisplaysAsync` executes, for every detected display with a valid friendly name, record `_settings.CachedDeviceNames[display.MonitorId] = display.FriendlyName`.
- When `_audioDirector.EnumerateAudioEndpointsAsync` executes, for every detected endpoint with a valid name, record `_settings.CachedDeviceNames[endpoint.Id] = endpoint.Name`.
- When settings are saved to disk, `CachedDeviceNames` is saved automatically.

---

## 2. Dropdown Resolution & Formatting

### 2.1 Label Formatting Helper
Define a unified label resolver in [`DeviceOptionResolver`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.App/ViewModels/DeviceOptionResolver.cs):
- For an audio or display ID:
  1. Check `customDeviceNames` (user-configured nickname).
  2. Check `cachedDeviceNames` (last known system friendly name).
  3. Check live detected device names.
  4. If offline/disconnected, format label as:
     - With nickname and friendly name: `"{nickname} ({friendlyName}) (Disconnected)"`
     - With nickname only: `"{nickname} (Disconnected)"`
     - With friendly name only: `"{friendlyName} [{id}] (Disconnected)"` (or `"{friendlyName} (Disconnected)"` for audio)
     - Fallback: `"{id} (Disconnected)"`

### 2.2 `DeviceOptionResolver.ResolveAudioOption`
Update to handle all cases:
```csharp
public static string ResolveAudioOption(
    string targetId,
    IEnumerable<AudioEndpointInfo> endpoints,
    ObservableCollection<DeviceSelectionOption> options,
    IDictionary<string, string>? customNames = null,
    IDictionary<string, string>? cachedNames = null)
```
- First, look for a matching endpoint in `endpoints` using `MatchesEndpoint`.
- If matched:
  - Check if `options` already contains an item with `opt.Id == match.Id`.
  - If not in `options` (because it is inactive/unplugged/disabled):
    - Format its label using nickname, endpoint name, and `(Disconnected)`.
    - Add to `options`.
  - Return `match.Id`.
- If not matched in `endpoints` (completely missing/offline):
  - Check if `options` already contains an item for `targetId`.
  - If not in `options`:
    - Resolve friendly name from `cachedNames` or `customNames`.
    - Format label with `(Disconnected)`.
    - Add to `options`.
  - Return `targetId`.

### 2.3 `DeviceOptionResolver.EnsureDisplayOption`
Update to:
```csharp
public static void EnsureDisplayOption(
    string monitorId,
    ObservableCollection<DeviceSelectionOption> options,
    IDictionary<string, string>? customNames = null,
    IDictionary<string, string>? cachedNames = null)
```
- If `monitorId` is not empty and not in `options`, resolve its display label via `customNames` or `cachedNames`, tag it with `(Disconnected)`, and add to `options`.

---

## 3. Multi-Monitor Pile Management (`MonitorTiles`)

### 3.1 Preserving Offline Displays in `PopulateMonitorTiles`
In [`MainSettingsViewModel.Monitors.cs`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.App/ViewModels/MainSettingsViewModel.Monitors.cs):
1. Populate detected displays from `displays` as active tiles (`IsActive = true`).
2. Collect all `TargetMonitorIds` from presets across Desk and Sim Rig modes.
3. For any monitor ID present in presets but not in `displays`:
   - Determine its pile assignment (`Desk` or `SimRig`).
   - Lookup friendly name from `settings.CustomDeviceNames` or `settings.CachedDeviceNames`.
   - Create a `DisplayMonitorTileViewModel`:
     - `DisplayNumber = number++`
     - `MonitorId = monitorId`
     - `FriendlyName = resolvedName`
     - `Pile = pile`
     - `IsPrimary = false`
     - `IsActive = false`
   - Add to `MonitorTiles`.

### 3.2 Syncing Monitor Tiles
In `SyncMonitorTilesToActivePresets()`:
- `deskMonitorIds` and `rigMonitorIds` are gathered from `MonitorTiles` regardless of `IsActive`.
- Offline tiles remain in their designated piles, so moving other tiles or saving presets will never strip offline monitors from `TargetMonitorIds`.

### 3.3 UI Representation in `MainSettingsWindow.xaml`
In `MonitorTileTemplate`:
- Add a visual status indicator on the tile:
  - If `IsActive == false`, display an `(Offline)` badge or muted badge color and subtle opacity reduction so users clearly understand the monitor is configured for this profile but currently powered off.

---

## 4. Testing Strategy

1. **Unit Tests in `RigSwitch.Tests.Unit`:**
   - **`DeviceOptionResolverTests`:**
     - Test resolving an inactive audio endpoint (e.g. `Unplugged`) adds the item to `options` with `(Disconnected)` and friendly name.
     - Test resolving an unknown/missing endpoint ID uses cached name and `(Disconnected)`.
     - Test `EnsureDisplayOption` uses cached friendly name and `(Disconnected)` instead of bare EDID.
   - **`MainSettingsViewModelTests` / `MonitorTileTests`:**
     - Test `PopulateMonitorTiles` creates offline tiles (`IsActive == false`) for preset monitors missing from detected displays.
     - Test `SyncMonitorTilesToActivePresets` preserves offline monitor IDs in `TargetMonitorIds`.
     - Test `CachedDeviceNames` is updated when displays and audio endpoints are loaded.
2. **Quality Gates:**
   - `dotnet test`
   - `npm run lint`
   - `npx tsc --noEmit`
