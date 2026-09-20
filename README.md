# 🚀 RigSwitch

[![Build](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/spelech/RigSwitch/actions)
[![Tests](https://img.shields.io/badge/tests-104%2F104%20passing-brightgreen.svg)](https://github.com/spelech/RigSwitch/actions)
[![.NET](https://img.shields.io/badge/.NET-10.0--windows-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D6.svg)](https://www.microsoft.com/windows)
[![Toolbelt](https://img.shields.io/badge/standard-AgenticEngineeringToolbelt-6f42c1.svg)](docs/superpowers/specs/2026-09-19-rigswitch-design.md)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**RigSwitch** is an ultra-fast, native Windows system tray control plane and workstation orchestrator. It switches instantly between a **Desk Setup** (primary workstation display + desk audio) and a **Sim Rig Setup** (racing simulator / secondary display + rig audio) via global hotkeys or the system tray.

Unlike basic primary display toggles, RigSwitch disables the inactive display at the Win32 CCD driver level, preventing unwanted secondary screen clutter, phantom mouse cursors, and games launching off-screen.

---

## ⚡ Key Features

* 🖥️ **Instant Display Topology Switching**: Leverages low-level Win32 Connecting and Configuring Displays (CCD) APIs (`SetDisplayConfig`) to enable the target screen and disable the inactive screen atomically.
* 🛡️ **Fail-Safe Safety Gates**: Verifies that the target display is physically connected and recognized by the GPU before altering topology, eliminating black-screen lockout risk.
* 🔊 **Multi-Tier Audio Fallback**: Routes Windows default playback endpoints via CoreAudio COM (`IPolicyConfig`). Automatically falls back to a secondary playback device if primary desk speakers are unplugged or set to AUX mode.
* 🧹 **Audio Endpoint Visibility Filter**: Easily hide cluttering virtual endpoints created by third-party audio drivers, VR headsets, or streaming devices directly within Windows.
* ⌨️ **Global Simulator Hotkeys**: Low-level Win32 hotkey hooks that function seamlessly even inside exclusive full-screen racing simulators and games.
* 📌 **Native System Tray Integration**: Lightweight WPF desktop app running resident in the taskbar with dynamically rendered GDI icons (workstation monitor for Desk mode, racing steering wheel for Sim Rig mode).
* ⚙️ **Modern Dark-Mode Settings GUI**: Intuitive MVVM interface with automatic hardware detection dropdowns to easily select your displays and audio devices, customize nicknames, configure hotkeys, and toggle endpoint visibility.

---

## ⌨️ Default Hotkeys

| Hotkey | Action | Description |
| :--- | :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>S</kbd> | **Toggle Profile** | Flips between Desk Setup and Sim Rig Setup |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>D</kbd> | **Desk Setup** | Activates Desk display & routes audio to primary desk endpoint (with fallback) |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>R</kbd> | **Sim Rig Setup** | Activates Sim Rig display & routes audio to rig playback endpoint |

*Hotkeys can be customized at any time via the Settings window.*

---

## 🎛️ Hardware Profile Configuration

Profiles are configured per PC directly from the Settings GUI. Detected monitors and audio devices appear in dropdown menus:

| Profile | Active Display | Inactive Display | Primary Audio | Fallback Audio |
| :--- | :--- | :--- | :--- | :--- |
| **Desk Setup** | Workstation Monitor | Simulator Display | Primary Desk Speakers / DAC | Secondary Audio / Monitor Line-Out |
| **Sim Rig Setup** | Simulator / Ultrawide Display | Workstation Monitor | Sim Rig Audio / DAC | *(Optional)* |

---

## 🛠️ Building & Running Locally

### Prerequisites
* Windows 10/11 x64 (Version 1903 or higher)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Version `10.0.100` or higher)

### Build Solution
```powershell
# Clone the repository
git clone https://github.com/spelech/RigSwitch.git
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

## 🔀 Branching Strategy (Git Flow)

RigSwitch follows the **Git Flow** release model:

* **`main`**: Holds the latest stable, production-ready releases.
* **`develop`**: The primary integration branch where new features and enhancements land.
* **`feat/<name>`**: Feature branches branched from `develop` and merged back into `develop` via Pull Request.
* **`release/v<version>`**: Release preparation branches branched from `develop`. When ready, merged into `main` (with a version tag `vX.Y.Z`) and merged back into `develop`.
* **`hotfix/<name>`**: Urgent fixes branched directly from `main` and merged into both `main` and `develop`.

Pushing any `v*` tag to `main` automatically triggers the GitHub Actions release workflow to compile the installer and publish the GitHub Release.

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
