# Installation Guide

RigSwitch provides two straightforward installation methods: the official setup executable or Microsoft's Windows Package Manager (**WinGet**).

---

## Method 1: Official Setup Installer (Recommended)

1. Download the latest installer executable:
   * [Download RigSwitch-Setup-v1.0.0.exe](https://github.com/spelech/RigSwitch/releases/latest/download/RigSwitch-Setup-v1.0.0.exe)
2. Run `RigSwitch-Setup-v1.0.0.exe`.
3. The setup wizard installs the application cleanly to:
   ```
   %LOCALAPPDATA%\Programs\RigSwitch\
   ```
4. Shortcuts are automatically placed on your **Desktop** and **Start Menu**.
5. RigSwitch starts immediately in your system tray and registers with Windows boot autorun.

::: tip No Administrator Rights Required
Because RigSwitch installs into `%LOCALAPPDATA%` and operates strictly on user-level Windows APIs, no administrator elevation or UAC prompts are required.
:::

---

## Method 2: Windows Package Manager (WinGet)

To install or upgrade RigSwitch via PowerShell or Windows Terminal:

```powershell
# Install RigSwitch
winget install spelech.RigSwitch

# Upgrade RigSwitch to the latest release
winget upgrade spelech.RigSwitch
```

---

## Building from Source

If you prefer building from the Git repository:

```powershell
# Clone the repository
git clone https://github.com/spelech/RigSwitch.git
cd RigSwitch

# Build the complete solution in Release mode
dotnet build RigSwitch.slnx -c Release

# Execute test suite (242/242 passing tests)
dotnet test RigSwitch.slnx -c Release

# Launch the desktop tray application
dotnet run --project src/RigSwitch.App -c Release
```
