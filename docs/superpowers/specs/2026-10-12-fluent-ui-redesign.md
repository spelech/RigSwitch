# Design Spec: Windows 11 Fluent UI Modernization (OLED & High Contrast)

**Date:** 2026-10-12  
**Status:** In Review / Design  
**Author:** Pair programming with User  
**Target:** `src/RigSwitch.App/Views/MainSettingsWindow.xaml` and supporting styles  

---

## 1. Objectives & Goals

Transform RigSwitch's settings interface from a boxy, dense tab layout into a modern, native-feeling Windows 11 Settings experience while preserving its dark, high-contrast OLED aesthetic (`#000000` base, `#00C853` Desk Emerald, `#FF5500` Sim Rig Orange).

### Key Desired Outcomes:
1. **Left Navigation Sidebar:** Replace the horizontal tab headers with a vertical navigation rail mirroring Windows 11 Settings.
2. **Elevated Setting Cards:** Group related settings into rounded, elevated dark cards (`CornerRadius="8"`, `#08080E` background, `#20202E` border) with clear title + descriptive subtitle hierarchy.
3. **Fluent Controls:** Replace dated square checkboxes with modern Windows 11 pill toggle switches (`FluentToggleSwitch`), smooth rounded ComboBoxes, and refined action buttons.
4. **Preserve All Functionality:** Seamlessly maintain all existing bindings, commands, view models, multi-monitor topology cards, audio device tables, hotkey recorders, and lifecycle hooks.
5. **No Regressions:** Maintain 100% automated test pass rate across `RigSwitch.Tests.Unit`.

---

## 2. Information Architecture & Navigation

The window layout will transition from a 3-row `TabControl` to a 2-column sidebar navigation structure:

```
+---------------------------------------------------------------------------------------+
|  ⚡ RIGSWITCH       [ DESK ACTIVE ]                      [🖥️ Desk Setup]  [🏎️ Sim Rig]  |
+---------------------+-----------------------------------------------------------------+
|                     |  Section Title (e.g. Workstation Presets)                       |
|  [🖥️ Presets]       |  Subtitle description...                                        |
|  [📺 Displays]      |                                                                 |
|  [🔊 Audio & DACs]  |  +-----------------------------------------------------------+  |
|  [🏷️ Nicknames]     |  | Setting Card (e.g. Primary Audio Endpoint)               |  |
|  [⌨️ Hotkeys]       |  | Default audio device when chat is disabled                |  |
|                     |  |                                       [ Pebble V3 ▾ ]     |  |
|                     |  +-----------------------------------------------------------+  |
|                     |  | Setting Card (e.g. Enforce Primary Speakers)              |  |
|  -----------------  |  | Re-assert speakers if background app hijacks audio        |  |
|  [Watchdog Status]  |  |                                             [ (●) ON ]    |  |
|  ● Enforcing Spkrs  |  +-----------------------------------------------------------+  |
+---------------------+-----------------------------------------------------------------+
|  Ready. All endpoints synchronized.                                [💾 Save Settings] |
+---------------------------------------------------------------------------------------+
```

### Navigation Categories:
1. **`🖥️ Presets` (Workstation Presets)**
   - Sub-selector pills for Desk vs Sim Rig presets.
   - Target Display & HDR mode dropdowns.
   - Primary Audio, Secondary Audio (Chat / Sonar), and Fallback Audio endpoints.
   - Volume behaviors, sliders, and mute checkboxes.
   - Primary, Secondary, and Fallback Microphones.
   - Direct Preset Hotkey recorder.
   - Lifecycle application hooks (Launch & Kill on switch).
2. **`📺 Displays` (Multi-Monitor Topology)**
   - Top description explaining multi-monitor assignment to Desk or Sim Rig piles.
   - "🔍 Identify Monitors" button.
   - Interactive monitor numbered tiles with Desk / Rig radio toggles.
3. **`🔊 Audio & DACs` (Device Visibility & Blacklist Lockout)**
   - Telemetry status card showing active audio endpoints.
   - Audio endpoint table with:
     - Endpoints marked as **`[Excluded / Bass Shaker DAC]`** with toggle switches.
     - Device friendly name & hardware ID.
     - Custom nickname input.
     - Show/Hide in menus toggle.
   - "Disable Non-Preset Audio Devices" master toggle.
4. **`🏷️ Device Nicknames`**
   - Clean card-based mapping for custom display and audio endpoint friendly names.
5. **`⌨️ Hotkeys & System`**
   - Hotkey recorders: Switch to Desk, Switch to Sim Rig, Toggle Audio Output (`Ctrl+Alt+A`).
   - Active Watchdog audio enforcement toggle (`EnforcePrimaryAudio`).
   - App preferences: Start minimized to tray, show toast notifications, check for updates on startup.
   - Software updates card with "Check for Updates" button.

---

## 3. UI Component Specifications & Styles

### 3.1 Color & Surface Hierarchy (Eye-Comfort Palette C: Warm Amber & Sage)
- **Window & Viewport Background:** `#1C1D1F` (Muted Warm Charcoal — completely eliminates pitch-black contrast glare).
- **Sidebar Background:** `#141517` (Deep Soft Slate).
- **Header & Footer Background:** `#141517` with subtle `rgba(255,255,255,0.06)` border (`#2A2B2F`).
- **Elevated Setting Card Background:** `#26272B` with soft `rgba(255,255,255,0.08)` border.
- **Card Hover / Active Fill:** `#2E3036`.
- **Text Primary:** `#E2E4E8` (Warm Off-White / Ivory — prevents harsh high-luminance retinal strain).
- **Text Secondary / Muted:** `#9CA3AF` (Comfortable Medium Slate, 13.5px).
- **Desk Profile Accent:** `#10B981` (Soft Sage / Emerald Green).
- **Sim Rig Profile Accent:** `#F59E0B` (Warm Honey Amber Gold — replaces harsh neon orange).

### 3.2 Typography & Anti-Aliasing (Softened Bahnschrift DIN)
- **Font Family:** `FontFamily="Bahnschrift, Segoe UI Variable Display, Segoe UI, sans-serif"`.
- **Casing:** Mixed natural Title Case (e.g. `Primary Audio Endpoint`, `Workstation Presets`) rather than aggressive all-caps, delivering relaxed and comfortable readability.
- **Anti-Aliasing Directives on Window Root:**
  - `TextOptions.TextFormattingMode="Display"` (pixel-snapped glyph alignment).
  - `TextOptions.TextRenderingMode="ClearType"` (rich subpixel rendering).
  - `RenderOptions.ClearTypeHint="Enabled"` (maintains ClearType across layered controls).
  - `UseLayoutRounding="True"` and `SnapsToDevicePixels="True"` (crisp 1px card borders).
- **Enlarged Font Sizing & Weight Baseline:**
  - **Window Title / Header:** `FontSize="20"`, `FontWeight="SemiBold"`, `#E2E4E8`
  - **Section Headers:** `FontSize="20"`, `FontWeight="SemiBold"`
  - **Navigation Sidebar Items:** `FontSize="15"`, `FontWeight="SemiBold"`, 44px row touch/click target
  - **Setting Card Titles:** `FontSize="15.5"`, `FontWeight="SemiBold"`, `#E2E4E8`
  - **Subtitles & Descriptions:** `FontSize="13.5"`, `FontWeight="Normal"`, `Foreground="#9CA3AF"`
  - **Form Controls (ComboBox, TextBox, Buttons):** `FontSize="14"`, `FontWeight="SemiBold"`, `Padding="10,7"`
  - **Status & Badges:** `FontSize="13"`, `FontWeight="Bold"`, pill style

### 3.3 Controls & Interactive Elements
- **Pill Toggle Switch (`FluentToggleSwitch`):**
  - Dimensions: 48px wide, 26px high, `CornerRadius="13"`.
  - Off state: Dark fill `#313338`, 1px border `rgba(255,255,255,0.12)`, white circle thumb (20px) left-aligned.
  - On state: Warm Amber Gold fill `#F59E0B` (when Sim Rig active) or Soft Sage `#10B981` (when Desk active), dark circle thumb right-aligned.
- **Rounded Cards (`FluentSettingCard`):**
  - `CornerRadius="10"`, `BorderThickness="1"`, `Padding="16,20"`.
  - Two-column grid: left side has Title (`FontWeight="SemiBold"`, 15.5px, `#E2E4E8`) + Subtitle (`Foreground="#9CA3AF"`, 13.5px), right side hosts the input control.
- **Modern Sidebar Nav Items:**
  - `Height="44"`, `CornerRadius="8"`, `Padding="14,10"`.
  - Left vertical indicator: 3.5px wide pill bar visible when item is selected.
  - Subtle hover highlight `#222327`.

---

## 4. Verification & Testing Strategy
1. **Automated Unit Tests:** All 372 existing tests in `RigSwitch.Tests.Unit` must pass without modifications to core business logic.
2. **Typecheck & Linter:** Run `npm run lint` and `npx tsc --noEmit`.
3. **XAML Compilation:** `dotnet build src/RigSwitch.App/RigSwitch.App.csproj` with zero errors and zero warnings.
4. **Visual Inspection:** Verify clean rendering, no text clipping, responsive scaling on window resize (`MinWidth="700"`, `MinHeight="540"`).
