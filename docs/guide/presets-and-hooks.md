# Multi-Preset Workstation Engine & Lifecycle Hooks

RigSwitch supports **6 dedicated workstation presets** (up to 3 for Desk and 3 for Sim Rig), complete with automated process management.

---

## Workstation Presets

Each preset represents an independent environment configuration:

### Default Desk Presets:
1. **Work / Primary**: Standard productivity setup with primary monitor, desktop DAC/speakers, and optional productivity tools.
2. **Media / Casual**: Desk setup routed to secondary audio (soundbar or monitor line-out).
3. **Clean Desk**: Workstation display with background utility hooks minimized or closed.

### Default Sim Rig Presets:
1. **GT3 / Circuit**: Cockpit display, dedicated rig audio, and automated launch of telemetry daemons (e.g. SimHub, Moza Pit House).
2. **Rally / Drift**: Custom display configuration tuned for rally setups.
3. **Flight / Space**: Dedicated flight stick utilities and HOTAS controller software.

Each preset independently stores:
* **Target Display** & **Device Path**
* **Primary Playback Audio Endpoint**
* **Fallback Audio Endpoint**
* **Direct Hotkey** (e.g. <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>1</kbd>)
* **Application Lifecycle Hooks**

---

## Application Lifecycle Hooks

When switching between workstation tasks and racing or flight simulators, you frequently need specific software running (e.g., Fanatec FanaLab, Moza Pit House, SimHub, CrewChief, Discord, OBS).

RigSwitch automates this through **Preset Application Hooks**:

```
[Switching into "GT3 / Circuit"]
  │
  ├──► Closes applications configured on the outgoing preset
  │     (Sends WM_CLOSE with 2.5s graceful timeout, falls back to process-tree kill)
  │
  └──► Launches applications configured on the incoming preset
        (Spawns processes asynchronously and tracks PIDs)
```

### Configuring Hooks:
1. Open **Settings** ➔ **Workstation Profiles**.
2. Select your desired preset card.
3. Under **Applications & Tools**, click **+ Add Application (.exe)**.
4. Browse to your simulator utility or background app.
5. Check **"Close when switching away"** if you want RigSwitch to cleanly terminate the program when returning to the Desk.
