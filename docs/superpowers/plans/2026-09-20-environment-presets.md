# Environment Presets Engine & Display Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Environment Presets Engine enabling 3 configurable presets per profile (`Desk` and `SimRig`), direct preset switching, tray submenus, and quick display settings access.

**Architecture:** Extend domain records (`WorkstationPreset`, `UserSettings`), update `IProfileSwitchCoordinator` to coordinate preset-level switching with fail-safe reachability checks, add legacy auto-migration in `JsonSettingsStorageService`, overhaul Tray context submenus, and update the WPF MVVM Settings Window.

**Tech Stack:** .NET 10.0 (`net10.0-windows`), C# 13, WPF, Win32 CCD, CoreAudio COM, xUnit, NSubstitute.

## Global Constraints
- Target Framework: `net10.0-windows`
- Anti-Bloat: Strict ban on `*Manager`, `*Helper`, or `*Util` junk drawers.
- Strict limit: Max 500 lines of code per file.
- `Nullable: enable`, `ImplicitUsings: enable`, `TreatWarningsAsErrors: true`.
- Zero build warnings, zero compiler errors.
- 100% test pass rate across unit and simulation harness suites.

---

### Task 1: Domain Models & Interface Contracts

**Files:**
- Create: `src/RigSwitch.Core/Models/WorkstationPreset.cs`
- Modify: `src/RigSwitch.Core/Models/UserSettings.cs`
- Modify: `src/RigSwitch.Core/Events/ProfileChangedEventArgs.cs`
- Modify: `src/RigSwitch.Core/Interfaces/IProfileSwitchCoordinator.cs`
- Test: `tests/RigSwitch.Tests.Unit/WorkstationPresetTests.cs`

**Interfaces:**
- Produces: `WorkstationPreset` record, updated `UserSettings` with `DeskPresets` and `RigPresets` (3 slots each), `ActiveDeskPresetIndex`, `ActiveRigPresetIndex`, and updated `IProfileSwitchCoordinator` with `SwitchToPresetAsync`.

- [ ] **Step 1: Write unit tests for WorkstationPreset and UserSettings defaults**
- [ ] **Step 2: Implement WorkstationPreset.cs**
- [ ] **Step 3: Update UserSettings.cs, ProfileChangedEventArgs.cs, and IProfileSwitchCoordinator.cs**
- [ ] **Step 4: Run tests to verify Core builds and tests pass**

---

### Task 2: Settings Storage Migration & Unit Tests

**Files:**
- Modify: `src/RigSwitch.Infrastructure/Storage/JsonSettingsStorageService.cs`
- Modify: `tests/RigSwitch.Tests.Unit/JsonSettingsStorageServiceTests.cs`

**Description:**
Ensure `JsonSettingsStorageService` gracefully auto-migrates existing settings that lack presets by populating the 3 preset slots and copying legacy configuration to Preset 0.

- [ ] **Step 1: Write migration test for legacy JSON settings**
- [ ] **Step 2: Update JsonSettingsStorageService.cs with migration logic**
- [ ] **Step 3: Run unit tests to verify migration passes**

---

### Task 3: ProfileSwitchCoordinator Presets Implementation

**Files:**
- Modify: `src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs`
- Modify: `tests/RigSwitch.Tests.Unit/ProfileSwitchCoordinatorTests.cs`
- Modify: `tests/RigSwitch.Tests.Unit/ReviewFixesTests.cs`

**Description:**
Update `ProfileSwitchCoordinator` to switch displays and route audio using the active preset of the target profile, and implement `SwitchToPresetAsync` for direct preset activation.

- [ ] **Step 1: Write unit tests for preset-level switching and reachability validation**
- [ ] **Step 2: Implement preset switching in ProfileSwitchCoordinator.cs**
- [ ] **Step 3: Run unit tests to confirm all tests pass**

---

### Task 4: TrayIconService Preset Submenus & Dynamic Tooltips

**Files:**
- Modify: `src/RigSwitch.App/Services/TrayIconService.cs`
- Modify: `src/RigSwitch.App/App.xaml.cs`

**Description:**
Overhaul the system tray context menu to display nested submenus for Desk Presets and Sim Rig Presets with checkmarks indicating the active preset, and dynamic tooltip showing setup and preset name.

- [ ] **Step 1: Update TrayIconService.cs with preset submenus and tooltip formatting**
- [ ] **Step 2: Wire up preset notifications in App.xaml.cs**
- [ ] **Step 3: Verify build clean with 0 warnings**

---

### Task 5: MainSettingsViewModel & WPF Settings Window Presets Tab

**Files:**
- Modify: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs`
- Create: `src/RigSwitch.App/ViewModels/PresetConfigurationItemViewModel.cs`
- Modify: `src/RigSwitch.App/Views/MainSettingsWindow.xaml`

**Description:**
Update the Settings Window to display 3 preset cards each for Desk and Sim Rig profiles, with preset naming, target display, primary/fallback audio, and "Quick Display Settings" button (`ms-settings:display`).

- [ ] **Step 1: Create PresetConfigurationItemViewModel.cs**
- [ ] **Step 2: Update MainSettingsViewModel.cs with preset collections**
- [ ] **Step 3: Overhaul Profiles tab in MainSettingsWindow.xaml**
- [ ] **Step 4: Verify build clean with 0 warnings and strict < 500 lines limit**

---

### Task 6: Simulation Test Harness & Full Verification

**Files:**
- Modify: `tests/RigSwitch.Tests.Harness/Disturbances/SimulationDisturbanceContext.cs`
- Modify: `tests/RigSwitch.Tests.Harness/SimulationStressTests.cs`

**Description:**
Update the controls-grade simulation harness to stress-test rapid multi-preset switching under simulated hardware drop disturbances.

- [ ] **Step 1: Update SimulationDisturbanceContext with preset defaults**
- [ ] **Step 2: Add preset switching test cases to SimulationStressTests**
- [ ] **Step 3: Run full solution test suite in Debug and Release configurations**
