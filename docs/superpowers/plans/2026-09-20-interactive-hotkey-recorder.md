# Interactive Hotkey Recorder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement an interactive Hotkey Recorder control in RigSwitch so users can record 2- or 3-key combinations by pressing keys directly instead of typing strings.

**Architecture:** Create a `HotkeyGestureParser` in Core for parsing and validating key combinations, a custom `HotkeyRecorderControl` in WPF, and update `MainSettingsWindow.xaml` to replace manual hotkey textboxes.

**Tech Stack:** .NET 10.0 (`net10.0-windows`), C# 13, WPF.

## Global Constraints
- Target Framework: `net10.0-windows`
- Anti-Bloat: Strict ban on `*Manager`, `*Helper`, or `*Util` junk drawers.
- Strict limit: Max 500 lines of code per file.
- `Nullable: enable`, `ImplicitUsings: enable`, `TreatWarningsAsErrors: true`.
- Zero build warnings, zero compiler errors.
- 100% test pass rate across unit and simulation harness suites.

---

### Task 1: Hotkey Gesture Parser & Unit Tests

**Files:**
- Create: `src/RigSwitch.Core/Services/HotkeyGestureParser.cs`
- Test: `tests/RigSwitch.Tests.Unit/HotkeyGestureParserTests.cs`

**Description:**
Implement `HotkeyGestureParser` with:
- Canonical string formatting from modifiers + key.
- Win32 validity checking (must have at least one modifier + valid non-modifier key).
- Key normalizing (mapping SystemKey/F-keys/Numpad/letters).

- [x] **Step 1: Write unit tests for HotkeyGestureParser**
- [x] **Step 2: Implement HotkeyGestureParser.cs**
- [x] **Step 3: Run unit tests to confirm 100% pass**

---

### Task 2: HotkeyRecorderControl WPF Control

**Files:**
- Create: `src/RigSwitch.App/Controls/HotkeyRecorderControl.xaml`
- Create: `src/RigSwitch.App/Controls/HotkeyRecorderControl.xaml.cs`
- Test: `tests/RigSwitch.Tests.Unit/HotkeyRecorderControlTests.cs`

**Description:**
Create the interactive WPF control with `Hotkey` two-way dependency property, recording visual state, `PreviewKeyDown` handler capturing modifiers + key, Escape to cancel, and clear button.

- [x] **Step 1: Implement HotkeyRecorderControl.xaml and .xaml.cs**
- [x] **Step 2: Write unit tests for control logic**
- [x] **Step 3: Verify build with 0 warnings**

---

### Task 3: Settings UI Integration & Hotkey Tab Overhaul

**Files:**
- Modify: `src/RigSwitch.App/Views/MainSettingsWindow.xaml`
- Modify: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs`

**Description:**
Replace all raw hotkey textboxes in `MainSettingsWindow.xaml` (Toggle hotkey, Desk hotkey, Sim Rig hotkey, and preset direct hotkeys) with `HotkeyRecorderControl`.

- [x] **Step 1: Update MainSettingsWindow.xaml with HotkeyRecorderControl**
- [x] **Step 2: Verify binding, tab order, and styling**
- [x] **Step 3: Verify file lengths < 500 lines and run full test suite**
