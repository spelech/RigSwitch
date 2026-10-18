# RigSwitch Workspace Guidelines

## Verification & Visual Screenshot Testing
- Run `dotnet test --nologo`, `npm run lint`, and `npx tsc --noEmit` after code changes.
- When modifying WPF views (`MainSettingsWindow.xaml`, `FluentTheme.xaml`) or layout ViewModels:
  1. Regenerate the documentation screenshots via `dotnet test --filter "FullyQualifiedName~UiScreenshotCaptureTests"`.
  2. Visually inspect all 5 images in `docs/screenshots/*.png` using `view_file` to confirm zero unwanted vertical scrollbars and zero badge/text clipping at `1240x840`.
- For headless WPF `RenderTargetBitmap` capture in STA unit tests, render `(FrameworkElement)window.Content` (ensuring the root `Grid` has an explicit `Background` brush and calling `Measure`, `Arrange`, and `UpdateLayout`) rather than rendering the `Window` instance directly, which produces a black bitmap without an active HWND.

## Domain & Testing Invariants
- `new UserSettings()` pre-populates 3 default `DeskPresets` and 3 default `RigPresets`. Tests that construct custom preset lists on a `UserSettings` instance must call `.Clear()` on `DeskPresets` / `RigPresets` before adding test presets.
- Windows CoreAudio COM calls (`IMMDeviceEnumerator`, `IPolicyConfig`) invoked from threadpool threads must call `CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED)`.
- `AudioEndpointWatchdogService` must suppress automatic re-assertion while a profile switch is in progress (`BeginProfileSwitch` / `EndProfileSwitch`) and must never re-assert a target endpoint whose state is `NotPresent` (unplugged).
