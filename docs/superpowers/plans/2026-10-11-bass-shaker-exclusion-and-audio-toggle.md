# Bass Shaker Audio Exclusion, Primary/Secondary Audio Toggle, and CoreAudio Watchdog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide foolproof exclusion of bass shaker DACs from Windows default audio routing, enable dual-tier (Primary Speakers vs Secondary Headset/Sonar) preset audio switching via hotkey, and provide a native CoreAudio watchdog to safeguard default audio.

**Architecture:** Extend core domain models (`UserSettings`, `WorkstationPreset`) with blacklisted device IDs and secondary audio endpoints. Implement audio output toggling and blacklist exclusion in `ProfileSwitchCoordinator`. Hook into native Windows CoreAudio events via `IMMNotificationClient` in `AudioEndpointWatchdogService` with re-entrancy protection. Expose blacklist toggles, secondary endpoints, and hotkey configuration in WPF settings and tray icon.

**Tech Stack:** .NET 8, C# 12, WPF, Windows CoreAudio COM (`IMMDeviceEnumerator`, `IMMNotificationClient`, `IPolicyConfig`), xUnit, NSubstitute.

## Global Constraints

- Never break existing `UserSettings` JSON serialization backward compatibility.
- Ensure bass shaker DACs remain enabled in Windows so SimHub telemetry streams function normally.
- Watchdog must be event-driven via `IMMNotificationClient`, with zero CPU polling loops.
- Re-entrancy guards and debouncing (~250ms) must prevent event feedback loops during deliberate switches.
- Always run `dotnet test` to verify changes, preserving all existing tests.

---

### Task 1: Core Models & Settings Serialization for Blacklist & Audio Toggle

**Files:**
- Modify: `src/RigSwitch.Core/Models/UserSettings.cs`
- Modify: `src/RigSwitch.Core/Models/WorkstationPreset.cs`
- Test: `tests/RigSwitch.Tests.Unit/UserSettingsAudioTests.cs`

**Interfaces:**
- Produces:
  - `AudioOutputMode`: enum (`Primary = 0`, `Secondary = 1`)
  - `UserSettings.BlacklistedAudioEndpointIds`: `List<string>`
  - `UserSettings.AudioToggleHotkey`: `string` (default `"Ctrl+Alt+A"`)
  - `UserSettings.EnforcePrimaryAudio`: `bool` (default `true`)
  - `UserSettings.ActiveAudioOutputMode`: `AudioOutputMode` (default `Primary`)
  - `WorkstationPreset.SecondaryAudioId`: `string`
  - `WorkstationPreset.SecondaryMicrophoneId`: `string`

- [ ] **Step 1: Write the failing tests for UserSettings and WorkstationPreset audio fields**

Create `tests/RigSwitch.Tests.Unit/UserSettingsAudioTests.cs`:
```csharp
namespace RigSwitch.Tests.Unit;

using System.Text.Json;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

public sealed class UserSettingsAudioTests
{
    [Fact]
    public void WorkstationPreset_HasDefaultSecondaryAudioProperties()
    {
        var preset = new WorkstationPreset();
        Assert.Equal(string.Empty, preset.SecondaryAudioId);
        Assert.Equal(string.Empty, preset.SecondaryMicrophoneId);
    }

    [Fact]
    public void UserSettings_HasDefaultAudioExclusionAndToggleSettings()
    {
        var settings = new UserSettings();
        Assert.Empty(settings.BlacklistedAudioEndpointIds);
        Assert.Equal("Ctrl+Alt+A", settings.AudioToggleHotkey);
        Assert.True(settings.EnforcePrimaryAudio);
        Assert.Equal(AudioOutputMode.Primary, settings.ActiveAudioOutputMode);
    }

    [Fact]
    public void UserSettings_JsonSerialization_RoundtripsNewAudioProperties()
    {
        var original = new UserSettings
        {
            AudioToggleHotkey = "Ctrl+Shift+A",
            EnforcePrimaryAudio = false,
            ActiveAudioOutputMode = AudioOutputMode.Secondary,
            BlacklistedAudioEndpointIds = ["{DAC-1}", "{DAC-2}"]
        };
        original.DeskPresets[0].SecondaryAudioId = "{SONAR-HEADSET}";

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<UserSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal("Ctrl+Shift+A", restored.AudioToggleHotkey);
        Assert.False(restored.EnforcePrimaryAudio);
        Assert.Equal(AudioOutputMode.Secondary, restored.ActiveAudioOutputMode);
        Assert.Equal(["{DAC-1}", "{DAC-2}"], restored.BlacklistedAudioEndpointIds);
        Assert.Equal("{SONAR-HEADSET}", restored.DeskPresets[0].SecondaryAudioId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~UserSettingsAudioTests`
Expected: FAIL due to missing `AudioOutputMode`, `SecondaryAudioId`, etc.

- [ ] **Step 3: Implement the new properties in Core models**

In `src/RigSwitch.Core/Models/UserSettings.cs`:
Add `AudioOutputMode` enum and settings properties:
```csharp
public enum AudioOutputMode
{
    Primary = 0,
    Secondary = 1
}
```
Add to `UserSettings`:
```csharp
public List<string> BlacklistedAudioEndpointIds { get; set; } = [];
public string AudioToggleHotkey { get; set; } = "Ctrl+Alt+A";
public bool EnforcePrimaryAudio { get; set; } = true;
public AudioOutputMode ActiveAudioOutputMode { get; set; } = AudioOutputMode.Primary;
```
In `src/RigSwitch.Core/Models/WorkstationPreset.cs`:
```csharp
public string SecondaryAudioId { get; set; } = string.Empty;
public string SecondaryMicrophoneId { get; set; } = string.Empty;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~UserSettingsAudioTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RigSwitch.Core/Models/UserSettings.cs src/RigSwitch.Core/Models/WorkstationPreset.cs tests/RigSwitch.Tests.Unit/UserSettingsAudioTests.cs
git commit -m "feat(core): add blacklisted audio endpoints, secondary audio fields, and audio output mode"
```

---

### Task 2: Core Audio Routing & Blacklist Filtering in Coordinator

**Files:**
- Modify: `src/RigSwitch.Core/Interfaces/IProfileSwitchCoordinator.cs`
- Modify: `src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs`
- Test: `tests/RigSwitch.Tests.Unit/ProfileSwitchCoordinatorAudioToggleTests.cs`

**Interfaces:**
- Consumes: `UserSettings.BlacklistedAudioEndpointIds`, `UserSettings.ActiveAudioOutputMode`, `WorkstationPreset.SecondaryAudioId`
- Produces:
  - `IProfileSwitchCoordinator.ToggleAudioOutputModeAsync(CancellationToken ct = default)`: `Task<AudioOutputMode>`
  - `IProfileSwitchCoordinator.CurrentAudioOutputMode`: `AudioOutputMode`
  - Blacklist filtering inside `ResolveAudioEndpoint` so blacklisted endpoints can never be selected as fallback or default.

- [ ] **Step 1: Write failing tests for blacklist exclusion and toggle behavior**

Create `tests/RigSwitch.Tests.Unit/ProfileSwitchCoordinatorAudioToggleTests.cs`:
```csharp
namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Core.Services;
using Xunit;

public sealed class ProfileSwitchCoordinatorAudioToggleTests
{
    private readonly IDisplayDeviceManager _displayManager = Substitute.For<IDisplayDeviceManager>();
    private readonly IAudioEndpointDirector _audioDirector = Substitute.For<IAudioEndpointDirector>();
    private readonly ISettingsStorageService _storageService = Substitute.For<ISettingsStorageService>();

    [Fact]
    public async Task SwitchProfile_FiltersOutBlacklistedAudioEndpoints()
    {
        var settings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{SHAKER-DAC-1}"],
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{OFFLINE-ID}", FallbackAudioId = string.Empty }]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SHAKER-DAC-1}", "Nobsound USB DAC", "USB", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{SPEAKERS}", "Pebble V3", "Realtek", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback)
        };
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        var coordinator = new ProfileSwitchCoordinator(_displayManager, _audioDirector, _storageService);
        await coordinator.SwitchProfileAsync(ProfileMode.Desk);

        // Crucial: Must select Pebble V3, NEVER the blacklisted shaker DAC!
        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
        await _audioDirector.DidNotReceive().SetDefaultPlaybackEndpointAsync("{SHAKER-DAC-1}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleAudioOutputMode_SwitchesBetweenPrimaryAndSecondary()
    {
        var settings = new UserSettings
        {
            ActiveAudioOutputMode = AudioOutputMode.Primary,
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}", SecondaryAudioId = "{SONAR-HEADSET}" }]
        };
        _storageService.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var endpoints = new List<AudioEndpointInfo>
        {
            new("{SPEAKERS}", "Pebble V3", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback),
            new("{SONAR-HEADSET}", "SteelSeries Sonar Gaming", "SteelSeries", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback)
        };
        _audioDirector.EnumerateAudioEndpointsAsync(Arg.Any<CancellationToken>()).Returns(endpoints);

        var coordinator = new ProfileSwitchCoordinator(_displayManager, _audioDirector, _storageService);
        
        var newMode = await coordinator.ToggleAudioOutputModeAsync();
        Assert.Equal(AudioOutputMode.Secondary, newMode);
        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{SONAR-HEADSET}", Arg.Any<CancellationToken>());

        var toggledBackMode = await coordinator.ToggleAudioOutputModeAsync();
        Assert.Equal(AudioOutputMode.Primary, toggledBackMode);
        await _audioDirector.Received(1).SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~ProfileSwitchCoordinatorAudioToggleTests`
Expected: FAIL due to missing `ToggleAudioOutputModeAsync` on `IProfileSwitchCoordinator`.

- [ ] **Step 3: Implement `ToggleAudioOutputModeAsync` and blacklist filtering in coordinator**

In `src/RigSwitch.Core/Interfaces/IProfileSwitchCoordinator.cs`:
```csharp
AudioOutputMode CurrentAudioOutputMode { get; }
Task<AudioOutputMode> ToggleAudioOutputModeAsync(CancellationToken cancellationToken = default);
```

In `src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs`:
1. Filter `endpointList` in `SwitchProfileAsync` and `ToggleAudioOutputModeAsync` by excluding any ID matching `settings.BlacklistedAudioEndpointIds`:
```csharp
var blacklisted = settings.BlacklistedAudioEndpointIds ?? [];
var allowedEndpoints = endpointList.Where(e => !blacklisted.Any(b => MatchesEndpoint(b, e.Id))).ToList();
```
2. In `SwitchProfileAsync`, pick the target endpoint according to `settings.ActiveAudioOutputMode`:
```csharp
var targetAudioId = settings.ActiveAudioOutputMode == AudioOutputMode.Secondary && !string.IsNullOrWhiteSpace(preset.SecondaryAudioId)
    ? preset.SecondaryAudioId
    : preset.PrimaryAudioId;
var targetMicId = settings.ActiveAudioOutputMode == AudioOutputMode.Secondary && !string.IsNullOrWhiteSpace(preset.SecondaryMicrophoneId)
    ? preset.SecondaryMicrophoneId
    : preset.PrimaryMicrophoneId;
```
3. Implement `ToggleAudioOutputModeAsync`:
Toggles `_settings.ActiveAudioOutputMode`, resolves and switches audio endpoint, saves settings, and returns new mode.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~ProfileSwitchCoordinatorAudioToggleTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RigSwitch.Core/Interfaces/IProfileSwitchCoordinator.cs src/RigSwitch.Core/Services/ProfileSwitchCoordinator.cs tests/RigSwitch.Tests.Unit/ProfileSwitchCoordinatorAudioToggleTests.cs
git commit -m "feat(core): implement blacklist filtering and audio output toggle in ProfileSwitchCoordinator"
```

---

### Task 3: Native CoreAudio `IMMNotificationClient` Support

**Files:**
- Modify: `src/RigSwitch.Infrastructure/Windows/CoreAudio/ComInterfaces.cs`
- Modify: `src/RigSwitch.Infrastructure/Windows/CoreAudio/INativeAudioProvider.cs`
- Modify: `src/RigSwitch.Infrastructure/Windows/CoreAudio/WindowsNativeAudioProvider.cs`
- Test: `tests/RigSwitch.Tests.Unit/CoreAudioNotificationClientTests.cs`

**Interfaces:**
- Produces:
  - `IMMNotificationClient` interface definition in `ComInterfaces.cs`
  - `INativeAudioProvider.DefaultDeviceChanged`: event `Action<EDataFlow, ERole, string>?`
  - `WindowsNativeAudioProvider`: implements `IMMNotificationClient` and raises `DefaultDeviceChanged` when Windows sends notifications.

- [ ] **Step 1: Write failing test for native notification event handling**

Create `tests/RigSwitch.Tests.Unit/CoreAudioNotificationClientTests.cs`:
```csharp
namespace RigSwitch.Tests.Unit;

using RigSwitch.Infrastructure.Windows.CoreAudio;
using Xunit;

public sealed class CoreAudioNotificationClientTests
{
    [Fact]
    public void NativeAudioProvider_ExposesDefaultDeviceChangedEvent()
    {
        var provider = new WindowsNativeAudioProvider();
        bool eventFired = false;
        provider.DefaultDeviceChanged += (flow, role, id) =>
        {
            eventFired = true;
        };

        Assert.False(eventFired);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~CoreAudioNotificationClientTests`
Expected: FAIL due to missing `DefaultDeviceChanged` on `WindowsNativeAudioProvider`.

- [ ] **Step 3: Define `IMMNotificationClient` and hook into `WindowsNativeAudioProvider`**

In `src/RigSwitch.Infrastructure/Windows/CoreAudio/ComInterfaces.cs`:
```csharp
[ComImport]
[Guid("79912872-C81A-4839-B470-B0E73A16A602")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMNotificationClient
{
    void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, DevicePresenceState dwNewState);
    void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
    void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
    void OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);
    void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PROPERTYKEY key);
}
```
Update `IMMDeviceEnumerator`:
```csharp
int RegisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IMMNotificationClient pClient);
int UnregisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IMMNotificationClient pClient);
```
In `src/RigSwitch.Infrastructure/Windows/CoreAudio/INativeAudioProvider.cs`:
```csharp
event Action<EDataFlow, ERole, string>? DefaultDeviceChanged;
```
In `src/RigSwitch.Infrastructure/Windows/CoreAudio/WindowsNativeAudioProvider.cs`:
Implement `IMMNotificationClient` and register with `_deviceEnumerator.RegisterEndpointNotificationCallback(this)` upon initialization, unregistering on `Dispose()`. In `OnDefaultDeviceChanged`, invoke `DefaultDeviceChanged?.Invoke(flow, role, pwstrDefaultDeviceId)`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~CoreAudioNotificationClientTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RigSwitch.Infrastructure/Windows/CoreAudio/ComInterfaces.cs src/RigSwitch.Infrastructure/Windows/CoreAudio/INativeAudioProvider.cs src/RigSwitch.Infrastructure/Windows/CoreAudio/WindowsNativeAudioProvider.cs tests/RigSwitch.Tests.Unit/CoreAudioNotificationClientTests.cs
git commit -m "feat(infrastructure): add IMMNotificationClient and DefaultDeviceChanged event to audio provider"
```

---

### Task 4: Audio Endpoint Watchdog Service

**Files:**
- Create: `src/RigSwitch.Infrastructure/Windows/CoreAudio/IAudioEndpointWatchdogService.cs`
- Create: `src/RigSwitch.Infrastructure/Windows/CoreAudio/AudioEndpointWatchdogService.cs`
- Test: `tests/RigSwitch.Tests.Unit/AudioEndpointWatchdogServiceTests.cs`

**Interfaces:**
- Consumes: `INativeAudioProvider.DefaultDeviceChanged`, `ISettingsStorageService`, `IAudioEndpointDirector`
- Produces:
  - `IAudioEndpointWatchdogService`: `Start()`, `Stop()`, `NotifyIntentionalSwitch(string endpointId)`
  - Prevents switches to blacklisted devices (reverts immediately).
  - Enforces primary speakers against unauthorized changes when `EnforcePrimaryAudio` is true.
  - Allows human switching between valid non-blacklisted devices.

- [ ] **Step 1: Write failing tests for AudioEndpointWatchdogService**

Create `tests/RigSwitch.Tests.Unit/AudioEndpointWatchdogServiceTests.cs`:
```csharp
namespace RigSwitch.Tests.Unit;

using NSubstitute;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;
using RigSwitch.Infrastructure.Windows.CoreAudio;
using Xunit;

public sealed class AudioEndpointWatchdogServiceTests
{
    private readonly INativeAudioProvider _provider = Substitute.For<INativeAudioProvider>();
    private readonly IAudioEndpointDirector _director = Substitute.For<IAudioEndpointDirector>();
    private readonly ISettingsStorageService _storage = Substitute.For<ISettingsStorageService>();

    [Fact]
    public async Task WhenDefaultSwitchesToBlacklistedDevice_WatchdogRevertsImmediately()
    {
        var settings = new UserSettings
        {
            BlacklistedAudioEndpointIds = ["{SHAKER-DAC}"],
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}" }]
        };
        _storage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var watchdog = new AudioEndpointWatchdogService(_provider, _director, _storage);
        watchdog.Start();

        // Simulate Windows setting default device to bass shaker DAC
        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eRender, ERole.eConsole, "{SHAKER-DAC}");

        // Allow debounce task to complete
        await Task.Delay(350);

        await _director.Received(1).SetDefaultPlaybackEndpointAsync("{SPEAKERS}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenIntentionalSwitchNotified_WatchdogDoesNotRevert()
    {
        var settings = new UserSettings
        {
            EnforcePrimaryAudio = true,
            DeskPresets = [new WorkstationPreset { PrimaryAudioId = "{SPEAKERS}", SecondaryAudioId = "{SONAR}" }]
        };
        _storage.LoadSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var watchdog = new AudioEndpointWatchdogService(_provider, _director, _storage);
        watchdog.Start();

        watchdog.NotifyIntentionalSwitch("{SONAR}");

        _provider.DefaultDeviceChanged += Raise.Event<Action<EDataFlow, ERole, string>>(
            EDataFlow.eRender, ERole.eConsole, "{SONAR}");

        await Task.Delay(350);

        await _director.DidNotReceive().SetDefaultPlaybackEndpointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~AudioEndpointWatchdogServiceTests`
Expected: FAIL due to missing `AudioEndpointWatchdogService`.

- [ ] **Step 3: Implement `AudioEndpointWatchdogService`**

Create `src/RigSwitch.Infrastructure/Windows/CoreAudio/IAudioEndpointWatchdogService.cs` and `src/RigSwitch.Infrastructure/Windows/CoreAudio/AudioEndpointWatchdogService.cs`:
- Maintain `_activeTargetEndpointId` and debounce timer (250ms).
- When `DefaultDeviceChanged` fires for `eRender` and `eConsole`:
  - If `newId` matches `BlacklistedAudioEndpointIds`: immediately revert to expected target endpoint.
  - If `EnforcePrimaryAudio` is enabled, user mode is `Primary`, and `newId` matches known virtual hijacking endpoints (e.g. SteelSeries Sonar): revert to primary speaker endpoint.
  - If user intentionally switched via `NotifyIntentionalSwitch(id)`, update expected target and do not revert.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~AudioEndpointWatchdogServiceTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RigSwitch.Infrastructure/Windows/CoreAudio/IAudioEndpointWatchdogService.cs src/RigSwitch.Infrastructure/Windows/CoreAudio/AudioEndpointWatchdogService.cs tests/RigSwitch.Tests.Unit/AudioEndpointWatchdogServiceTests.cs
git commit -m "feat(infrastructure): implement AudioEndpointWatchdogService with blacklist lockout and speaker enforcement"
```

---

### Task 5: ViewModels & Options Filtering

**Files:**
- Modify: `src/RigSwitch.App/ViewModels/DeviceOptionResolver.cs`
- Modify: `src/RigSwitch.App/ViewModels/AudioEndpointVisibilityItemViewModel.cs`
- Modify: `src/RigSwitch.App/ViewModels/PresetConfigurationItemViewModel.cs`
- Modify: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs`
- Test: `tests/RigSwitch.Tests.Unit/DeviceOptionResolverBlacklistTests.cs`

**Interfaces:**
- `DeviceOptionResolver.FilterBlacklistedEndpoints`: filters out blacklisted devices from available preset options.
- `AudioEndpointVisibilityItemViewModel.IsBlacklisted`: toggle property with callback to update settings.
- `PresetConfigurationItemViewModel.SecondaryAudioId`: bound to UI dropdown.
- `MainSettingsViewModel.AudioToggleHotkey`, `MainSettingsViewModel.EnforcePrimaryAudio`.

- [ ] **Step 1: Write failing test for blacklist filtering in DeviceOptionResolver**

Create `tests/RigSwitch.Tests.Unit/DeviceOptionResolverBlacklistTests.cs`:
```csharp
namespace RigSwitch.Tests.Unit;

using RigSwitch.App.ViewModels;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;
using Xunit;

public sealed class DeviceOptionResolverBlacklistTests
{
    [Fact]
    public void ExcludeBlacklistedEndpoints_RemovesBlacklistedFromOptions()
    {
        var endpoints = new List<AudioEndpointInfo>
        {
            new("{DAC-1}", "Nobsound Shaker DAC", "USB", DevicePresenceState.Active, false, false, AudioDeviceFlow.Playback),
            new("{SPEAKERS}", "Pebble V3", "Realtek", DevicePresenceState.Active, true, false, AudioDeviceFlow.Playback)
        };
        var blacklisted = new List<string> { "{DAC-1}" };

        var allowed = DeviceOptionResolver.FilterBlacklistedEndpoints(endpoints, blacklisted);

        Assert.Single(allowed);
        Assert.Equal("{SPEAKERS}", allowed[0].Id);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~DeviceOptionResolverBlacklistTests`
Expected: FAIL due to missing `FilterBlacklistedEndpoints`.

- [ ] **Step 3: Implement blacklist filtering and ViewModel properties**

In `DeviceOptionResolver.cs`:
Add `FilterBlacklistedEndpoints(IEnumerable<AudioEndpointInfo> endpoints, IEnumerable<string> blacklistedIds)`.
In `AudioEndpointVisibilityItemViewModel.cs`:
Add `IsBlacklisted` boolean property with callback `onBlacklistChanged`.
In `PresetConfigurationItemViewModel.cs`:
Add `SecondaryAudioId` and `SecondaryMicrophoneId` properties and mapping methods.
In `MainSettingsViewModel.cs`:
Add `AudioToggleHotkey`, `EnforcePrimaryAudio`, bind them on load/save, and wire up `onBlacklistChanged`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/RigSwitch.Tests.Unit --filter FullyQualifiedName~DeviceOptionResolverBlacklistTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RigSwitch.App/ViewModels/DeviceOptionResolver.cs src/RigSwitch.App/ViewModels/AudioEndpointVisibilityItemViewModel.cs src/RigSwitch.App/ViewModels/PresetConfigurationItemViewModel.cs src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs tests/RigSwitch.Tests.Unit/DeviceOptionResolverBlacklistTests.cs
git commit -m "feat(ui): add blacklist filtering and secondary audio properties to view models"
```

---

### Task 6: UI Views Integration

**Files:**
- Modify: `src/RigSwitch.App/Views/MainSettingsWindow.xaml`
- Test: manual inspection / build verification

**Interfaces:**
- Preset Cards: add Secondary Audio (Headset / Sonar) ComboBox.
- Audio Devices Grid: add Blacklist / Bass Shaker toggle column and visual status badge.
- General / Audio Settings: add Enforce Primary Speakers checkbox and Audio Toggle Hotkey recorder control.

- [ ] **Step 1: Add Secondary Audio dropdowns to preset cards**
In `src/RigSwitch.App/Views/MainSettingsWindow.xaml`, in the preset configuration template, add the `SecondaryAudioId` ComboBox next to `PrimaryAudioId`.

- [ ] **Step 2: Add Blacklist toggle to Audio Devices tab**
In the Audio Endpoints DataGrid / ItemsControl in `MainSettingsWindow.xaml`, add a toggle switch or checkbox bound to `IsBlacklisted`, styled with warning/shield accent.

- [ ] **Step 3: Add Audio Toggle Hotkey and Enforce Primary Audio settings**
In the Hotkeys section, add `AudioToggleHotkey` with `HotkeyRecorderControl`, and add the `EnforcePrimaryAudio` checkbox.

- [ ] **Step 4: Build project to verify XAML compilation**
Run: `dotnet build src/RigSwitch.App/RigSwitch.App.csproj`
Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/RigSwitch.App/Views/MainSettingsWindow.xaml
git commit -m "feat(ui): add secondary audio, blacklist toggles, and audio hotkey to settings window"
```

---

### Task 7: Hotkey Registration, Tray Integration & App Lifecycle

**Files:**
- Modify: `src/RigSwitch.App/Services/TrayIconService.cs`
- Modify: `src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs`
- Modify: `src/RigSwitch.App/App.xaml.cs`
- Test: `tests/RigSwitch.Tests.Unit/MainSettingsViewModelAudioTests.cs`

**Interfaces:**
- `HotkeyService`: register `AudioToggleHotkey` to trigger `coordinator.ToggleAudioOutputModeAsync()`.
- `TrayIconService`: add menu item to toggle audio output mode and show notifications on switch.
- `App.xaml.cs`: instantiate and start `IAudioEndpointWatchdogService` on launch, stop on exit.

- [ ] **Step 1: Write unit test for AudioToggleHotkey registration**

Create `tests/RigSwitch.Tests.Unit/MainSettingsViewModelAudioTests.cs` verifying hotkey registration and toggle delegation.

- [ ] **Step 2: Implement hotkey and tray audio toggle**
Wire up `AudioToggleHotkey` in `MainSettingsViewModel.RegisterGlobalHotkeys`.
Update `TrayIconService` to add a "Switch Audio Output" context menu item.
In `App.xaml.cs`, register and start `AudioEndpointWatchdogService`.

- [ ] **Step 3: Run all unit tests**
Run: `dotnet test`
Expected: All tests PASS.

- [ ] **Step 4: Commit**

```bash
git add src/RigSwitch.App/Services/TrayIconService.cs src/RigSwitch.App/ViewModels/MainSettingsViewModel.cs src/RigSwitch.App/App.xaml.cs tests/RigSwitch.Tests.Unit/MainSettingsViewModelAudioTests.cs
git commit -m "feat(app): wire audio toggle hotkey, tray menu, and watchdog into application lifecycle"
```

---

### Task 8: Full Verification, Linting, and Typechecking

**Files:**
- Verify entire repository

- [ ] **Step 1: Run all backend unit tests**
Run: `dotnet test`
Expected: 100% tests pass.

- [ ] **Step 2: Run lint and typecheck per user rules**
Run: `npm run lint` and `npx tsc --noEmit` if web/docs project exists, or confirm C# formatting/analyzer status.

- [ ] **Step 3: Final commit and verify git clean**
Run: `git status`
Confirm working tree is clean.
