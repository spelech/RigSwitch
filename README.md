# 🚀 RigSwitch

[![Build](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/stevenpelech/RigSwitch/actions)
[![Tests](https://img.shields.io/badge/tests-97%2F97%20passing-brightgreen.svg)](https://github.com/stevenpelech/RigSwitch/actions)
[![.NET](https://img.shields.io/badge/.NET-10.0--windows-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D6.svg)](https://www.microsoft.com/windows)
[![Toolbelt](https://img.shields.io/badge/standard-AgenticEngineeringToolbelt-6f42c1.svg)](docs/superpowers/specs/2026-09-19-rigswitch-design.md)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**RigSwitch** is an ultra-fast, native Windows system tray control plane and workstation orchestrator. It switches instantly between a **Desk Setup** (MSI MPG341CX OLED display + Pebble V3 USB audio) and a **Sim Rig Setup** (Asus VG34VQL3A ultrawide display + Asus monitor line-out audio) via global hotkeys or the system tray.

Unlike basic primary display toggles, RigSwitch disables the inactive display at the Win32 CCD driver level, preventing unwanted secondary screen clutter, phantom mouse cursors, and games launching off-screen.

---

## ⚡ Key Features

* 🖥️ **Instant Display Topology Switching**: Leverages low-level Win32 Connecting and Configuring Displays (CCD) APIs (`SetDisplayConfig`) to enable the target screen and disable the inactive screen atomically.
* 🛡️ **Fail-Safe Safety Gates**: Verifies that the target display is physically connected and recognized by the GPU before altering topology, eliminating black-screen lockout risk.
* 🔊 **Multi-Tier Audio Fallback**: Routes Windows default playback endpoints via CoreAudio COM (`IPolicyConfig`). Automatically falls back from Creative Pebble V3 USB to MSI monitor audio when the Pebble speakers are unplugged or set to AUX mode.
* 🧹 **Audio Endpoint Visibility Filter**: Easily hide cluttering virtual endpoints created by SteelSeries Sonar, Oculus VR, or Steam Streaming directly within Windows.
* ⌨️ **Global Simulator Hotkeys**: Low-level Win32 hotkey hooks that function seamlessly even inside exclusive full-screen racing simulators and games.
* 📌 **Native System Tray Integration**: Lightweight WPF desktop app running resident in the taskbar with dynamically rendered GDI icons (workstation monitor for Desk mode, racing steering wheel for Sim Rig mode).
* ⚙️ **Modern Dark-Mode Settings GUI**: Intuitive MVVM interface to customize device nicknames, configure hotkeys, preview hardware IDs, and toggle endpoint visibility.

---

## ⌨️ Default Hotkeys

| Hotkey | Action | Description |
| :--- | :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>S</kbd> | **Toggle Profile** | Flips between Desk Setup and Sim Rig Setup |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>D</kbd> | **Desk Setup** | Activates MSI OLED display & routes audio to Pebble V3 (or MSI fallback) |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>R</kbd> | **Sim Rig Setup** | Activates Asus Ultrawide display & routes audio to Asus monitor output |

*Hotkeys can be customized at any time via the Settings window.*

---

## 🎛️ Hardware Profile Mapping

| Profile | Active Display | Disabled Display | Primary Audio | Fallback Audio |
| :--- | :--- | :--- | :--- | :--- |
| **Desk Setup** | `MONITOR\MSI4DD0`<br/>*(MSI MPG341CX OLED)* | `MONITOR\AUS3438`<br/>*(Asus VG34VQL3A)* | `Speakers (Pebble V3)` | `MPG341CX OLED (NVIDIA Audio)` |
| **Sim Rig Setup** | `MONITOR\AUS3438`<br/>*(Asus VG34VQL3A)* | `MONITOR\MSI4DD0`<br/>*(MSI MPG341CX OLED)* | `VG34VQL3A (NVIDIA Audio)`<br/>*(Pass-through to Pebble V2)* | *None* |

---

## 🛠️ Building & Running Locally

### Prerequisites
* Windows 10/11 x64 (Version 1903 or higher)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Version `10.0.100` or higher)

### Build Solution
```powershell
# Clone the repository
git clone https://github.com/stevenpelech/RigSwitch.git
cd RigSwitch

# Build the solution in Release configuration (0 warnings, 0 errors)
dotnet build RigSwitch.slnx -c Release
```

### Run All Tests
```powershell
# Execute all unit tests and simulation harness stress tests
dotnet test RigSwitch.slnx -c Release
```

### Run the Application
```powershell
# Launch RigSwitch in the system tray
dotnet run --project src/RigSwitch.App -c Release
```

---

## 📐 Architecture & Standards

RigSwitch strictly adheres to Steven T. Pelech's **`AgenticEngineeringToolbelt`**:

* **Anti-Bloat Discipline**: Strictly zero `*Manager`, `*Helper`, or `*Util` catch-alls.
* **Strict File Limit**: All source and test files remain strictly `< 500` lines of code.
* **Atomic Persistence**: Settings persistence uses temporary file swap semantics to guarantee zero configuration corruption.
* **Controls-Grade Observability**: Comprehensive simulation test suite with `DiagnosticRingBuffer` and 6-part agent feedback envelopes for automated post-mortem root-cause analysis.

For complete architectural details, topology diagrams, and sequence flows, refer to [ARCHITECTURE.md](ARCHITECTURE.md).

---

## 📜 License

This project is licensed under the [MIT License](LICENSE).
