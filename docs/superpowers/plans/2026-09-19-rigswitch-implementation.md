# RigSwitch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a native Windows C# .NET system tray utility and hardware control plane to switch instantly between a Desk setup (MSI OLED display + Pebble V3 / MSI audio fallback) and a Sim Rig setup (Asus ultrawide display + Asus/Pebble V2 audio) using global hotkeys, tray menu, and a settings GUI.

**Architecture:** A decoupled multi-project .NET solution utilizing the modern `.slnx` format. Win32 CCD (`SetDisplayConfig`) manages single-display topology switching, CoreAudio COM (`IPolicyConfig`) routes default playback endpoints and toggles endpoint visibility, and a central `ProfileSwitchCoordinator` orchestrates atomic transitions with fail-safe safety gates and multi-tier audio fallback. The UI is a lightweight, dark-themed WPF desktop tray application with an MVVM settings panel.

**Tech Stack:** C# 13, .NET 10 SDK, WPF (Windows Presentation Foundation), Win32 CCD API (`user32.dll`), Windows CoreAudio COM (`IPolicyConfig`, `IMMDeviceEnumerator`), xUnit, NSubstitute.

## Global Constraints
- Target Framework: `net10.0-windows` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.
- Solution Format: Modern XML-based `.slnx`.
- Anti-Bloat & Modularity: Strict ban on `*Manager`, `*Helper`, or `*Util` junk drawers. Max 500 lines per file.
- Interfaces by default (`I*`) for all services.
- Test Coverage: $\ge$ 80% code coverage target across unit and simulation harness suites.
- Disturbance & Simulation: Controls-grade test harness with diagnostic ring buffer and 6-part agent feedback envelope.
- Git Flow: Fresh feature branch `feat/rigswitch-core` off `develop`, atomic Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `chore:`).

---

### Task 1: Solution Scaffolding & Directory.Build.props

**Files:**
- Create: `Directory.Build.props`
- Create: `RigSwitch.slnx`
- Create: `src/RigSwitch.Core/RigSwitch.Core.csproj`
- Create: `src/RigSwitch.Infrastructure/RigSwitch.Infrastructure.csproj`
- Create: `src/RigSwitch.App/RigSwitch.App.csproj`
- Create: `tests/RigSwitch.Tests.Unit/RigSwitch.Tests.Unit.csproj`
- Create: `tests/RigSwitch.Tests.Harness/RigSwitch.Tests.Harness.csproj`

**Interfaces:**
- Produces: Base project structure and compile targets for all subsequent tasks.

- [ ] **Step 1: Create `Directory.Build.props`**
Define standard compiler flags, nullable annotations, implicit usings, and treat warnings as errors across the entire solution.

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>13.0</LangVersion>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create project files for Core, Infrastructure, App, and Tests**
Set up project dependencies: `RigSwitch.App` references `RigSwitch.Core` and `RigSwitch.Infrastructure`. Test projects reference `Core` and `Infrastructure` with `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, and `NSubstitute`.

- [ ] **Step 3: Create modern `.slnx` solution file**
Link all five projects into `RigSwitch.slnx`.

- [ ] **Step 4: Verify build succeeds**
Run `dotnet build RigSwitch.slnx` to ensure empty projects compile with zero warnings and zero errors.

- [ ] **Step 5: Commit scaffolding**
```bash
git checkout -b feat/rigswitch-core
git add Directory.Build.props RigSwitch.slnx src/ tests/
git commit -m "chore: scaffold .slnx solution and project structure"
```

---

### Task 2: Core Domain Models & Interfaces

**Files:**
- Create: `src/RigSwitch.Core/Enums/ProfileMode.cs`
- Create: `src/RigSwitch.Core/Enums/DevicePresenceState.cs`
- Create: `src/RigSwitch.Core/Models/DisplayDeviceInfo.cs`
- Create: `src/RigSwitch.Core/Models/AudioEndpointInfo.cs`
- Create: `src/RigSwitch.Core/Models/UserSettings.cs`
- Create: `src/RigSwitch.Core/Interfaces/IDisplayConfigurationService.cs`
- Create: `src/RigSwitch.Core/Interfaces/IAudioEndpointDirector.cs`
- Create: `src/RigSwitch.Core/Interfaces/IGlobalHotkeyService.cs`
- Create: `src/RigSwitch.Core/Interfaces/ISettingsStorageService.cs`
- Create: `src/RigSwitch.Core/Interfaces/IProfileSwitchCoordinator.cs`

**Interfaces:**
- Consumes: Standard .NET runtime.
- Produces: The complete public API contract for all domain orchestration, display control, audio routing, and settings.

- [ ] **Step 1: Write Domain Enums and Data Models**
Implement `ProfileMode` (`Desk`, `SimRig`), `DevicePresenceState` (`Active`, `Disabled`, `NotPresent`, `Unplugged`), and records for `DisplayDeviceInfo`, `AudioEndpointInfo`, and `UserSettings` (with default mappings for `MSI4DD0`, `AUS3438`, and Pebble speakers).

- [ ] **Step 2: Write Service Interfaces**
Define `IDisplayConfigurationService`, `IAudioEndpointDirector`, `IGlobalHotkeyService`, `ISettingsStorageService`, and `IProfileSwitchCoordinator` with clean asynchronous signatures accepting `CancellationToken`.

- [ ] **Step 3: Build Core project**
Run `dotnet build src/RigSwitch.Core/RigSwitch.Core.csproj` to verify zero compile errors.

- [ ] **Step 4: Commit Core definitions**
```bash
git add src/RigSwitch.Core/
git commit -m "feat(core): add domain models, enums, and service interfaces"
```

---

### Task 3: Settings Storage Service

**Files:**
- Create: `src/RigSwitch.Infrastructure/Storage/JsonSettingsStorageService.cs`
- Test: `tests/RigSwitch.Tests.Unit/JsonSettingsStorageServiceTests.cs`

**Interfaces:**
- Consumes: `ISettingsStorageService`, `UserSettings`.
- Produces: Atomic file persistence in `%APPDATA%\RigSwitch\settings.json`.

- [ ] **Step 1: Write unit tests for `JsonSettingsStorageService`**
Test:
1. `LoadSettingsAsync_WhenFileDoesNotExist_ReturnsDefaultSettingsAndCreatesDirectory`
2. `SaveSettingsAsync_WritesValidJsonAndCanBeReloaded`
3. `LoadSettingsAsync_WhenFileCorrupt_FallsBackToDefaultsWithoutCrashing`

- [ ] **Step 2: Verify tests fail**
Run `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~JsonSettingsStorageServiceTests` $\rightarrow$ verify tests fail because `JsonSettingsStorageService` is not implemented.

- [ ] **Step 3: Implement `JsonSettingsStorageService`**
Implement thread-safe, atomic file write using temp file rename to prevent partial writes.

- [ ] **Step 4: Verify tests pass**
Run `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~JsonSettingsStorageServiceTests` $\rightarrow$ verify all 3 tests pass.

- [ ] **Step 5: Commit settings storage**
```bash
git add src/RigSwitch.Infrastructure/Storage/ tests/RigSwitch.Tests.Unit/
git commit -m "feat(infra): implement JsonSettingsStorageService with atomic persistence"
```

---

### Task 4: Win32 CCD Native Display Configuration Service

**Files:**
- Create: `src/RigSwitch.Infrastructure/Windows/Ccd/NativeCcdApi.cs`
- Create: `src/RigSwitch.Infrastructure/Windows/Ccd/WindowsDisplayConfigurationService.cs`
- Test: `tests/RigSwitch.Tests.Unit/WindowsDisplayConfigurationServiceTests.cs`

**Interfaces:**
- Consumes: `IDisplayConfigurationService`, `DisplayDeviceInfo`.
- Produces: Low-level Win32 CCD display queries and single-display topology switching via `QueryDisplayConfig` and `SetDisplayConfig`.

- [ ] **Step 1: Implement `NativeCcdApi.cs`**
Define P/Invoke declarations, structs (`DISPLAYCONFIG_PATH_INFO`, `DISPLAYCONFIG_MODE_INFO`, `DISPLAYCONFIG_TARGET_DEVICE_NAME`), and flags (`SDC_APPLY`, `SDC_USE_SUPPLIED_DISPLAY_CONFIG`, `SDC_SAVE_TO_DATABASE`, `QDC_ALL_PATHS`) for `user32.dll`.

- [ ] **Step 2: Write unit tests with simulated/mocked CCD provider**
Verify display enumeration parser maps monitor EDID IDs (`MSI4DD0`, `AUS3438`) and correctly computes target display paths and active flags.

- [ ] **Step 3: Implement `WindowsDisplayConfigurationService`**
Implement `EnumerateDisplaysAsync` and `ApplySingleDisplayTopologyAsync`. Check target monitor is present in active or connected paths before applying; set target source position to (0,0) with primary flags, set inactive path flags to 0 (disabled), and invoke `SetDisplayConfig`.

- [ ] **Step 4: Run unit tests**
Run `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~Display` $\rightarrow$ verify tests pass.

- [ ] **Step 5: Commit display configuration service**
```bash
git add src/RigSwitch.Infrastructure/Windows/Ccd/ tests/RigSwitch.Tests.Unit/
git commit -m "feat(infra): implement WindowsDisplayConfigurationService with Win32 CCD"
```

---

### Task 5: CoreAudio Native Endpoint Director & Fallback Logic

**Files:**
- Create: `src/RigSwitch.Infrastructure/Windows/CoreAudio/ComInterfaces.cs`
- Create: `src/RigSwitch.Infrastructure/Windows/CoreAudio/CoreAudioEndpointDirector.cs`
- Test: `tests/RigSwitch.Tests.Unit/CoreAudioEndpointDirectorTests.cs`

**Interfaces:**
- Consumes: `IAudioEndpointDirector`, `AudioEndpointInfo`.
- Produces: Audio routing via `IPolicyConfig::SetDefaultEndpoint` and endpoint visibility control.

- [ ] **Step 1: Implement `ComInterfaces.cs`**
Declare COM interop interfaces for `IMMDeviceEnumerator`, `IMMDevice`, `IMMDeviceCollection`, `IPropertyStore`, and `IPolicyConfig` (`Guid("f8679f50-850a-41cf-9c72-430f290290c8")`).

- [ ] **Step 2: Write unit tests for audio fallback logic and visibility filtering**
Test:
1. `ResolveDefaultEndpoint_WhenPebbleV3Active_SelectsPebbleV3`
2. `ResolveDefaultEndpoint_WhenPebbleV3Unplugged_FallsBackToMsiOled`
3. `SyncEndpointVisibility_DisablesUnwantedEndpoints`

- [ ] **Step 3: Implement `CoreAudioEndpointDirector`**
Implement:
- `EnumerateAudioEndpointsAsync`: queries active, unplugged, and disabled render devices.
- `SetDefaultPlaybackEndpointAsync`: invokes `IPolicyConfig::SetDefaultEndpoint` for `eConsole`, `eMultimedia`, and `eCommunications`.
- `SetEndpointVisibilityAsync`: changes device state or writes registry flag to enable/disable specific MMDevice endpoints.

- [ ] **Step 4: Run audio unit tests**
Run `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~Audio` $\rightarrow$ verify all tests pass.

- [ ] **Step 5: Commit audio endpoint director**
```bash
git add src/RigSwitch.Infrastructure/Windows/CoreAudio/ tests/RigSwitch.Tests.Unit/
git commit -m "feat(infra): implement CoreAudioEndpointDirector with IPolicyConfig and fallback"
```

---

### Task 6: Profile Switch Coordinator

**Files:**
- Create: `src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs`
- Test: `tests/RigSwitch.Tests.Unit/ProfileSwitchCoordinatorTests.cs`

**Interfaces:**
- Consumes: `IDisplayConfigurationService`, `IAudioEndpointDirector`, `ISettingsStorageService`.
- Produces: `IProfileSwitchCoordinator`, atomic profile transition events.

- [ ] **Step 1: Write unit tests for `ProfileSwitchCoordinator`**
Test:
1. `SwitchProfileAsync_DeskToRig_ExecutesDisplayThenAudio`
2. `SwitchProfileAsync_WhenRigMonitorMissing_AbortsSafelyWithoutDisablingDeskDisplay`
3. `SwitchProfileAsync_WhenCancellationRequested_UnwindsCleanly`
4. `SwitchProfileAsync_DeskProfile_ResolvesPebbleWithMsiFallback`

- [ ] **Step 2: Verify tests fail**
Run `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~ProfileSwitchCoordinatorTests` $\rightarrow$ verify fail.

- [ ] **Step 3: Implement `ProfileSwitchCoordinator`**
Orchestrate:
1. Verify target display is reachable. If not, raise fail-safe error and abort.
2. Call `IDisplayConfigurationService.ApplySingleDisplayTopologyAsync`.
3. Resolve audio endpoint (with multi-tier fallback for Desk).
4. Call `IAudioEndpointDirector.SetDefaultPlaybackEndpointAsync`.
5. Call `IAudioEndpointDirector.SetEndpointVisibilityAsync` for hidden list.
6. Update `CurrentProfile` and fire `ProfileChanged` event.

- [ ] **Step 4: Verify tests pass**
Run `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~ProfileSwitchCoordinatorTests` $\rightarrow$ verify pass.

- [ ] **Step 5: Commit coordinator**
```bash
git add src/RigSwitch.Core/Services/ tests/RigSwitch.Tests.Unit/
git commit -m "feat(core): implement ProfileSwitchCoordinator with fail-safe gates"
```

---

### Task 7: Global Hotkey Service

**Files:**
- Create: `src/RigSwitch.Infrastructure/Windows/Hotkeys/WindowsGlobalHotkeyService.cs`
- Test: `tests/RigSwitch.Tests.Unit/WindowsGlobalHotkeyServiceTests.cs`

**Interfaces:**
- Consumes: `IGlobalHotkeyService`.
- Produces: Low-level `RegisterHotKey` hooks dispatching to background actions.

- [ ] **Step 1: Write unit tests for hotkey registry & dispatch mapping**
Test registration, collision prevention, unregistration, and callback dispatching.

- [ ] **Step 2: Implement `WindowsGlobalHotkeyService`**
Create a lightweight hidden message-only window (`HwndSource`) that listens for `WM_HOTKEY` (0x0312) and invokes registered actions on the UI/dispatcher thread.

- [ ] **Step 3: Run unit tests**
Run `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~Hotkey` $\rightarrow$ verify pass.

- [ ] **Step 4: Commit hotkey service**
```bash
git add src/RigSwitch.Infrastructure/Windows/Hotkeys/ tests/RigSwitch.Tests.Unit/
git commit -m "feat(infra): implement WindowsGlobalHotkeyService using Win32 WM_HOTKEY"
```

---

### Task 8: Controls-Grade Simulation Test Harness & Disturbance Injection

**Files:**
- Create: `tests/RigSwitch.Tests.Harness/Taps/DiagnosticRingBuffer.cs`
- Create: `tests/RigSwitch.Tests.Harness/Disturbances/SimulationDisturbanceContext.cs`
- Create: `tests/RigSwitch.Tests.Harness/Disturbances/MonitorUnpluggedDisturbance.cs`
- Create: `tests/RigSwitch.Tests.Harness/Disturbances/AudioFallbackDisturbance.cs`
- Create: `tests/RigSwitch.Tests.Harness/SimulationHarnessRunner.cs`
- Create: `tests/RigSwitch.Tests.Harness/SimulationStressTests.cs`

**Interfaces:**
- Consumes: `ProfileSwitchCoordinator`, all mockable infrastructure services.
- Produces: 50-switch stress testing, disturbance injection, and 6-part agent feedback envelope formatting.

- [ ] **Step 1: Implement `DiagnosticRingBuffer` and 6-part feedback envelope**
Create a 100-event circular buffer recording transitions, timestamps, and error envelopes formatted as JSON.

- [ ] **Step 2: Implement disturbance injection scenarios**
- `MonitorUnpluggedDisturbance`: Intercepts display query to simulate an unreachable Asus monitor.
- `AudioFallbackDisturbance`: Simulates Pebble V3 being unplugged or plugged in during switch loops.

- [ ] **Step 3: Implement `SimulationHarnessRunner` & 50-switch stress tests**
Run 50 rapid back-and-forth transitions with random cancellations and disturbances. Assert 100% convergence, zero deadlocks, and zero orphaned display states.

- [ ] **Step 4: Execute simulation harness tests**
Run `dotnet test tests/RigSwitch.Tests.Harness` $\rightarrow$ confirm stress tests pass.

- [ ] **Step 5: Commit simulation harness**
```bash
git add tests/RigSwitch.Tests.Harness/
git commit -m "test(harness): implement controls-grade simulation harness and disturbance tests"
```

---

### Task 9: WPF Desktop Application, System Tray & Settings Window

**Files:**
- Create: `src/RigSwitch.App/App.xaml` & `App.xaml.cs`
- Create: `src/RigSwitch.App/Services/TrayIconService.cs`
- Create: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs`
- Create: `src/RigSwitch.App/ViewModels/AudioVisibilityViewModel.cs`
- Create: `src/RigSwitch.App/Views/MainSettingsWindow.xaml` & `MainSettingsWindow.xaml.cs`

**Interfaces:**
- Consumes: All Core and Infrastructure services via `Microsoft.Extensions.DependencyInjection`.
- Produces: System tray icon with context menu, toast notifications, and dark-themed Settings GUI.

- [ ] **Step 1: Implement `TrayIconService`**
Create tray icon using `System.Windows.Forms.NotifyIcon` with custom SVG/ICO rendering:
- Left-click: Toggle active profile (Desk $\leftrightarrow$ Rig).
- Right-click context menu: "Desk Setup (MSI OLED)", "Sim Rig Setup (Asus)", "---", "Settings...", "Exit".
- Balloon/Toast notifications on profile switch.

- [ ] **Step 2: Implement ViewModels with reactive MVVM bindings**
- `MainSettingsViewModel`: profile selection, custom device nicknames, test switch buttons, hotkey configuration.
- `AudioVisibilityViewModel`: checkable list of audio endpoints with real-time toggle to hide/show in Windows.

- [ ] **Step 3: Implement `MainSettingsWindow.xaml`**
Dark theme, clean layout, tabs for:
1. Setup Profiles (Desk vs Sim Rig)
2. Device Renamer (custom nicknames)
3. Audio Visibility (checkboxes to hide Sonar, Steam, Oculus)
4. Hotkeys (pick keys for toggle, desk, rig)

- [ ] **Step 4: Bootstrap `App.xaml.cs` with DI Host**
Initialize `HostApplicationBuilder`, register services as singletons, bind tray icon, register global hotkeys on startup, and run application in background mode without showing main window until requested.

- [ ] **Step 5: Build and verify WPF application compiles**
Run `dotnet build src/RigSwitch.App/RigSwitch.App.csproj` $\rightarrow$ verify zero errors and zero warnings.

- [ ] **Step 6: Commit WPF application**
```bash
git add src/RigSwitch.App/
git commit -m "feat(app): implement WPF system tray service, MVVM settings window, and DI bootstrap"
```

---

### Task 10: End-to-End System Smoke & Integration Verification

**Files:**
- Create: `ARCHITECTURE.md`
- Create: `README.md`
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: Full solution build artifacts and live hardware.
- Produces: Verified working switching tool, living documentation, and CI pipeline.

- [ ] **Step 1: Create living documentation (`ARCHITECTURE.md` & `README.md`)**
Document architecture, Mermaid topology and sequence diagrams, CLI and hotkey reference, and quickstart instructions.

- [ ] **Step 2: Create 4-stage GitHub Actions CI workflow**
Add `.github/workflows/ci.yml` adhering to Steven's toolbelt standards (Gate 1: Release & link integrity; Gate 2: Parallel builds & xUnit tests; Gate 3: Smoke test).

- [ ] **Step 3: Run full solution build and all tests**
Run:
```bash
dotnet build RigSwitch.slnx --configuration Release
dotnet test RigSwitch.slnx --configuration Release --no-build
```
Verify 100% tests pass and code coverage $\ge$ 80%.

- [ ] **Step 4: Live Hardware Smoke Verification**
Execute dry-run and live switch verification using detected hardware IDs (`MSI4DD0`, `AUS3438`, `Pebble V3`, `VG34VQL3A`), verifying display and audio states in Windows.

- [ ] **Step 5: Final Commit & PR readiness**
```bash
git add ARCHITECTURE.md README.md .github/
git commit -m "docs: add living architecture documentation and 4-stage CI workflow"
```
