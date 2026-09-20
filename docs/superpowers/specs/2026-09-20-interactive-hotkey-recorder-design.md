# Interactive Hotkey Recorder Specification

> **Target Standard**: Steven T. Pelech's `AgenticEngineeringToolbelt`  
> **Solution**: `RigSwitch.slnx` (`net10.0-windows`, C# 13, WPF)  
> **Milestone**: Feature 2 of Multi-Feature Roadmap  
> **Branch**: `feat/hotkey-recorder`

---

## 1. Overview & Objectives

Replace manual hotkey string typing in RigSwitch with an interactive, gaming-grade **Hotkey Recorder**:
1. **Interactive Detection**: Users click "Record" (or focus the hotkey box), press their desired 2- or 3-key combination (e.g., `Ctrl+Alt+S`, `Ctrl+Shift+R`, `Alt+F1`), and RigSwitch automatically detects and serializes the combination.
2. **Win32 Compatibility Validation**: Ensures the combination contains at least one modifier (`Ctrl`, `Alt`, `Shift`, `Win`) plus a valid primary key, compatible with the Win32 `RegisterHotKey` engine.
3. **User Feedback & Control**:
   - `Escape` cancels recording without altering the previous hotkey.
   - `Backspace` or a clear button ("✕") unbinds / clears the hotkey.
   - Visual state indicates active recording mode.
4. **Integration Across Settings**:
   - Global Toggle Hotkey
   - Direct Desk Hotkey
   - Direct Sim Rig Hotkey
   - Direct Preset Hotkeys for all 6 presets

---

## 2. Architecture & Components (`RigSwitch.App`)

### 2.1 `HotkeyStringFormatter` Utility / Domain Helper
A dedicated helper in `RigSwitch.Core.Services` or `RigSwitch.App.Services`:
- Parses raw WPF `KeyEventArgs` (`Key`, `Keyboard.Modifiers`, `SystemKey`) into canonical string format (`Ctrl+Alt+S`).
- Maps WPF `Key` values to Win32 Virtual Key codes (`VK_*`).
- Prevents invalid lone modifiers (e.g. just pressing `Ctrl` without a primary key).

### 2.2 `HotkeyRecorderControl` (WPF UserControl)
A modern WPF control located in `src/RigSwitch.App/Controls/HotkeyRecorderControl.xaml` / `.xaml.cs`:
- Dependency properties:
  - `Hotkey` (string, `BindsTwoWayByDefault`): The bound hotkey string.
  - `IsRecording` (bool, ReadOnly): State flag for styling.
  - `Watermark` (string): Text shown when no hotkey is assigned ("None" / "Click to Record").
- Interaction Flow:
  - Clicking the control activates recording mode (`IsRecording = true`).
  - While recording, control intercepts `PreviewKeyDown`:
    - If `Key == Key.Escape`: exits recording mode without modifying `Hotkey`.
    - If `Key == Key.Back` or `Delete`: sets `Hotkey = string.Empty` and exits recording mode.
    - If non-modifier key is pressed with at least one modifier: formats canonical hotkey string, sets `Hotkey = formatted`, and exits recording mode.
  - Clear button ("✕") immediately clears the hotkey.

---

## 3. Global Hotkey Registration & Conflict Detection

When saving settings:
- `WindowsGlobalHotkeyService` checks if the recorded hotkey can be registered.
- If Windows returns false (hotkey in use by another application like GeForce Experience or Discord), `MainSettingsViewModel` displays an inline warning banner: `"Warning: Hotkey '{hotkey}' is already in use by Windows or another application."`.

---

## 4. Verification & Testing

- **Unit Tests (`RigSwitch.Tests.Unit`)**:
  - Hotkey string formatting tests (various modifier + key combinations, numpad, function keys).
  - Validation of Win32 registerable combinations.
- **Controls & MVVM Tests**:
  - Testing recording activation, cancellation on Escape, clearing on Delete, and successful binding.
- **Quality Checks**:
  - Strictly < 500 lines per file.
  - Zero compiler warnings or errors.
