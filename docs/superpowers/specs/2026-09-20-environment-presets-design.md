# Environment Presets Engine & Display Settings Specification

> **Target Standard**: Steven T. Pelech's `AgenticEngineeringToolbelt`  
> **Solution**: `RigSwitch.slnx` (`net10.0-windows`, C# 13, WPF)  
> **Milestone**: Feature 1 of Multi-Feature Roadmap  
> **Branch**: `feat/environment-presets`

---

## 1. Executive Summary & Purpose

RigSwitch currently switches between two flat hardware configurations: **Desk** and **Sim Rig**. While the physical separation is binary (Desk setup vs. Sim Rig setup), users need multiple specialized configurations (presets) within each setup.

For example, a user on the **Sim Rig** may need:
- *Preset 1 (GT3 / Circuit)*: Ultrawide display, surround speakers, primary wheel setup.
- *Preset 2 (Rally / Drift)*: Ultrawide display, headset audio.
- *Preset 3 (Flight / Space)*: Secondary display/VR audio routing.

This specification introduces the **Environment Presets Engine**, allowing up to **3 customizable presets** per workstation environment (`Desk` and `SimRig`), along with quick-access display properties and tray submenu integration.

---

## 2. Domain & Data Models (`RigSwitch.Core`)

### 2.1 `WorkstationPreset` Model
A new domain record representing an individual configuration preset:

```csharp
namespace RigSwitch.Core.Models;

public sealed record WorkstationPreset
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default Preset";
    public string TargetMonitorId { get; set; } = string.Empty;
    public string PrimaryAudioId { get; set; } = string.Empty;
    public string FallbackAudioId { get; set; } = string.Empty;
    public string DirectHotkey { get; set; } = string.Empty;
    public List<string> LaunchApplicationPaths { get; set; } = [];
}
```

### 2.2 `UserSettings` Evolution & Backward Compatibility
Update `UserSettings` to house presets and active preset indices:

```csharp
namespace RigSwitch.Core.Models;

using RigSwitch.Core.Enums;

public sealed record UserSettings
{
    public ProfileMode LastActiveProfile { get; set; } = ProfileMode.Desk;

    public int ActiveDeskPresetIndex { get; set; } = 0;
    public int ActiveRigPresetIndex { get; set; } = 0;

    public List<WorkstationPreset> DeskPresets { get; set; } =
    [
        new() { Name = "Work / Primary" },
        new() { Name = "Media / Casual" },
        new() { Name = "Clean Desk" }
    ];

    public List<WorkstationPreset> RigPresets { get; set; } =
    [
        new() { Name = "GT3 / Circuit" },
        new() { Name = "Rally / Drift" },
        new() { Name = "Flight / Space" }
    ];

    public Dictionary<string, string> CustomDeviceNames { get; set; } = new();
    public List<string> HiddenAudioEndpointIds { get; set; } = [];

    public string ToggleHotkey { get; set; } = "Ctrl+Alt+S";
    public string DeskHotkey { get; set; } = "Ctrl+Alt+D";
    public string RigHotkey { get; set; } = "Ctrl+Alt+R";

    public bool StartMinimizedToTray { get; set; } = true;
    public bool ShowToastNotifications { get; set; } = true;

    // Helper methods to get active preset safely
    public WorkstationPreset GetActivePreset(ProfileMode mode)
    {
        var list = mode == ProfileMode.Desk ? DeskPresets : RigPresets;
        var idx = mode == ProfileMode.Desk ? ActiveDeskPresetIndex : ActiveRigPresetIndex;
        if (list.Count == 0) list.Add(new WorkstationPreset());
        return (idx >= 0 && idx < list.Count) ? list[idx] : list[0];
    }
}
```

### 2.3 Legacy Auto-Migration
In `JsonSettingsStorageService.LoadSettingsAsync()`, if loading a settings file that has no `DeskPresets` or `RigPresets`, it initializes the 3 default slots and copies any existing legacy values into Preset 0, preserving existing user configurations seamlessly.

---

## 3. Coordinator & Service Contracts (`IProfileSwitchCoordinator`)

Extend `IProfileSwitchCoordinator` to support preset-level switching:

```csharp
namespace RigSwitch.Core.Interfaces;

public interface IProfileSwitchCoordinator : IDisposable
{
    ProfileMode CurrentProfile { get; }
    int CurrentPresetIndex { get; }
    WorkstationPreset CurrentPreset { get; }

    event EventHandler<ProfileChangedEventArgs>? ProfileChanged;

    void SetCurrentProfile(ProfileMode profile);
    void SetCurrentPreset(ProfileMode profile, int presetIndex);

    Task<bool> SwitchProfileAsync(ProfileMode targetProfile, CancellationToken cancellationToken = default);
    Task<bool> SwitchToPresetAsync(ProfileMode targetProfile, int presetIndex, CancellationToken cancellationToken = default);
}
```

### 3.1 Switching Execution Logic
1. `SwitchProfileAsync(ProfileMode targetProfile)`:
   - Retrieves active preset for `targetProfile` (`GetActivePreset(targetProfile)`).
   - Validates display reachability for `preset.TargetMonitorId`.
   - Disables inactive monitor (from the other profile's active preset) and activates `preset.TargetMonitorId`.
   - Resolves audio routing against `preset.PrimaryAudioId` and `preset.FallbackAudioId`.
   - Updates `CurrentProfile` and triggers `ProfileChanged`.
2. `SwitchToPresetAsync(ProfileMode targetProfile, int presetIndex)`:
   - Updates `ActiveDeskPresetIndex` or `ActiveRigPresetIndex`.
   - Performs display and audio switch to the selected preset's configuration.
   - Saves settings and notifies tray/UI.

---

## 4. UI & System Tray Integration (`RigSwitch.App`)

### 4.1 Tray Icon Context Menu
- Left-click or `ToggleHotkey` toggles between the active Desk preset and the active Rig preset.
- Right-click menu organizes environment options into submenus:
  - 🖥️ **Desk Setup** ▸
    - ✓ *Preset 1 Name* (Active)
    - *Preset 2 Name*
    - *Preset 3 Name*
  - 🏎️ **Sim Rig Setup** ▸
    - ✓ *Preset 1 Name* (Active)
    - *Preset 2 Name*
    - *Preset 3 Name*
  - ──────────
  - ⚙️ Settings...
  - ❌ Exit RigSwitch
- Tooltip displays active setup and preset: `"RigSwitch - Sim Rig [GT3 / Circuit]"`.

### 4.2 Settings Window (Profiles Tab)
- Presets are organized into two sections: **🖥️ Desk Setup Presets** and **🏎️ Sim Rig Setup Presets**.
- Each section displays 3 preset cards with:
  - Radio button to designate the "Active / Default" preset.
  - Name editable textbox.
  - Target Display ComboBox.
  - Primary Audio ComboBox.
  - Fallback Audio ComboBox.
  - Direct Hotkey field (e.g. `Ctrl+Alt+1`).
  - "Launch Display Settings" button: launches `ms-settings:display` via Win32 shell execute.

---

## 5. Verification & Testing Standards

- **Unit Tests (`RigSwitch.Tests.Unit`)**:
  - `WorkstationPresetTests`: serialization, default preset generation.
  - `ProfileSwitchCoordinatorPresetTests`: switching between presets in same environment, switching across environments with different presets, safety gate handling per preset.
  - `JsonSettingsStorageServiceMigrationTests`: auto-migration from flat legacy settings to preset lists.
- **Simulation Harness (`RigSwitch.Tests.Harness`)**:
  - Multi-preset rapid switching disturbance test under simulated hardware disconnections.
- **Anti-Bloat & Line Limits**:
  - All files strictly `< 500` lines.
  - Zero `*Manager`, `*Helper`, or `*Util` types.
  - 100% build pass with zero warnings (`TreatWarningsAsErrors=true`).
