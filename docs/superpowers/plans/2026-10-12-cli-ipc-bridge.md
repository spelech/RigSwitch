# RigSwitch CLI & IPC Bridge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a first-class Command Line Interface (CLI) and Named Pipe IPC bridge into RigSwitch for status querying, profile switching, audio toggling, diagnostics, and live UI screenshot capture from terminals, scripts, and AI agents.

**Architecture:** 
1. Core protocol messages (`IpcRequest`, `IpcResponse`) in `RigSwitch.Core`.
2. Zero-overhead Named Pipe transport (`\\.\pipe\RigSwitch-Ipc`) with client and server in `RigSwitch.Infrastructure`.
3. Command dispatcher in `RigSwitch.App` wiring IPC requests directly to the live `IProfileSwitchCoordinator`, `IAudioEndpointWatchdogService`, and WPF Dispatcher (for screenshot rendering).
4. Dual-mode startup in `App.xaml.cs`: CLI mode with Win32 console attachment vs. normal GUI/Tray mode with background IPC server.

**Tech Stack:** C# .NET 10, Windows Named Pipes (`NamedPipeServerStream`, `NamedPipeClientStream`), Win32 `AttachConsole`, `System.Text.Json`, WPF `RenderTargetBitmap`.

## Global Constraints

- Never break existing `UserSettings` JSON serialization backward compatibility.
- Never block the WPF UI thread during IPC operations.
- All non-GUI CLI commands must provide a headless fallback if the tray app is not running.
- Support both formatted text and `--json` outputs.
- All 372 existing tests in `RigSwitch.Tests.Unit` must pass without regressions.
- Always run `npm run lint` and `npx tsc --noEmit` after code changes.

---

### Task 1: Core IPC Protocol Models & Serialization

**Files:**
- Create: `src/RigSwitch.Core/Ipc/IpcRequest.cs`
- Create: `src/RigSwitch.Core/Ipc/IpcResponse.cs`
- Create: `src/RigSwitch.Core/Ipc/IpcCommands.cs`
- Test: `tests/RigSwitch.Tests.Unit/IpcProtocolTests.cs`

**Interfaces:**
- Produces: `IpcRequest`, `IpcResponse`, `IpcResponse<T>`, `IpcCommands` constants (`Status`, `Switch`, `Audio`, `Blacklist`, `Screenshot`, `Doctor`).

- [ ] **Step 1: Write failing serialization tests in `IpcProtocolTests.cs`**
- [ ] **Step 2: Run test to confirm it fails (`dotnet test --filter FullyQualifiedName~IpcProtocolTests`)**
- [ ] **Step 3: Implement `IpcRequest`, `IpcResponse`, `IpcCommands` in `RigSwitch.Core/Ipc/`**
- [ ] **Step 4: Verify tests pass**
- [ ] **Step 5: Commit Task 1** (`feat(core): add IPC protocol request and response models`)

---

### Task 2: Named Pipe IPC Transport

**Files:**
- Create: `src/RigSwitch.Infrastructure/Ipc/IIpcServer.cs`
- Create: `src/RigSwitch.Infrastructure/Ipc/IIpcClient.cs`
- Create: `src/RigSwitch.Infrastructure/Ipc/NamedPipeIpcServer.cs`
- Create: `src/RigSwitch.Infrastructure/Ipc/NamedPipeIpcClient.cs`
- Test: `tests/RigSwitch.Tests.Unit/NamedPipeIpcTests.cs`

**Interfaces:**
- Consumes: `IpcRequest`, `IpcResponse`
- Produces: `IIpcServer` (`Start()`, `Stop()`, `event Func<IpcRequest, Task<IpcResponse>> RequestReceived`), `IIpcClient` (`Task<IpcResponse> SendAsync(IpcRequest, CancellationToken)`).

- [ ] **Step 1: Write integration tests in `NamedPipeIpcTests.cs` for client-server communication**
- [ ] **Step 2: Run test to confirm it fails**
- [ ] **Step 3: Implement `NamedPipeIpcServer` and `NamedPipeIpcClient` with timeout handling & cancellation**
- [ ] **Step 4: Verify tests pass**
- [ ] **Step 5: Commit Task 2** (`feat(infra): implement Named Pipe IPC server and client`)

---

### Task 3: IPC Command Dispatcher & Live Service Wiring

**Files:**
- Create: `src/RigSwitch.App/Services/IpcCommandHandler.cs`
- Modify: `src/RigSwitch.App/App.xaml.cs`
- Test: `tests/RigSwitch.Tests.Unit/IpcCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IProfileSwitchCoordinator`, `ISettingsStorageService`, `IAudioEndpointWatchdogService`, `IDisplayConfigurationService`, `IAudioEndpointDirector`
- Produces: `IpcCommandHandler.HandleRequestAsync(IpcRequest)` executing:
  - `status`: Returns live profile, audio mode, watchdog status, monitors, endpoints.
  - `switch`: Executes `coordinator.SwitchProfileAsync()`.
  - `audio`: Executes `coordinator.ToggleAudioOutputModeAsync()`.
  - `blacklist`: Lists or updates blacklisted DACs.
  - `screenshot`: Dispatches to UI thread, switches view, captures PNG via `RenderTargetBitmap`.
  - `doctor`: Health diagnostic summary.

- [ ] **Step 1: Write failing tests in `IpcCommandHandlerTests.cs`**
- [ ] **Step 2: Run test to confirm it fails**
- [ ] **Step 3: Implement `IpcCommandHandler.cs` and wire into `App.xaml.cs` tray startup**
- [ ] **Step 4: Verify tests pass**
- [ ] **Step 5: Commit Task 3** (`feat(app): add IPC command handler with live UI screenshot capture`)

---

### Task 4: CLI Argument Parser, Console Attacher & Headless Fallback

**Files:**
- Create: `src/RigSwitch.App/Services/CliController.cs`
- Modify: `src/RigSwitch.App/App.xaml.cs`
- Test: `tests/RigSwitch.Tests.Unit/CliControllerTests.cs`

**Interfaces:**
- Consumes: Win32 `AttachConsole`, `IIpcClient`, headless services.
- Produces: `CliController.RunAsync(string[] args)` parsing arguments, communicating with pipe or falling back to headless execution, formatting output (plain text or `--json`), and exiting with standard status codes.

- [ ] **Step 1: Write failing tests in `CliControllerTests.cs` for argument parsing and formatting**
- [ ] **Step 2: Run test to confirm it fails**
- [ ] **Step 3: Implement `CliController.cs` and Win32 console attachment in `App.xaml.cs`**
- [ ] **Step 4: Verify tests pass**
- [ ] **Step 5: Commit Task 4** (`feat(app): implement CLI entry point with console attachment and headless fallback`)

---

### Task 5: Live Verification & Testing

**Files:**
- Test: All tests in `tests/RigSwitch.Tests.Unit/`

- [ ] **Step 1: Run full automated test suite** (`dotnet test`)
- [ ] **Step 2: Run linter and typechecker** (`npm run lint`, `npx tsc --noEmit`)
- [ ] **Step 3: Live verify CLI execution**
  - Run `RigSwitch.exe status`
  - Run `RigSwitch.exe status --json`
  - Run `RigSwitch.exe doctor`
- [ ] **Step 4: Commit and push branch `feat/cli-ipc-bridge`**
