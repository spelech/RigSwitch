# Display Topology Switching

RigSwitch controls your monitors through low-level Windows **Connecting and Configuring Displays (CCD)** Win32 driver APIs rather than generic display emulation.

---

## Why Win32 CCD?

Standard multi-monitor tools often rely on virtual display drivers or Windows shell shortcuts (<kbd>Win</kbd> + <kbd>P</kbd>). These approaches suffer from severe drawbacks:
* Windows continues treating disabled screens as connected video sinks, allowing windows and mouse pointers to vanish off-screen.
* DirectX and Vulkan games often query the GPU adapter and launch on the inactive monitor.
* Virtual display drivers introduce display latency and require test-signing or kernel-mode driver certificates.

RigSwitch invokes `QueryDisplayConfig` and `SetDisplayConfig` directly:
1. Enumerates all active and inactive path descriptors on the GPU adapter.
2. Identifies target and inactive monitors using their hardware EDID, monitor name, or Windows display path.
3. Sets `DISPLAYCONFIG_PATH_ACTIVE` on the target display while explicitly stripping the flag from the inactive screen.
4. Commits the changes atomically using `SDC_APPLY | SDC_SAVE_TO_DATABASE | SDC_ALLOW_CHANGES`.

---

## Fail-Safe Safety Gates

Changing display topology carries an inherent risk of a **black-screen lockout** if the target monitor is turned off, unplugged, or entering a deep power-saving sleep state.

RigSwitch eliminates this risk using a multi-step **Display Reachability Gate**:

```
[Profile Switch Requested]
            │
            ▼
[QueryDisplayConfig (QDC_ALL_PATHS)]
            │
            ├─► Is Target Monitor physically reachable?
            │         │
            │         ├─► NO  ──► [ABORT SWITCH & ALERT USER]
            │         │           Current display remains 100% active.
            │         ▼
            │        YES
            ▼
[Apply SDC_APPLY Atomically]
```

If the target display cannot be reached:
* The current active display is **never disabled**.
* The switch halts immediately.
* A Windows balloon notification alerts you: *"Target display [Name] is disconnected or powered off."*
