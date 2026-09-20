# UI Contrast Overhaul & Audio Cleanup Filtering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the white-on-white text box/control contrast issues across the dark OLED UI and provide search, presence status badges, and filtering in the Audio Cleanup tab.

**Architecture:** 
Enhance WPF ControlTemplates and Styles in `MainSettingsWindow.xaml` for `TextBox`, `ComboBox`, and `ComboBoxItem` so dark backgrounds (`#0D0D12`), borders (`#3A3A4E`), and crisp white text (`#FFFFFF`) are strictly enforced regardless of OS Aero/Fluent defaults.
Extend `AudioEndpointVisibilityItemViewModel` to store `DevicePresenceState`, exposing status badges and human-readable state indicators.
Add real-time filter properties (`AudioSearchText`, `AudioFilterSelection`) and a filtered view collection in `MainSettingsViewModel`, updating the Audio Cleanup tab with modern search and filter bars.

**Tech Stack:** C# .NET 10 WPF, CommunityToolkit/MVVM patterns, xUnit, NSubstitute.

## Global Constraints
- OLED Pitch Black aesthetic (#000000 window, #0D0D12 input surfaces, #3A3A4E borders, #FFFFFF text).
- Full compliance with user rule: Run `npm run lint` and `npx tsc --noEmit` after changes.
- All existing 242+ unit tests must pass.

---

### Task 1: Extend AudioEndpointVisibilityItemViewModel with State and Badges

**Files:**
- Modify: `src/RigSwitch.App/ViewModels/AudioEndpointVisibilityItemViewModel.cs`
- Modify: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs`
- Test: `tests/RigSwitch.Tests.Unit/AudioEndpointVisibilityViewModelTests.cs`

**Interfaces:**
- Consumes: `AudioEndpointInfo.State` (`DevicePresenceState`)
- Produces: `AudioEndpointVisibilityItemViewModel.State`, `StateLabel`, `StateBadgeBackground`, `StateBadgeForeground`

- [ ] **Step 1: Write unit tests for AudioEndpointVisibilityItemViewModel presence state**
- [ ] **Step 2: Run tests to verify failure**
- [ ] **Step 3: Implement presence state and badge properties on AudioEndpointVisibilityItemViewModel**
- [ ] **Step 4: Update MainSettingsViewModel instantiation of AudioEndpointVisibilityItemViewModel to pass state**
- [ ] **Step 5: Run tests and verify all pass**

---

### Task 2: Implement Search and Filter in MainSettingsViewModel for Audio Endpoints

**Files:**
- Modify: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs`
- Test: `tests/RigSwitch.Tests.Unit/AudioEndpointFilteringTests.cs`

**Interfaces:**
- Produces: `FilteredAudioEndpoints` (`ICollectionView` or filtered `ObservableCollection`), `AudioSearchText` (`string`), `AudioFilterSelection` (`string` or enum: All, Active, Inactive)

- [ ] **Step 1: Write unit tests for audio search and status filtering logic**
- [ ] **Step 2: Run tests to verify failure**
- [ ] **Step 3: Implement AudioSearchText, AudioFilterSelection, and FilteredAudioEndpoints in MainSettingsViewModel**
- [ ] **Step 4: Run tests to verify they pass**

---

### Task 3: Comprehensive Dark Theme Overhaul for TextBoxes, ComboBoxes, and Audio Cleanup UI

**Files:**
- Modify: `src/RigSwitch.App/Views/MainSettingsWindow.xaml`

- [ ] **Step 1: Replace default TextBox and ComboBox styles with explicit high-contrast OLED templates (solid dark surfaces, caret brush, dark popup & items)**
- [ ] **Step 2: Update Audio Cleanup tab with Search bar, filter pills/buttons, and status badges for Active/Disabled/Unplugged devices**
- [ ] **Step 3: Verify build and unit tests with `dotnet test`**

---

### Task 4: Capture UI Verification Screenshots and Verify Linter/Typecheck

**Files:**
- Test / Verify: Visual inspection via automated screenshot script or tool
- Verification: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 1: Launch application briefly or run screenshot capture to generate artifact screenshots**
- [ ] **Step 2: Run `dotnet test`, `npm run lint`, and `npx tsc --noEmit`**
- [ ] **Step 3: Present results and screenshots to user**
