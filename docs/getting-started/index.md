# Overview & Requirements

**RigSwitch** is an ultra-fast, native Windows system tray control plane and hardware orchestrator. It switches instantly between a **Desk Setup** (workstation monitor + desktop speakers) and a **Sim Rig Setup** (racing simulator / cockpit display + rig audio) via global hotkeys or the system tray.

---

## The Problem RigSwitch Solves

In multi-environment desktop configurations (such as an office workstation adjacent to a racing cockpit or flight simulator):
* Leaving both displays active creates phantom mouse cursor traps, window misplacement, and games launching on the wrong screen.
* Basic Windows shortcuts (<kbd>Win</kbd> + <kbd>P</kbd>) toggle multi-monitor topologies blindly without coordinating default audio routing.
* Manually switching monitors, changing default playback devices in Windows Settings, and launching simulator background software (SimHub, Moza Pit House, CrewChief) takes dozens of clicks every single session.

RigSwitch automates the entire transition into an atomic, sub-second hardware switch.

---

## System Requirements

| Component | Minimum Requirement | Recommended |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 x64 (Version 1903+) | Windows 11 x64 (Version 22H2+) |
| **Architecture** | 64-bit (`win-x64`) | 64-bit (`win-x64`) |
| **Runtime** | Self-contained (No .NET installation required) | .NET 10.0 Windows Desktop Runtime |
| **Privileges** | Standard User (No administrator elevation needed) | Standard User |
| **Graphics** | DirectX 11+ WDDM-compliant GPU driver | NVIDIA RTX / AMD Radeon with CCD support |

---

## Next Steps

* Ready to install? Check the [Installation Guide](./installation.md).
* Want to see how to trigger your first profile switch? Jump to the [Quickstart Guide](./quickstart.md).
