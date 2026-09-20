# Application Lifecycle Hooks Specification

> **Target Standard**: Steven T. Pelech's `AgenticEngineeringToolbelt`  
> **Solution**: `RigSwitch.slnx` (`net10.0-windows`, C# 13, WPF)  
> **Milestone**: Feature 3 of Multi-Feature Roadmap  
> **Branch**: `feat/app-lifecycle-hooks`

---

## 1. Overview & Objectives

Enable RigSwitch presets to automatically launch specialized software when entering a preset, and gracefully terminate that software when switching away:
1. **Automated Launch**: When entering a preset (e.g., Sim Rig preset), automatically launch configured executables (e.g. Moza Pit House, SimHub, CrewChief, Fanatec Control Panel).
2. **Graceful Termination on Switch-Away**: When switching away from that preset, gracefully request applications to close (`CloseMainWindow()`) with a timed fallback to termination (`Kill()`), freeing RAM and system resources on the main desktop.
3. **Resilience & Safety**:
   - Process tracking by PID with process-name fallback.
   - Non-blocking asynchronous execution so slow app launches do not stall display or audio switching.
   - Failure of an application to launch does not fail or abort the display/audio hardware transition.

---

## 2. Domain Models & Service Contracts

### 2.1 `PresetApplicationHook` Model (`RigSwitch.Core.Models`)
```csharp
namespace RigSwitch.Core.Models;

public sealed record PresetApplicationHook
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public bool CloseOnSwitchAway { get; set; } = true;
}
```

### 2.2 `IApplicationLifecycleHookService` (`RigSwitch.Core.Interfaces`)
```csharp
namespace RigSwitch.Core.Interfaces;

public interface IApplicationLifecycleHookService : IDisposable
{
    Task LaunchHooksForPresetAsync(WorkstationPreset preset, CancellationToken cancellationToken = default);
    Task CloseHooksForPresetAsync(WorkstationPreset preset, CancellationToken cancellationToken = default);
}
```

### 2.3 `WindowsApplicationLifecycleHookService` (`RigSwitch.Infrastructure.Windows.Processes`)
- Implements `IApplicationLifecycleHookService`.
- Maintains active launched process dictionary keyed by `HookId`.
- On launch:
  - Validates `File.Exists(hook.ExecutablePath)`.
  - Spawns `Process` with `UseShellExecute = true` (or `false` if arguments provided).
  - Tracks running process reference.
- On close:
  - For hooks with `CloseOnSwitchAway = true`:
    - Sends graceful `CloseMainWindow()` and waits up to 2.5 seconds.
    - If process does not exit, invokes `Kill(entireProcessTree: true)`.
    - If process PID is no longer valid, looks up running processes by filename to ensure no orphaned background processes remain.

---

## 3. Coordinator Integration (`ProfileSwitchCoordinator`)

In `ProfileSwitchCoordinator`:
- Injects `IApplicationLifecycleHookService`.
- On profile or preset switch:
  1. Identifies the previous active preset (`previousPreset`).
  2. Completes display topology switch & audio routing.
  3. Closes hooks for `previousPreset` via `_appHookService.CloseHooksForPresetAsync(previousPreset)`.
  4. Launches hooks for `newPreset` via `_appHookService.LaunchHooksForPresetAsync(newPreset)`.
- Dispatches hook errors non-fatally via diagnostic logging.

---

## 4. UI Integration (`RigSwitch.App`)

In `MainSettingsWindow.xaml` (under `PresetDetailTemplate`):
- "🚀 Launch Applications" section per preset.
- ItemsControl displaying configured hooks with:
  - Executable name & path preview.
  - "Close on switch away" checkbox.
  - Remove button ("✕").
- "+ Add Application" button opening standard Windows `OpenFileDialog` filtered to `Executable Files (*.exe)|*.exe`.

---

## 5. Verification & Testing

- **Unit Tests (`RigSwitch.Tests.Unit`)**:
  - `WindowsApplicationLifecycleHookServiceTests`: process mock provider testing launch, graceful close, kill fallback, and nonexistent executable handling.
  - `ProfileSwitchCoordinatorApplicationHookTests`: testing coordinator invoking close on outgoing preset and launch on incoming preset.
- **Controls & Quality**:
  - All files strictly `< 500` lines.
  - Zero warnings, 100% test pass rate.
