# Design Spec: RigSwitch CLI & IPC Bridge

**Date:** 2026-10-12  
**Status:** In Design / Draft  
**Target:** `src/RigSwitch.App`, `src/RigSwitch.Core`, `packaging`  

---

## 1. Objectives & Goals

Provide a unified, first-class Command Line Interface (CLI) and Inter-Process Communication (IPC) bridge for RigSwitch that allows users, automation scripts (PowerShell, Stream Deck, AutoHotkey), and AI agents to query status, switch profiles, toggle audio outputs, manage blacklists, run diagnostics, and capture live UI screenshots directly from the terminal.

### Key Desired Outcomes:
1. **Native CLI Interface:** Type `rigswitch status`, `rigswitch switch rig`, `rigswitch audio toggle`, etc., from any console.
2. **Zero-Overhead IPC:** Fast (<15ms) communication with the running background tray application via Windows Named Pipes (`\\.\pipe\RigSwitch-Ipc`).
3. **Headless Fallback:** If RigSwitch is not running in the tray, non-GUI CLI commands (status, switch, audio) automatically execute headlessly against `RigSwitch.Core` services.
4. **Live UI Screenshot Tool:** `rigswitch screenshot --view <view_name> --out <path>` commands the running GUI process to render any settings tab to a PNG file for instant visual verification.
5. **Machine & Human Readable:** All query commands support both formatted human text and `--json` structured output for AI agents and scripts.

---

## 2. Command Architecture & Syntax

```text
rigswitch [command] [subcommand/target] [options]

Commands:
  status                     Display active workstation profile, audio output mode, and watchdog health
  switch <desk|rig>          Switch to Desk or Sim Rig workstation preset
  audio <toggle|speakers|chat>
                             Toggle or explicitly set audio output between Primary Speakers and Sonar Chat
  blacklist <list|add|remove>
                             Manage excluded haptic transducer DACs (SimHub ShakeIt)
  screenshot                 Render live settings UI to an image file
                             Options:
                               --view <presets|displays|audio|nicknames|hotkeys>
                               --out <filepath> (default: %TEMP%\rigswitch-view.png)
  doctor                     Perform comprehensive hardware diagnostic & watchdog health audit

Global Options:
  --json                     Output structured JSON instead of human-readable text
  --help, -h                 Show command usage and exit
```

---

## 3. IPC Architecture: Windows Named Pipe

### 3.1 Transport & Channel
- **Pipe Name:** `\\.\pipe\RigSwitch-Ipc`
- **Security:** Standard current-user ACL (`PipeSecurity`).
- **Framing:** Single newline-delimited UTF-8 JSON messages.

### 3.2 Protocol Schema
**Request:**
```json
{
  "id": "req-1234",
  "command": "switch",
  "args": {
    "target": "rig"
  }
}
```

**Response:**
```json
{
  "id": "req-1234",
  "success": true,
  "data": {
    "profile": "SimRig",
    "display": "VG34VQL3A",
    "audio": "VG34VQL3A (Sim Rig)",
    "audioMode": "Primary"
  },
  "error": null
}
```

---

## 4. Console Attachment in WPF
WPF executables are compiled with `<OutputType>WinExe</OutputType>`. When launched from PowerShell or CMD, a `WinExe` does not automatically attach to the caller's console.
- We implement Win32 `AttachConsole(ATTACH_PARENT_PROCESS)` at startup in `App.xaml.cs`.
- If command-line arguments are present, the app attaches to the caller console, writes output to standard stdout/stderr, and exits with code 0 or 1.
- If no command-line arguments are present, `AttachConsole` is skipped and normal WPF GUI / System Tray initialization proceeds.

---

## 5. UI Screenshot Capture via IPC
When the CLI receives `rigswitch screenshot --view <name> --out <path>`:
1. IPC request is sent to `\\.\pipe\RigSwitch-Ipc`.
2. The running tray app dispatches to the WPF UI thread.
3. The UI thread switches `ContentTabControl.SelectedIndex` to the requested view index (0=Presets, 1=Displays, 2=Audio, 3=Nicknames, 4=Hotkeys).
4. `RenderTargetBitmap` renders the window or view to PNG and writes it to the output file.
5. IPC response returns the absolute file path.
6. The CLI prints the output path.

---

## 6. Verification & Testing Strategy
1. **Unit Tests:** Mock IPC client and server to verify request/response protocol, command routing, and argument parsing.
2. **CLI E2E Tests:** Execute commands in test environment (`status`, `switch`, `audio toggle`) with both formatted and `--json` outputs.
3. **All Existing Tests:** Confirm all 372 existing unit and simulation tests continue to pass without regression.
4. **Linter & Typechecking:** Run `npm run lint` and `npx tsc --noEmit`.
