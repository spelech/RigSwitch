# System Architecture & Technical Specifications

RigSwitch is engineered strictly according to Steven T. Pelech's **`AgenticEngineeringToolbelt`** specifications for native Windows desktop software.

---

## High-Level Component Topology

The following diagram illustrates the interaction between the user interface, hardware coordinator, and native Windows subsystems:

```mermaid
graph TD
    UI[WPF UI & System Tray] --> Coord[ProfileSwitchCoordinator]
    Hotkey[WindowsGlobalHotkeyService] --> Coord

    subgraph Core Control Plane
        Coord --> Storage[JsonSettingsStorageService]
        Coord --> HookService[WindowsApplicationLifecycleHookService]
    end

    subgraph Native Windows Subsystems
        Coord --> CCD[WindowsDisplayConfigurationService]
        Coord --> Audio[CoreAudioEndpointDirector]
        CCD --> Win32CCD[Win32 SetDisplayConfig API]
        Audio --> CoreAudio[CoreAudio IPolicyConfig COM]
        HookService --> Win32Proc[Process Management API]
    end
```

---

## Switching Sequence Flow

The atomic switching process follows a rigorous safety-gated execution sequence:

```mermaid
sequenceDiagram
    autonumber
    actor User as User / Hotkey
    participant Coord as ProfileSwitchCoordinator
    participant Gate as Safety Gate Reachability
    participant CCD as WindowsDisplayConfigurationService
    participant Audio as CoreAudioEndpointDirector
    participant Hooks as WindowsAppHookService
    participant Tray as TrayIconService

    User->>Coord: SwitchProfileAsync(SimRig)
    Coord->>Gate: Verify Target Monitor Connected?
    alt Target Disconnected
        Gate-->>Coord: False (Target Unreachable)
        Coord-->>Tray: ShowNotification("Switch Aborted: Target screen unreachable")
    else Target Reachable
        Gate-->>Coord: True
        Coord->>Hooks: CloseHooksForPresetAsync(Outgoing)
        Coord->>CCD: ApplySingleDisplayTopologyAsync(Target, Inactive)
        CCD-->>Coord: Success (Topology Applied)
        Coord->>Audio: SetDefaultPlaybackEndpointAsync(RigAudio)
        Audio-->>Coord: Success (Audio Routed)
        Coord->>Hooks: LaunchHooksForPresetAsync(Incoming)
        Coord->>Tray: UpdateTrayState(SimRig, ActivePreset)
        Coord-->>User: Profile Switched Successfully
    end
```

---

## Architectural Principles & Disciplines

1. **Anti-Bloat Discipline**: Strictly zero `*Manager`, `*Helper`, or `*Util` catch-all classes. Subsystems are bounded to single responsibilities with explicit contracts (`IDisplayConfigurationService`, `IAudioEndpointDirector`, `ISettingsStorageService`).
2. **File Size Strict Limit**: All source files remain strictly `< 500` lines of code.
3. **Atomic Settings Persistence**: Settings are written to a temporary swap file and committed atomically via atomic file replace to prevent file corruption during sudden system shutdowns.
4. **Thread-Safe Coordination**: `ProfileSwitchCoordinator` synchronizes all switching requests through a `SemaphoreSlim(1, 1)` gate to prevent race conditions from rapid hotkey spamming.
