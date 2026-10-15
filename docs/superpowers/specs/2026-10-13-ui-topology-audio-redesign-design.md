# RigSwitch Audio Stutter Fix & UI / Topology / Audio Priority Redesign Specification

**Date:** 2026-10-13  
**Branch:** `fix/audio-stutter-watchdog`

---

## 1. Overview & Goals

This specification covers the root-cause fix for background CoreAudio stuttering as well as a cohesive UI and configuration redesign for RigSwitch:

1. **Eliminate Background CoreAudio Stuttering:** Prevent `AudioEndpointWatchdogService` from fighting Windows CoreAudio (`Audiosrv`) when a configured primary audio device is offline (e.g., USB-powered speakers in AUX/`NotPresent` mode), fix `IMMNotificationClient` COM apartment marshaling, and lazy-initialize `MainSettingsWindow` when started minimized to the system tray.
2. **Spacious Default Window & Visible Version Badge:** Increase the default `MainSettingsWindow` dimensions from `960×700` to `1240×840` (`MinWidth="1040"`, `MinHeight="680"`) so controls are never scrunched on startup, and display the application version (`vX.Y.Z`) in the header bar and sidebar status footer.
3. **Direct Sidebar Sub-Page Routing per Preset:** Replace the long stacked `Presets` page and redundant `Displays & Topology` / `Hardware Telemetry Summary` tab with direct sidebar navigation routes for each Desk preset (`Desk 1..3`) and Sim Rig preset (`Sim Rig 1..3`).
4. **Per-Preset 2D Monitor Topology Canvas + Slim "Off in Preset" Tray:** Give each preset its own interactive 2D spatial monitor stage where placing a monitor on the stage enables it in that preset, dragging positions it in 2D space (with edge snapping), clicking `★` sets Primary `(0,0)`, clicking the HDR pill cycles HDR state, and a **"📸 Capture Windows Layout"** button snapshots exact `(X, Y)` coordinates from Windows CCD. Disabled monitors sit in a compact pill tray below the stage.
5. **Ranked Audio & Microphone Priority Lists:** Replace separate `Primary` / `Secondary` / `Fallback` dropdowns with an ordered priority list per preset. RigSwitch and the CoreAudio Watchdog automatically route to the highest-ranked `Active` (non-blacklisted) endpoint, and the `Toggle Audio` hotkey (`Ctrl+Alt+A`) cycles through the currently `Active` endpoints in the preset's ranked list.
6. **Smart "Relevant Devices" Audio Filter:** Filter out unconnected Windows `MMDevice` registry ghosts (`NotPresent`) by default unless a device is referenced in a Preset, Blacklist, or Nickname, while preserving the two per-row Fluent toggle switches (`Win Menu Visibility` and `Exclude / Bass Shaker Lockout`) and offering filter tabs for `Relevant Devices`, `Active Only`, `Disabled / Unplugged`, and `All Registry Ghosts`.
7. **Streamlined `+ Add Nickname` Workflow:** Show only devices that currently have a custom nickname configured (with inline editing and `✕` removal), paired with an **"+ Add New Device Nickname"** bar whose dropdown only lists relevant devices that do not yet have a nickname.

---

## 2. Architecture & Data Model Changes

### 2.1 Backward-Compatible `WorkstationPreset` Models ([`WorkstationPreset.cs`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.Core/Models/WorkstationPreset.cs))

To support per-preset 2D spatial monitor layouts and ranked audio/microphone priority lists without breaking existing `settings.json` files:

- **`MonitorPlacement` Model (`RigSwitch.Core.Models.MonitorPlacement`):**
  ```csharp
  public sealed class MonitorPlacement
  {
      public string MonitorId { get; set; } = string.Empty;
      public int PositionX { get; set; }
      public int PositionY { get; set; }
      public int Width { get; set; } = 1920;
      public int Height { get; set; } = 1080;
      public bool IsPrimary { get; set; }
      public PresetHdrMode HdrMode { get; set; } = PresetHdrMode.Retain;
  }
  ```
- **`WorkstationPreset` Additions:**
  - `List<MonitorPlacement> MonitorPlacements { get; set; } = [];`
    - Synchronized with `TargetMonitorIds` (where the primary monitor is first in `TargetMonitorIds` for backward compatibility).
  - `List<string> AudioPriorityIds { get; set; } = [];`
  - `List<string> MicrophonePriorityIds { get; set; } = [];`
  - **Automatic Migration Helper (`GetEffectiveAudioPriorityList()` / `GetEffectiveMicrophonePriorityList()`):**
    - If `AudioPriorityIds` is empty but legacy `PrimaryAudioId`, `SecondaryAudioId`, or `FallbackAudioId` are populated, automatically seeds the ordered list `[PrimaryAudioId, FallbackAudioId, SecondaryAudioId]` (deduplicated, non-empty) and keeps `PrimaryAudioId` / `FallbackAudioId` / `SecondaryAudioId` synchronized on save.

### 2.2 CCD 2D Spatial Positioning ([`WindowsDisplayConfigurationService.cs`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.Infrastructure/Windows/Ccd/WindowsDisplayConfigurationService.cs))

- **Reading Live Coordinates & Resolution:**
  - Extend [`DisplayDeviceInfo`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.Core/Models/DisplayDeviceInfo.cs) with optional `PositionX`, `PositionY`, `Width`, and `Height` (defaulting to `0, 0, 1920, 1080`) populated from `mode.modeInfo.sourceMode.position` and `sourceMode.width` / `sourceMode.height` during `EnumerateDisplaysAsync`.
- **Applying 2D Coordinates:**
  - Add an overload or optional `IReadOnlyList<MonitorPlacement>? placements = null` parameter on `ApplyDisplayTopologyAsync`.
  - When explicit `MonitorPlacement` coordinates are provided for a preset, set the primary monitor at `(0, 0)` (shifting relative coordinates if needed so primary is `(0, 0)`) and apply each enabled monitor's `(PositionX, PositionY)` to `clonedModes[modeIdx].modeInfo.sourceMode.position`. When no explicit placements exist, retain the linear left-to-right arrangement.

### 2.3 Ranked Audio Routing, Cycling Toggle & Watchdog Enforcement

- **Profile Activation ([`ProfileSwitchCoordinator.SwitchCoreAsync`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs)):**
  - Evaluates the preset's `GetEffectiveAudioPriorityList()` in order (`#1`, `#2`, `#3`, ...) and selects the first endpoint that is `DevicePresenceState.Active` and not in `BlacklistedAudioEndpointIds`.
  - Invokes `_onIntentionalAudioSwitch?.Invoke(resolvedAudioId)` **before** calling `_audioDirector.SetDefaultPlaybackEndpointAsync(resolvedAudioId)`.
- **Cycling Audio Toggle ([`ProfileSwitchCoordinator.ToggleAudioOutputModeAsync`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs)):**
  - Finds all endpoints in the active preset's `GetEffectiveAudioPriorityList()` that are currently `DevicePresenceState.Active` and not blacklisted.
  - Advances from the current default playback endpoint to the next active endpoint in that ranked list (wrapping around to index `0`), notifies the watchdog via `_onIntentionalAudioSwitch`, and routes default playback (and corresponding ranked microphone if configured) to that endpoint.
- **Watchdog Enforcement ([`AudioEndpointWatchdogService.cs`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.Infrastructure/Windows/CoreAudio/AudioEndpointWatchdogService.cs)):**
  - When `EnforcePrimaryAudio` is enabled, resolves the highest-ranked `DevicePresenceState.Active` non-blacklisted endpoint from the active preset's `GetEffectiveAudioPriorityList()`.
  - Never attempts to force a `NotPresent` or `Unplugged` endpoint (such as USB speakers in AUX mode), and recognizes intentional switches from `SwitchProfileAsync` and `ToggleAudioOutputModeAsync`.

---

## 3. UI & Navigation Specification ([`MainSettingsWindow.xaml`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.App/Views/MainSettingsWindow.xaml))

### 3.1 Window Dimensions, Header & Sidebar Navigation
- **Window Dimensions:** `Width="1240"`, `Height="840"`, `MinWidth="1040"`, `MinHeight="680"`.
- **Version Display:** Expose `AppVersionText` (e.g., `v1.4.0`) on [`MainSettingsViewModel`](file:///C:/Users/Alias/repos/RigSwitch/src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs) and render it as a subtle pill badge next to `⚡ RigSwitch` in the top header and in the bottom watchdog card of the left navigation sidebar.
- **Left Sidebar Navigation Rail:**
  - **🖥️ Desk Presets** section header + 3 preset route items bound to `DeskPresets[0..2]` (showing preset name and a dot badge on the default desk preset).
  - **🏎️ Sim Rig Presets** section header + 3 preset route items bound to `RigPresets[0..2]` (showing preset name and a dot badge on the default rig preset).
  - **System & Hardware** section header:
    - `🔊 Audio & DAC Lockout`
    - `🏷️ Device Nicknames`
    - `⌨️ Hotkeys & System`
  - *(The redundant `Displays & Topology` tab and `Hardware Telemetry Summary` card are removed, as monitor topology and live audio status badges now live directly inside each preset view.)*

### 3.2 Selected Preset Page Layout
When any Desk or Sim Rig preset is selected in the left sidebar, the main content pane displays that single preset's configuration without requiring nested tab bars:
1. **Top Identity & Shortcut Bar:**
   - Preset Name `TextBox`, **Default Preset for Profile** toggle/radio, **Direct Hotkey** recorder (`HotkeyRecorderControl`), and a **"⚡ Activate Preset Now"** button.
2. **Middle 2-Column Hardware Grid:**
   - **Left Card — 2D Monitor Topology Stage & Slim "Off in Preset" Tray:**
     - Header actions: `🔍 Identify Monitors` and `📸 Capture Windows Layout`.
     - **2D Stage Canvas:** Displays rectangles for each monitor enabled in this preset, positioned proportionally by `(PositionX, PositionY)` and `(Width, Height)`. Users can drag monitor rectangles to reposition (with edge snapping), click `★` to make a monitor Primary `(0,0)`, click the `HDR` badge on a monitor to cycle `Retain` → `Enable HDR` → `Disable HDR`, or click `✕` to disable the monitor for this preset.
     - **Slim "Off in Preset" Dock:** Compact horizontal wrap strip underneath the canvas showing monitors not enabled in this preset. Clicking `＋` (or dragging onto the stage) enables the monitor on the canvas.
   - **Right Card — Playback Audio Priority Order (Ranked List):**
     - Header action: Dropdown + `＋ Add Device` button (listing relevant un-added playback devices).
     - Ordered list of ranked playback endpoints (`#1`, `#2`, `#3`, ...) with live status pills (`● Active Now`, `● Ready`, `● Offline`), move `↑` / `↓` buttons, and `✕` remove button.
     - Compact footer row inside the card for **Playback Volume Behavior** (`Retain System Volume` vs `Custom Volume % + Mute`).
3. **Bottom 2-Column Auxiliary Grid:**
   - **Left Card — Microphone Priority Order & Input Level:**
     - Ranked microphone priority list (`#1`, `#2`, ...) with `＋ Add Mic`, `↑` / `↓` reorder, `✕` remove, and custom mic volume slider.
   - **Right Card — Lifecycle Application Hooks:**
     - `+ Add Application (.exe)` button and list of launch/terminate hooks for the preset.

### 3.3 Audio & DAC Lockout Page
- **Filter Segmented Control:**
  - `Relevant Devices` *(Default)*: Shows any endpoint where `State == Active`, `State == Disabled`, `State == Unplugged`, OR the endpoint ID is referenced in any Preset, Blacklist, or Nickname. Hides unconnected `NotPresent` Windows registry ghosts.
  - `Active Only`: Shows only `DevicePresenceState.Active` endpoints.
  - `Disabled / Unplugged`: Shows `Disabled` and `Unplugged` endpoints.
  - `All Registry Ghosts`: Shows all enumerated endpoints including unconnected `NotPresent` registry entries.
- **Per-Row Controls:**
  - Left Fluent toggle switch (`IsVisible`): toggles visibility in the Windows Sound menu.
  - Device Friendly Name, Adapter Name, and Endpoint ID.
  - Right Fluent toggle switch (`IsBlacklisted`, Amber style): `Exclude / Bass Shaker DAC` lockout toggle.
  - Presence state badge (`Active`, `Disabled`, `Unplugged`, `Not Present`).

### 3.4 Device Nicknames Page (`+ Add Nickname` Workflow)
- **Top `+ Add New Device Nickname` Card:**
  - `ComboBox` bound to `UnnicknamedDeviceOptions` (containing only **relevant** displays, playback devices, and microphones that do **not** currently have a nickname in `CustomDeviceNicknames`).
  - `TextBox` for entering the new nickname + **`＋ Add Nickname`** button.
- **Configured Nicknames List:**
  - Displays only devices that currently have a non-empty nickname.
  - Each row shows the device type badge (`Display`, `Audio`, `Mic`), original hardware name/ID, an editable `TextBox` for updating the nickname inline, and a **`✕`** button to remove the nickname (which returns the device to `UnnicknamedDeviceOptions`).

---

## 4. Verification & Testing Plan

1. **Unit Tests (`RigSwitch.Tests.Unit`):**
   - `AudioEndpointWatchdogServiceTests`: Verify offline primary (`NotPresent`) falls back to active ranked device without stutter loops.
   - `ProfileSwitchCoordinatorAudioToggleTests`: Verify ranked audio priority resolution on profile switch and cycling across active ranked devices on `ToggleAudioOutputModeAsync`.
   - `MainSettingsViewModel` tests: Verify `Relevant` audio filter hides unreferenced `NotPresent` ghosts while keeping preset-referenced offline devices, verify `+ Add Nickname` only offers un-nicknamed devices and removes/edits existing nicknames, and verify per-preset selection and monitor canvas inclusion/exclusion.
2. **Visual Verification (`UiScreenshotCaptureTests` & Live Screenshot Inspection):**
   - Render `MainSettingsWindow` at `1240×840` across `presets`, `audio`, `nicknames`, and `hotkeys` routes, capture PNGs via `CaptureScreenshotAsync`, and inspect them with `view_file` to confirm spacing, typography, version badges, 2D monitor canvas, and toggle switches.
3. **Build, Lint & Typecheck:**
   - Run `dotnet test`, `dotnet build --warnaserror`, `npm run lint`, and `npx tsc --noEmit`.
