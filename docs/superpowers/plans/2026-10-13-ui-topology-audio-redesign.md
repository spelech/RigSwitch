# UI, Topology & Ranked Audio Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Per-preset sidebar pages with a 2D monitor topology canvas, ranked audio/mic priority lists, a "Relevant Devices" audio filter, a `+ Add Nickname` workflow, a larger default window and a visible version.

**Architecture:** Extend `WorkstationPreset` with backward-compatible `MonitorPlacements`, `AudioPriorityIds`, `MicrophonePriorityIds`. Core services (`ProfileSwitchCoordinator`, `AudioEndpointWatchdogService`) resolve the first `Active` entry of the ranked list. The WPF layer gains per-preset page ViewModels; the monolithic XAML is split into per-page UserControls where new.

**Tech Stack:** .NET / WPF (Fluent), xUnit, Windows CCD + CoreAudio P/Invoke.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-13-ui-topology-audio-redesign-design.md` (source of truth).
- Default window `1240×840`, `MinWidth=1040`, `MinHeight=680`.
- Version shown as `vX.Y.Z` in header and sidebar footer.
- Existing configs (`PrimaryAudioId/SecondaryAudioId/FallbackAudioId`, mic equivalents, `TargetMonitorIds`) must load and keep working (migration, no data loss).
- Audio page keeps both Fluent toggles per row: `Win Menu` (left) and `Exclude / Bass Shaker Lockout` (right).
- After every code change run: `dotnet build`, `dotnet test`, `npm run lint`, `npx tsc --noEmit`.
- Validate UI with `MainSettingsWindow.CaptureScreenshotAsync` + `view_file` before claiming done.
- Preserve unrelated comments/docstrings.

---

### Task 1: Commit audio stutter fix (DONE)

Committed as `733a987` on `fix/audio-stutter-watchdog`.

---

### Task 2: Preset model — placements and ranked lists

**Files:**
- Modify: `src/RigSwitch.Core/Models/WorkstationPreset.cs`
- Create: `src/RigSwitch.Core/Models/MonitorPlacement.cs`
- Modify: `src/RigSwitch.Core/Models/DisplayDeviceInfo.cs` (add `PositionX`, `PositionY`, `Width`, `Height`)
- Test: `tests/RigSwitch.Tests.Unit/WorkstationPresetRankingTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record MonitorPlacement(string MonitorId, int X, int Y, bool IsPrimary, PresetHdrMode HdrMode)`
  - `WorkstationPreset.MonitorPlacements : List<MonitorPlacement>`
  - `WorkstationPreset.AudioPriorityIds : List<string>`, `MicrophonePriorityIds : List<string>`
  - `IReadOnlyList<string> WorkstationPreset.GetEffectiveAudioPriorityList()` / `GetEffectiveMicrophonePriorityList()` — returns the explicit list if non-empty, else `[Primary, Secondary, Fallback]` with blanks removed and case-insensitive de-dup.
  - `DisplayDeviceInfo.PositionX/PositionY/Width/Height : int { get; init; }`

- [ ] **Step 1: Write failing tests**

```csharp
using RigSwitch.Core.Models;
using Xunit;

namespace RigSwitch.Tests.Unit;

public class WorkstationPresetRankingTests
{
    [Fact]
    public void EffectiveAudioList_FallsBackToLegacyFields()
    {
        var p = new WorkstationPreset { PrimaryAudioId = "a", SecondaryAudioId = "", FallbackAudioId = "A" };
        Assert.Equal(new[] { "a" }, p.GetEffectiveAudioPriorityList());
    }

    [Fact]
    public void EffectiveAudioList_PrefersExplicitList()
    {
        var p = new WorkstationPreset { PrimaryAudioId = "legacy", AudioPriorityIds = ["x", "y"] };
        Assert.Equal(new[] { "x", "y" }, p.GetEffectiveAudioPriorityList());
    }

    [Fact]
    public void EffectiveMicList_FallsBackToLegacyFields()
    {
        var p = new WorkstationPreset { PrimaryMicrophoneId = "m1", FallbackMicrophoneId = "m2" };
        Assert.Equal(new[] { "m1", "m2" }, p.GetEffectiveMicrophonePriorityList());
    }
}
```

- [ ] **Step 2:** Run `dotnet test --filter WorkstationPresetRankingTests` — expect compile FAIL.
- [ ] **Step 3: Implement.** In `WorkstationPreset` add:

```csharp
public List<MonitorPlacement> MonitorPlacements { get; set; } = [];
public List<string> AudioPriorityIds { get; set; } = [];
public List<string> MicrophonePriorityIds { get; set; } = [];

public IReadOnlyList<string> GetEffectiveAudioPriorityList() =>
    ResolveRanking(AudioPriorityIds, PrimaryAudioId, SecondaryAudioId, FallbackAudioId);

public IReadOnlyList<string> GetEffectiveMicrophonePriorityList() =>
    ResolveRanking(MicrophonePriorityIds, PrimaryMicrophoneId, SecondaryMicrophoneId, FallbackMicrophoneId);

private static IReadOnlyList<string> ResolveRanking(List<string> explicitList, params string[] legacy)
{
    IEnumerable<string> source = explicitList is { Count: > 0 } ? explicitList : legacy;
    return source.Where(s => !string.IsNullOrWhiteSpace(s))
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .ToList();
}
```

`MonitorPlacement.cs`: the record above in namespace `RigSwitch.Core.Models` (`using RigSwitch.Core.Enums;`). Add the four int props to `DisplayDeviceInfo`.
- [ ] **Step 4:** Re-run tests — PASS. Also verify existing config JSON deserialization tests still pass (`dotnet test`).
- [ ] **Step 5:** `git add -A; git commit -m "feat: add ranked audio lists and monitor placements to presets"`

---

### Task 3: CCD positions — read, capture and apply placements

**Files:**
- Modify: `src/RigSwitch.Infrastructure/Windows/Ccd/WindowsDisplayConfigurationService.cs` (populate `PositionX/Y/Width/Height` in `EnumerateDisplaysAsync`; apply `MonitorPlacements` positions in `ApplyDisplayTopologyAsync`, falling back to current behaviour when empty)
- Modify: `src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs` (pass preset placements to topology apply)
- Modify: `IDisplayConfigurationService` (add optional `IReadOnlyList<MonitorPlacement>? placements = null` parameter; update fakes in tests)
- Test: `tests/RigSwitch.Tests.Unit/ProfileSwitchCoordinatorPlacementTests.cs`

**Interfaces:**
- Consumes: Task 2 `MonitorPlacements`.
- Produces: `ApplyDisplayTopologyAsync(IReadOnlyList<string> monitorIds, string primaryId, IReadOnlyList<MonitorPlacement>? placements, CancellationToken ct)` (match the existing signature's naming when editing; only append `placements`).

- [ ] **Step 1:** Write a test with the existing fake display service asserting that switching to a preset with two `MonitorPlacements` forwards them (record the argument on the fake) and that a legacy preset forwards `null`/empty.
- [ ] **Step 2:** Run it, expect FAIL (compile).
- [ ] **Step 3:** Implement parameter plumbing in interface, Windows service, coordinator and all test fakes. In the Windows service, after cloning modes, for each placement matching a target `monitorId` set `sourceMode.position = new POINTL { x = p.X, y = p.Y }`; the primary placement must be normalized to (0,0) by shifting all others by its offset.
- [ ] **Step 4:** `dotnet test` PASS.
- [ ] **Step 5:** Commit `feat: apply per-preset monitor placements via CCD`.

---

### Task 4: Ranked audio routing in coordinator and watchdog

**Files:**
- Modify: `src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs` (resolve route target and `ToggleAudioOutputModeAsync` cycle)
- Modify: `src/RigSwitch.Infrastructure/Windows/CoreAudio/AudioEndpointWatchdogService.cs` (`ResolveEffectiveTargetEndpoint` uses ranked list)
- Test: extend `tests/RigSwitch.Tests.Unit/AudioEndpointWatchdogServiceTests.cs`, `ProfileSwitchCoordinatorAudioToggleTests.cs`

**Interfaces:**
- Consumes: `GetEffectiveAudioPriorityList()`, `GetEffectiveMicrophonePriorityList()`.
- Produces: behaviour — first `Active`, non-blacklisted endpoint in rank wins; toggle selects the next `Active` endpoint after the current default (wrapping); if ≤1 active, no-op.

- [ ] **Step 1:** Add failing tests: (a) `AudioPriorityIds = [A(NotPresent), B(Active), C(Active)]` → watchdog and switch target B; (b) toggle with default=B moves to C, then back to B; (c) blacklisted top entry skipped.
- [ ] **Step 2:** Run filtered tests — expect FAIL.
- [ ] **Step 3:** Replace the Primary/Secondary/Fallback walk in both services with iteration over the effective list (reuse existing `IsEndpointAvailable`). Keep `onIntentionalAudioSwitch` invocation before every set-default call. Apply the same logic to microphones.
- [ ] **Step 4:** `dotnet test` PASS (all existing tests included).
- [ ] **Step 5:** Commit `feat: route audio and mic by ranked priority list`.

---

### Task 5: ViewModel layer

**Files:**
- Modify: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs`, `PresetConfigurationItemViewModel.cs`, `DeviceOptionResolver` (in `src/RigSwitch.App/ViewModels/`)
- Create: `src/RigSwitch.App/ViewModels/PresetMonitorCanvasItemViewModel.cs`, `RankedAudioItemViewModel.cs`, `NicknameItemViewModel.cs`
- Test: `tests/RigSwitch.Tests.Unit/MainSettingsViewModelRedesignTests.cs`

**Interfaces (Produces):**
- `MainSettingsViewModel`: `string AppVersionText` (`"v" + assembly InformationalVersion` trimmed of `+hash`), `PresetConfigurationItemViewModel? SelectedPreset`, `bool IsPresetViewSelected`, `NavigateToPresetCommand(PresetConfigurationItemViewModel)`, `AudioDeviceFilter` enum `{ Relevant, All }` + `ShowAllRegistryGhosts` toggle, `ObservableCollection<NicknameItemViewModel> ConfiguredNicknames`, `ObservableCollection<DeviceOption> UnnicknamedDeviceOptions`, `AddNicknameCommand`, `RemoveNicknameCommand`.
- `PresetConfigurationItemViewModel`: `ObservableCollection<PresetMonitorCanvasItemViewModel> StageMonitors`, `ObservableCollection<PresetMonitorCanvasItemViewModel> UnassignedMonitors`, commands `AddMonitorToStage`, `RemoveMonitorFromStage`, `SetPrimary`, `CycleHdr`, `CaptureWindowsLayoutCommand`, `MoveMonitor(double x, double y)` (snaps edges within 24 px); `ObservableCollection<RankedAudioItemViewModel> AudioRanking` and `MicrophoneRanking` with `MoveUp/MoveDown/Remove`, `AddAudioCommand(DeviceOption)`, and write-back to `AudioPriorityIds` / `MicrophonePriorityIds` / `MonitorPlacements` / `TargetMonitorIds` on save.
- `RankedAudioItemViewModel`: `Rank`, `DisplayName`, `Status` (`Active Now`, `Ready`, `Offline`).
- Relevant filter rule: show `Active`, `Disabled`, `Unplugged`, plus any `NotPresent` referenced by a preset ranking, blacklist or nickname.

- [ ] **Step 1:** Write failing tests, one per rule: relevant filter hides unreferenced `NotPresent` but keeps one referenced by a nickname; `UnnicknamedDeviceOptions` excludes already-nicknamed devices; `MoveUp` on rank 2 swaps and persists to `AudioPriorityIds`; `RemoveMonitorFromStage` moves the monitor to `UnassignedMonitors` and drops its placement; `SetPrimary` normalizes the primary to (0,0); `MoveMonitor` snaps to a neighbour's edge; `AppVersionText` matches `^v\d+\.\d+\.\d+`.
- [ ] **Step 2:** Run — expect FAIL.
- [ ] **Step 3:** Implement the ViewModels (use the existing `RelayCommand` / observable base the project already uses in `MainSettingsViewModel`). Replace legacy per-preset Primary/Secondary/Fallback dropdown properties on the UI side only; the model keeps legacy fields for compatibility, and on save write `Primary/Secondary/Fallback` from the first three ranking entries.
- [ ] **Step 4:** `dotnet test` PASS.
- [ ] **Step 5:** Commit `feat: viewmodels for preset pages, ranking, topology canvas and nicknames`.

---

### Task 6: XAML — window, sidebar, preset page, audio and nickname pages

**Files:**
- Modify: `src/RigSwitch.App/Views/MainSettingsWindow.xaml` and `.xaml.cs`
- Create: `src/RigSwitch.App/Views/PresetPageView.xaml/.cs` (monitor stage + tray + rankings), optionally `MonitorStageControl.cs` for drag/snap hit-testing
- Test: `tests/RigSwitch.Tests.Unit/UiScreenshotCaptureTests.cs` (add per-view captures)

- [ ] **Step 1:** Add failing screenshot test assertions: window `Width==1240`, `Height==840`, `MinWidth==1040`, `MinHeight==680`; version text element present (`AutomationId="AppVersionBadge"`) in header.
- [ ] **Step 2:** Run — expect FAIL.
- [ ] **Step 3:** Implement:
  - Window sizes and version badge (header and sidebar footer bound to `AppVersionText`).
  - Remove `Presets`, `Displays & Topology` and `Hardware Telemetry Summary` tabs; add sidebar groups `Desk 1..3` and `Sim Rig 1..3` bound to `NavigateToPresetCommand`.
  - `PresetPageView`: Canvas stage (monitors as draggable `Border` with Thumb drag → `MoveMonitor`), per-monitor buttons `★ Primary (0,0)`, HDR cycle, `✕`; slim "Off in Preset" tray with click/drag-to-stage; `🔍 Identify`, `📸 Capture Windows Layout`; ranked audio/mic cards with `#n`, status pill, `↑ ↓ ✕`, `+ Add device` dropdown.
  - Audio page: `Relevant Devices` / `All Registry Ghosts` filter bar; keep both Fluent toggles per row.
  - Nicknames page: `+ Add New Device Nickname` bar (dropdown of `UnnicknamedDeviceOptions`), list of configured nicknames with inline edit and `✕`.
- [ ] **Step 4:** Build, run tests — PASS.
- [ ] **Step 5:** Commit `feat: redesigned settings window with per-preset pages`.

---

### Task 7: Visual validation, checks and PR

- [ ] **Step 1:** Run screenshot capture for views: `Desk 1`, `Sim Rig 1`, `Audio & DAC Lockout`, `Device Nicknames`, at default size and at `MinWidth×MinHeight`. `view_file` each PNG; fix clipping or cramped layout, re-capture.
- [ ] **Step 2:** `dotnet build`, `dotnet test`, `npm run lint`, `npx tsc --noEmit` — all clean.
- [ ] **Step 3:** `git push -u origin fix/audio-stutter-watchdog`; open PR via `gh pr create` summarising stutter fix + redesign, attaching screenshots.
