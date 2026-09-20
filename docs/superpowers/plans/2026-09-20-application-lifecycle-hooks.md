# Application Lifecycle Hooks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement preset application lifecycle hooks so RigSwitch automatically launches specified executables when entering a preset, and gracefully closes them when switching away.

**Architecture:** Create `PresetApplicationHook` model, `IApplicationLifecycleHookService` contract and Windows process implementation, integrate into `ProfileSwitchCoordinator`, and add UI in `MainSettingsWindow.xaml`.

**Tech Stack:** .NET 10.0 (`net10.0-windows`), C# 13, `System.Diagnostics.Process`, WPF.

## Global Constraints
- Target Framework: `net10.0-windows`
- Anti-Bloat: Strict ban on `*Manager`, `*Helper`, or `*Util` junk drawers.
- Strict limit: Max 500 lines of code per file.
- `Nullable: enable`, `ImplicitUsings: enable`, `TreatWarningsAsErrors: true`.
- Zero build warnings, zero compiler errors.
- 100% test pass rate across unit and simulation harness suites.

---

### Task 1: Domain Models & Service Contract

**Files:**
- Create: `src/RigSwitch.Core/Models/PresetApplicationHook.cs`
- Modify: `src/RigSwitch.Core/Models/WorkstationPreset.cs`
- Create: `src/RigSwitch.Core/Interfaces/IApplicationLifecycleHookService.cs`
- Test: `tests/RigSwitch.Tests.Unit/PresetApplicationHookTests.cs`

- [ ] **Step 1: Write unit tests for PresetApplicationHook**
- [ ] **Step 2: Create PresetApplicationHook.cs and update WorkstationPreset.cs**
- [ ] **Step 3: Define IApplicationLifecycleHookService.cs**
- [ ] **Step 4: Run unit tests to confirm pass**

---

### Task 2: Windows Application Lifecycle Hook Service Implementation

**Files:**
- Create: `src/RigSwitch.Infrastructure/Windows/Processes/INativeProcessProvider.cs`
- Create: `src/RigSwitch.Infrastructure/Windows/Processes/WindowsNativeProcessProvider.cs`
- Create: `src/RigSwitch.Infrastructure/Windows/Processes/WindowsApplicationLifecycleHookService.cs`
- Test: `tests/RigSwitch.Tests.Unit/WindowsApplicationLifecycleHookServiceTests.cs`

- [ ] **Step 1: Define mockable INativeProcessProvider and WindowsNativeProcessProvider**
- [ ] **Step 2: Write unit tests for WindowsApplicationLifecycleHookService with launch, graceful close, kill fallback**
- [ ] **Step 3: Implement WindowsApplicationLifecycleHookService.cs**
- [ ] **Step 4: Run unit tests to verify 100% pass**

---

### Task 3: ProfileSwitchCoordinator Integration & Unit Tests

**Files:**
- Modify: `src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs`
- Modify: `tests/RigSwitch.Tests.Unit/ProfileSwitchCoordinatorTests.cs`
- Modify: `src/RigSwitch.App/App.xaml.cs`

- [ ] **Step 1: Update coordinator constructor to accept IApplicationLifecycleHookService**
- [ ] **Step 2: Wire CloseHooksForPresetAsync on outgoing preset and LaunchHooksForPresetAsync on incoming preset**
- [ ] **Step 3: Wire DI container in App.xaml.cs**
- [ ] **Step 4: Add unit tests in ProfileSwitchCoordinatorTests and verify pass**

---

### Task 4: WPF Settings UI for Application Hooks

**Files:**
- Create: `src/RigSwitch.App/ViewModels/ApplicationHookItemViewModel.cs`
- Modify: `src/RigSwitch.App/ViewModels/PresetConfigurationItemViewModel.cs`
- Modify: `src/RigSwitch.App/Views/MainSettingsWindow.xaml`

- [ ] **Step 1: Implement ApplicationHookItemViewModel and bind collection in PresetConfigurationItemViewModel**
- [ ] **Step 2: Add "+ Add Application" command with OpenFileDialog**
- [ ] **Step 3: Add Application Hooks section to MainSettingsWindow.xaml**
- [ ] **Step 4: Verify build, formatting, line count < 500 lines, and run full test suite**
