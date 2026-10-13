# Design Specification: Bass Shaker Audio Exclusion, Primary/Secondary Audio Toggle, and CoreAudio Watchdog

**Date:** 2026-10-11  
**Status:** Approved  
**Target Product:** RigSwitch  

## 1. Overview & Problem Statement

Users running sim racing and flight simulation setups often have specialized audio hardware, including:
1. **Haptic Transducers / Bass Shakers (DACs/Amps):** Connected via dedicated USB DACs (e.g. Nobsound USB DAC, USB DAC to RCA into Fosi amp) managed by simulation telemetry software like SimHub (ShakeIt Bass Shakers).
   - **Problem:** These devices must remain enabled in Windows so SimHub can stream telemetry audio via WASAPI, but Windows or games occasionally assign them as the default audio device (after reboot, USB re-plug, or sleep). Routing system, game, or browser sounds to bass shakers causes harsh physical vibrations and risk of transducer damage.
2. **Dual-Tier Audio Routing (Speakers vs. SteelSeries Sonar / Headset):**
   - On the Desk setup, the user switches between desktop speakers (e.g., Creative Pebble V3 or MSI monitor speakers) and a headset.
   - On the Sim Rig, the user switches between rig speakers (e.g., VG monitor) and a headset.
   - **Problem:** SteelSeries Sonar requires setting its virtual endpoints (Gaming / Chat) as the Windows default audio device to perform app audio routing. However, when not using the headset, the user wants audio to default to speakers. Sonar's background service (SteelSeries GG) or Windows frequently hijacks default audio.

## 2. Goals & Success Criteria

- **Absolute Bass Shaker Protection:** Users can blacklist specific audio endpoints in RigSwitch. Blacklisted endpoints are filtered out from all preset selection dropdowns and are permanently barred from becoming the Windows default playback or communications device.
- **Primary / Secondary Audio per Preset:** Each workstation preset supports both a **Primary Audio (Speakers)** and a **Secondary Audio (Headset / Sonar)** endpoint.
- **Instant Audio Toggle Hotkey:** A global configurable hotkey (`Ctrl+Alt+A` by default) allows immediate toggling between Primary and Secondary audio on the active workstation profile.
- **Active CoreAudio Watchdog (`IMMNotificationClient`):** A zero-polling, native event-driven watchdog service monitors Windows audio default endpoint changes:
  - If Windows shifts default playback to a blacklisted shaker DAC, the watchdog instantly reverts it to the authorized preset device.
  - If `EnforcePrimarySpeakers` is enabled and the user is in Primary mode, unauthorized attempts by background apps (like Sonar) to steal default audio are intercepted and reverted.
  - Does not prevent deliberate human switching in the Windows volume picker between valid, non-blacklisted devices.
  - Re-entrancy guards prevent infinite loops during RigSwitch-initiated audio switches.

---

## 3. Architecture & Data Model

### 3.1 Data Model Extensions

#### `WorkstationPreset.cs`
```csharp
public sealed record WorkstationPreset
{
    // ... existing fields ...
    
    /// <summary>
    /// Gets or sets the secondary audio endpoint device identifier (e.g. Headset or SteelSeries Sonar).
    /// </summary>
    public string SecondaryAudioId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secondary microphone / capture endpoint device identifier.
    /// </summary>
    public string SecondaryMicrophoneId { get; set; } = string.Empty;
}
```

#### `UserSettings.cs`
```csharp
public enum AudioOutputMode
{
    Primary = 0,
    Secondary = 1
}

public sealed record UserSettings
{
    // ... existing fields ...

    /// <summary>
    /// Gets or sets the list of audio endpoint GUID identifiers strictly excluded from default routing.
    /// Used for bass shaker DACs and telemetry transducers.
    /// </summary>
    public List<string> BlacklistedAudioEndpointIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the global hotkey combination used to toggle between Primary and Secondary audio.
    /// </summary>
    public string AudioToggleHotkey { get; set; } = "Ctrl+Alt+A";

    /// <summary>
    /// Gets or sets whether RigSwitch actively guards against unauthorized background audio hijacking.
    /// </summary>
    public bool EnforcePrimaryAudio { get; set; } = true;

    /// <summary>
    /// Gets or sets the currently active audio output mode (Primary speakers vs Secondary headset).
    /// </summary>
    public AudioOutputMode ActiveAudioOutputMode { get; set; } = AudioOutputMode.Primary;
}
```

---

## 4. Component Details & Workflows

### 4.1 Audio Endpoint Blacklist Manager
- Located in `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs` and `DeviceOptionResolver.cs`.
- Endpoints in `UserSettings.BlacklistedAudioEndpointIds`:
  - Show a `[Blacklisted / Bass Shaker]` status badge in the Audio Settings view.
  - Are excluded from all dropdown pickers for `PrimaryAudioId`, `SecondaryAudioId`, and `FallbackAudioId`.
  - Can be toggled on/off directly from the Audio Devices list in the settings UI.

### 4.2 Audio Toggle & Output Coordination
- Managed in `src/RigSwitch.Core/Services/RigSwitchCoordinator.cs` (or `ProfileSwitchCoordinator.cs`).
- Method: `Task ToggleAudioOutputModeAsync(CancellationToken cancellationToken = default);`
  1. Inspects current `ActiveAudioOutputMode`.
  2. If currently `Primary`, switches to `Secondary` and applies `activePreset.SecondaryAudioId` (and `SecondaryMicrophoneId` if present).
  3. If currently `Secondary`, switches to `Primary` and applies `activePreset.PrimaryAudioId` (and `PrimaryMicrophoneId`).
  4. Signals watchdog of intentional user switch with an operational bypass token.
  5. Updates tray menu status and triggers Windows toast notification with the newly activated endpoint name.

### 4.3 Native CoreAudio Watchdog (`AudioEndpointWatchdogService`)
- Implemented in `src/RigSwitch.Infrastructure/Windows/CoreAudio/AudioEndpointWatchdogService.cs`.
- Interfaces with `IMMDeviceEnumerator.RegisterEndpointNotificationCallback`:
  - Subscribes to `IMMNotificationClient.OnDefaultDeviceChanged(EDataFlow flow, ERole role, string defaultDeviceId)`.
- Event Processing Rules:
  1. Filter for `flow == eRender` (playback) and `role == eConsole` or `role == eMultimedia`.
  2. **Re-entrancy Guard:** If `_isApplyingInternalSwitch` is true, ignore event.
  3. **Blacklist Check:** If `defaultDeviceId` matches any ID in `BlacklistedAudioEndpointIds`:
     - Log warning and immediately invoke `IAudioEndpointDirector.SetDefaultPlaybackEndpointAsync` for the authorized target endpoint.
  4. **Speaker Enforcement Check:** If `EnforcePrimaryAudio` is enabled and `ActiveAudioOutputMode == AudioOutputMode.Primary`:
     - If `defaultDeviceId` matches known Sonar virtual devices or unwanted auto-switches, snap back to `activePreset.PrimaryAudioId`.
  5. **Safe Human Switching:** If the change was to another valid, non-blacklisted speaker/endpoint via Windows flyout, the watchdog respects human intent and does not fight the user.

---

## 5. UI and UX Design

1. **Audio Settings Tab:**
   - Add a "Bass Shaker / Excluded DACs" filter chip or toggle in the Audio Endpoints data grid.
   - Dedicated toggle switch: `Exclude from System Audio` on each endpoint row.
   - Checkbox: `Actively enforce primary speakers against background hijacks (e.g. SteelSeries Sonar)`.
2. **Preset Configuration Cards:**
   - Primary Audio (Speakers) dropdown.
   - Secondary Audio (Headset / Sonar) dropdown.
   - Secondary Microphone (optional) dropdown.
3. **Tray Menu & Notifications:**
   - Context menu item: `Switch to Headset (SteelSeries Sonar)` / `Switch to Speakers`.
   - Toast notification on toggle: `"Audio Output Switched: Creative Pebble V3 (Speakers)"`.
4. **Hotkey Settings:**
   - Keybind input field for `AudioToggleHotkey` (defaults to `Ctrl+Alt+A`).

---

## 6. Testing Strategy

1. **Unit Tests:**
   - `AudioEndpointBlacklistTests`: verifies blacklisted IDs are filtered from preset dropdown collections and resolution helpers.
   - `AudioOutputModeToggleTests`: verifies toggling flips between primary and secondary endpoints on active presets.
   - `AudioEndpointWatchdogTests`: mocks `INativeAudioProvider` and simulates `OnDefaultDeviceChanged`:
     - Blacklisted shaker ID triggers immediate revert.
     - Secondary Sonar ID allowed when in `AudioOutputMode.Secondary`.
     - Internal switch flag avoids recursive loops.
2. **Integration / Manual Verification:**
   - Validate that SimHub continues to stream telemetry to shaker DACs without interference.
   - Validate that pressing `Ctrl+Alt+A` cleanly toggles between Pebble/MSI speakers and SteelSeries Sonar.
   - Test deliberate human switching via Windows taskbar sound flyout.
