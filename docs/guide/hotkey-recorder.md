# Interactive Hotkey Recorder

RigSwitch includes a specialized WPF **`HotkeyRecorderControl`** and gesture parser that captures multi-modifier key combinations directly without manual text typing.

---

## How It Works

Instead of forcing users to guess valid string syntax like `"Ctrl+Alt+S"` or `"Control, Alt, S"`, the interactive hotkey recorder listens to low-level keyboard input in real time.

### Recording a Shortcut:
1. Navigate to the **Global Hotkeys** tab in the Settings window.
2. Click the hotkey field (it turns bright emerald with the prompt *"Press keys..."*).
3. Press any key combination containing at least one modifier key:
   * **Supported Modifiers**: <kbd>Ctrl</kbd>, <kbd>Alt</kbd>, <kbd>Shift</kbd>, <kbd>Win</kbd>
   * **Supported Keys**: Letters (`A`–`Z`), Numbers (`0`–`9`), Function keys (`F1`–`F24`), Navigation keys (`Home`, `End`, `PageUp`, `PageDown`), Numpad keys, and punctuation.
4. The control formats the shortcut into canonical representation automatically (e.g. `Ctrl+Alt+S`).

### Canceling or Clearing:
* **Cancel Recording**: Press <kbd>Esc</kbd> at any time to exit recording mode without modifying your existing shortcut.
* **Clear Shortcut**: Press <kbd>Backspace</kbd>, <kbd>Delete</kbd>, or click the **✕** button on the right side of the control.

---

## Win32 Low-Level Registration

When a shortcut is saved:
1. The `HotkeyGestureParser` extracts the virtual key code and modifier bitmasks (`MOD_CONTROL`, `MOD_ALT`, `MOD_SHIFT`, `MOD_WIN`).
2. The `WindowsGlobalHotkeyService` calls `RegisterHotKey` against a hidden window procedure.
3. Windows intercepts `WM_HOTKEY` system-wide and routes it directly to RigSwitch without blocking foreground game input.
