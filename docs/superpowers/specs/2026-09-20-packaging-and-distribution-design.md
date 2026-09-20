# RigSwitch Packaging, Installer & Winget Distribution Specification

> **Target Standard**: Steven T. Pelech's `AgenticEngineeringToolbelt`  
> **Installer Engine**: Inno Setup 6 (`packaging/installer.iss`)  
> **Distribution Target**: GitHub Releases & Windows Package Manager (`winget`)  
> **Installation Scope**: Per-User (`PrivilegesRequired=lowest`)

---

## 1. Overview & Objectives

Provide an automated, seamless installation, uninstallation, and update pipeline for **RigSwitch**:
1. **Desktop Installer**: A compact, single-executable setup wizard (`RigSwitch-Setup-v<VERSION>.exe`) that installs per-user to `%LOCALAPPDATA%\Programs\RigSwitch` without requiring Administrator privileges or UAC prompts.
2. **Shortcuts & Autostart**: Creates Start Menu entries, an optional desktop shortcut, and an optional "Launch on Windows Startup" registry entry under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
3. **Clean Uninstallation**: Registers standard Windows uninstallation in `Settings > Apps > Installed Apps`, safely shutting down running instances before removing application binaries and shortcuts, while preserving user settings in `%APPDATA%\RigSwitch\settings.json`.
4. **Winget Distribution**: Formats manifest files (`spelech.RigSwitch.yaml`, `spelech.RigSwitch.installer.yaml`, `spelech.RigSwitch.locale.en-US.yaml`) compliant with Microsoft's `winget-pkgs` specification, enabling `winget install spelech.RigSwitch` and `winget upgrade spelech.RigSwitch`.
5. **CI/CD Release Automation**: GitHub Actions workflow triggered on version tags (`v*.*.*`) that runs full test verification, compiles the self-contained Release single-file binary, packages the Inno Setup installer, generates SHA256 checksums, publishes the GitHub Release, and stages the winget manifests.

---

## 2. Installer Architecture & Configuration (`packaging/installer.iss`)

### 2.1 File System & Registry Layout
* **Install Directory**: `{localappdata}\Programs\RigSwitch`
* **Main Executable**: `RigSwitch.App.exe`
* **Uninstaller**: `{localappdata}\Programs\RigSwitch\unins000.exe`
* **Settings Directory**: `{userappdata}\RigSwitch\settings.json` (Preserved across upgrades and uninstalls)
* **Start Menu Shortcut**: `{userprograms}\RigSwitch\RigSwitch.lnk`
* **Optional Desktop Shortcut**: `{userdesktop}\RigSwitch.lnk`
* **Optional Startup Run Key**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` -> `"RigSwitch" = "{localappdata}\Programs\RigSwitch\RigSwitch.App.exe"`

### 2.2 Process Guarding
* On install or update: Inno Setup checks if `RigSwitch.App.exe` is currently running. If running, it signals or requests the user/process to exit before replacing files.
* On uninstall: Ensures running instances are closed before removing binaries.

---

## 3. Winget Manifest Specification

### 3.1 Package Identifiers
* **PackageIdentifier**: `spelech.RigSwitch`
* **PackageName**: `RigSwitch`
* **Publisher**: `spelech`
* **License**: `MIT`
* **ShortDescription**: Ultra-fast Windows hardware control plane and system tray switcher between Desk and Sim Rig profiles.

### 3.2 Installer Manifest Properties
* **InstallerType**: `inno`
* **Scope**: `user`
* **InstallModes**: `interactive`, `silent`, `silentWithProgress`
* **SilentWithProgressSwitches**: `/SILENT /NORESTART`
* **SilentSwitches**: `/VERYSILENT /NORESTART`
* **UpgradeBehavior**: `install`

---

## 4. GitHub Actions Release Pipeline (`.github/workflows/release.yml`)

### 4.1 Trigger
Triggered automatically on push of git tags matching `v*` (e.g. `v1.0.0`).

### 4.2 Pipeline Stages
1. **Test Verification**: Runs unit and simulation harness tests on `windows-latest`.
2. **Single-File Publish**: `dotnet publish src/RigSwitch.App/RigSwitch.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/`
3. **Inno Setup Compilation**: Compiles `packaging/installer.iss` using Inno Setup 6, outputting `dist/RigSwitch-Setup-<VERSION>.exe`.
4. **Checksum Generation**: Computes SHA256 checksum and produces `dist/RigSwitch-Setup-<VERSION>.exe.sha256`.
5. **Winget Manifest Staging**: Runs PowerShell script to generate winget manifests populated with release URL and SHA256 hash.
6. **GitHub Release**: Uses `softprops/action-gh-release` to publish the release with the installer, checksum, and manifests attached as assets.

---

## 5. Local Build & Test Script (`scripts/build-installer.ps1`)

A standalone PowerShell script allowing developers to build and test the full installer locally:
* Publishes Release single-file executable.
* Detects Inno Setup compiler (`ISCC.exe`).
* Compiles `RigSwitch-Setup-<VERSION>.exe`.
* Verifies SHA256 hash.
* Tests silent installation and uninstallation switches.
