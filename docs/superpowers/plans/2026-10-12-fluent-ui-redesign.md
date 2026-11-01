# Windows 11 Fluent UI Redesign (Warm Charcoal, Amber & Sage) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modernize the RigSwitch settings interface into a clean, native-feeling Windows 11 Settings experience with a left navigation sidebar, elevated setting cards, pill toggle switches, and an eye-friendly warm charcoal palette with softened Bahnschrift typography.

**Architecture:** 
1. Extract modular styling into `src/RigSwitch.App/Styles/FluentTheme.xaml` defining the warm charcoal & amber/sage color hierarchy, modern pill toggle switches (`FluentToggleSwitch`), rounded cards, and button styles.
2. Refactor `src/RigSwitch.App/Views/MainSettingsWindow.xaml` from a dense horizontal `TabControl` into a 2-column sidebar navigation rail + elevated card content views.
3. Apply window-level ClearType anti-aliasing directives and enlarged softened typography in mixed Title Case.

**Tech Stack:** WPF, XAML, C# .NET 10, Windows 11 Fluent Design principles, ClearType typography.

## Global Constraints

- Never break existing `UserSettings` JSON serialization backward compatibility.
- Ensure all existing ViewModel bindings, commands, converters, and controls continue to function identically.
- Ensure bass shaker DACs remain enabled in Windows while barred from default audio routing.
- Keep watchdog event-driven via `IMMNotificationClient` with zero CPU polling loops.
- All 372 unit tests in `RigSwitch.Tests.Unit` must pass cleanly (`dotnet test`).
- Always run `npm run lint` and `npx tsc --noEmit` after code changes.

---

### Task 1: Fluent Control Styles & Eye-Comfort Palette Dictionary

**Files:**
- Create: `src/RigSwitch.App/Styles/FluentTheme.xaml`
- Modify: `src/RigSwitch.App/App.xaml:1-10`

**Interfaces:**
- Produces: 
  - Brushes: `MutedCharcoalBg` (`#1C1D1F`), `SlateSidebarBg` (`#141517`), `GraphiteCardBg` (`#26272B`), `CardBorderBrush` (`#36383F`), `TextPrimaryBrush` (`#E2E4E8`), `TextMutedBrush` (`#9CA3AF`), `AccentDesk` (`#10B981`), `AccentRig` (`#F59E0B`).
  - Styles: `FluentToggleSwitch` (modern CheckBox pill template with sliding circle thumb), `FluentSettingCard` (Border template with `CornerRadius="10"` and subtle border), `FluentComboBox`, `FluentTextBox`, `FluentNavButton`, `FluentActionButton`.

- [ ] **Step 1: Create `src/RigSwitch.App/Styles/FluentTheme.xaml`**
Define all color brushes, the `FluentToggleSwitch` control template (48x26 pill switch with animated/sliding thumb), `FluentSettingCard`, `FluentNavButton`, and input controls with rounded corners.

- [ ] **Step 2: Merge ResourceDictionary in `src/RigSwitch.App/App.xaml`**
Add `<ResourceDictionary Source="Styles/FluentTheme.xaml" />` to `App.xaml.Resources.MergedDictionaries`.

- [ ] **Step 3: Verify XAML compilation**
Run `dotnet build src/RigSwitch.App/RigSwitch.App.csproj` to confirm the resource dictionary compiles without syntax or schema errors.

- [ ] **Step 4: Commit Task 1**
Commit message: `style(app): add FluentTheme resource dictionary with warm palette and pill toggles`

---

### Task 2: MainSettingsWindow Structure & Sidebar Navigation Rail

**Files:**
- Modify: `src/RigSwitch.App/Views/MainSettingsWindow.xaml`

**Interfaces:**
- Consumes: `FluentTheme.xaml` brushes, navigation styles, and window anti-aliasing directives.
- Produces: 2-column layout with left navigation rail (width 240px) and scrollable right content view.

- [ ] **Step 1: Configure Window-Level ClearType & Anti-Aliasing**
Update `MainSettingsWindow` root attributes:
```xml
Background="{StaticResource MutedCharcoalBg}"
Foreground="{StaticResource TextPrimaryBrush}"
FontFamily="Bahnschrift, Segoe UI Variable Display, Segoe UI, sans-serif"
TextOptions.TextFormattingMode="Display"
TextOptions.TextRenderingMode="ClearType"
RenderOptions.ClearTypeHint="Enabled"
SnapsToDevicePixels="True"
UseLayoutRounding="True"
Width="860" Height="680" MinWidth="780" MinHeight="580"
```

- [ ] **Step 2: Replace TabControl with Left Navigation Rail & Content Host**
Implement the header bar with `⚡ RigSwitch` title, profile badge, and desk/rig switch buttons.
Implement the left sidebar with 5 navigation rail items using styled RadioButtons or TabControl with custom vertical panel template:
1. `🖥️ Presets`
2. `📺 Displays & Topology`
3. `🔊 Audio & DAC Lockout`
4. `🏷️ Device Nicknames`
5. `⌨️ Hotkeys & System`
Include bottom sidebar CoreAudio Watchdog status tile.

- [ ] **Step 3: Verify build**
Run `dotnet build src/RigSwitch.App/RigSwitch.App.csproj`.

- [ ] **Step 4: Commit Task 2**
Commit message: `feat(app): implement Windows 11 left navigation rail and window layout`

---

### Task 3: Modernize Views with Elevated Fluent Setting Cards

**Files:**
- Modify: `src/RigSwitch.App/Views/MainSettingsWindow.xaml`

**Interfaces:**
- Consumes: ViewModel properties (`DeskPresets`, `RigPresets`, `MonitorTiles`, `AudioEndpoints`, `DeviceNicknames`, `EnforcePrimaryAudio`, etc.)
- Produces: Elevated rounded cards (`CornerRadius="10"`, `#26272B` background) for each setting group across all 5 pages.

- [ ] **Step 1: Modernize Workstation Presets Page**
Wrap each preset setting in an elevated `FluentSettingCard`:
- Target Display & Windows HDR mode.
- Primary Audio Endpoint ComboBox.
- Secondary Audio (Headset / Sonar) ComboBox.
- Fallback Audio Endpoint ComboBox.
- Playback Volume Behavior and custom volume slider.
- Primary / Secondary / Fallback Microphones and volume sliders.
- Direct Hotkey recorder.
- Lifecycle application hooks (Processes to launch/terminate).

- [ ] **Step 2: Modernize Displays & Topology Page**
Wrap Multi-Monitor Topology in clean cards with the "Identify Monitors" button and styled monitor number tiles (Desk vs Sim Rig radio buttons).

- [ ] **Step 3: Modernize Audio & DAC Lockout Page**
Implement the Audio Endpoint list with `FluentToggleSwitch` for `[Excluded / Bass Shaker DAC]`, nickname inputs, and "Disable Non-Preset Audio Devices" toggle.

- [ ] **Step 4: Modernize Device Nicknames Page**
Card-based friendly nickname editor for displays and audio endpoints.

- [ ] **Step 5: Modernize Hotkeys & System Preferences Page**
- Desk Hotkey, Sim Rig Hotkey, and Audio Toggle Hotkey recorders.
- `EnforcePrimaryAudio` toggle switch (`FluentToggleSwitch`).
- App startup preferences (Start minimized, Toast notifications, Auto update check) with `FluentToggleSwitch`.
- Software update check card and button.
- Bottom footer status bar with `💾 Save Settings` button in Soft Sage (`#10B981`).

- [ ] **Step 6: Verify build**
Run `dotnet build src/RigSwitch.App/RigSwitch.App.csproj`.

- [ ] **Step 7: Commit Task 3**
Commit message: `feat(app): restyle setting views with elevated Fluent cards and pill toggles`

---

### Task 4: Automated Verification, Testing & PR Update

**Files:**
- Test: All tests in `tests/RigSwitch.Tests.Unit/`

- [ ] **Step 1: Run unit test suite**
Execute `dotnet test tests/RigSwitch.Tests.Unit`.
Confirm all 372 automated tests pass with 0 failures.

- [ ] **Step 2: Run linter and typechecker**
Execute `npm run lint` and `npx tsc --noEmit`. Confirm 0 errors.

- [ ] **Step 3: Build Debug and Release binaries**
Run `dotnet build -c Debug` and `dotnet build -c Release` to ensure both configurations compile cleanly.

- [ ] **Step 4: Commit and Push**
Commit any remaining polish and push branch `feat/shaker-lockout-and-audio-toggle` to GitHub.
