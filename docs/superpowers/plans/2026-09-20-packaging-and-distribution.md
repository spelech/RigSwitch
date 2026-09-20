# RigSwitch Packaging, Installer & Winget Distribution Implementation Plan

> **Goal**: Provide an automated installer/uninstaller (`RigSwitch-Setup-v<VERSION>.exe`) and winget distribution pipeline for RigSwitch, with local build scripts and GitHub Actions release automation.
>
> **Architecture**: Inno Setup 6 `.iss` packaging script for per-user installation without UAC elevation, integrated into a GitHub Actions release pipeline and winget manifest generator.
>
> **Tech Stack**: Inno Setup 6, PowerShell, .NET 10 SDK, GitHub Actions, Microsoft WinGet CLI.

## Global Constraints
- Target Framework: net10.0-windows
- Anti-Bloat: Strict ban on *Manager, *Helper, or *Util junk drawers.
- Strict limit: Max 500 lines of code per file.
- Nullable: enable, ImplicitUsings: enable, TreatWarningsAsErrors: true.
- Installer must support Per-User install (`PrivilegesRequired=lowest`) to `%LOCALAPPDATA%\Programs\RigSwitch`.
- Uninstaller must cleanly remove all files and shortcuts while preserving `%APPDATA%\RigSwitch\settings.json`.

---

### Task 1: Inno Setup Installer Script (`packaging/installer.iss`)

**Files:**
- Create: `packaging/installer.iss`

**Description:**
Create an Inno Setup script that:
- Packages `publish/RigSwitch.App.exe` and `LICENSE`.
- Sets `PrivilegesRequired=lowest` (Per-User).
- Default install directory: `{localappdata}\Programs\RigSwitch`.
- Creates Start Menu shortcut `{userprograms}\RigSwitch\RigSwitch.lnk`.
- Optional Desktop shortcut `{userdesktop}\RigSwitch.lnk`.
- Optional autostart entry in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Shuts down any running `RigSwitch.App.exe` before installation or uninstallation using `CloseApplications=yes`.
- Customizes metadata: Publisher="spelech", AppName="RigSwitch", AppVersion="{#MyAppVersion}".

---

### Task 2: Local Build & Test Script (`scripts/build-installer.ps1`)

**Files:**
- Create: `scripts/build-installer.ps1`

**Description:**
PowerShell script to build the self-contained Release single-file executable, locate `iscc.exe`, compile `dist/RigSwitch-Setup-<VERSION>.exe`, compute the SHA256 checksum, and output the artifact.

---

### Task 3: Winget Manifest Generator Script (`scripts/generate-winget-manifests.ps1`)

**Files:**
- Create: `scripts/generate-winget-manifests.ps1`

**Description:**
PowerShell script that generates the standard three-file WinGet manifest package for `spelech.RigSwitch`:
1. `spelech.RigSwitch.yaml` (version manifest)
2. `spelech.RigSwitch.installer.yaml` (installer manifest with download URL, SHA256, silent switches)
3. `spelech.RigSwitch.locale.en-US.yaml` (metadata manifest with description, tags, license)

---

### Task 4: GitHub Actions Release Pipeline (`.github/workflows/release.yml`)

**Files:**
- Create: `.github/workflows/release.yml`

**Description:**
GitHub Actions workflow triggered on tag pushes (`v*`):
- Runs full solution test suite in Release mode.
- Publishes self-contained single-file win-x64 binary.
- Compiles Inno Setup installer.
- Computes SHA256 checksum.
- Generates winget manifests.
- Publishes a GitHub Release with installer, checksum, and manifests attached.

---

### Task 5: End-to-End Verification & Testing

**Steps:**
1. Execute `scripts/build-installer.ps1` to produce `dist/RigSwitch-Setup-v1.0.0.exe`.
2. Test silent installation: `.\dist\RigSwitch-Setup-v1.0.0.exe /VERYSILENT /NORESTART`.
3. Verify files in `%LOCALAPPDATA%\Programs\RigSwitch`, Start Menu shortcut, and installed apps registry.
4. Verify application runs from the install directory.
5. Test silent uninstallation: run `unins000.exe /VERYSILENT /NORESTART` and verify clean removal of binaries and shortcuts.
