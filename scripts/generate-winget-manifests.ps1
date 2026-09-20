<#
.SYNOPSIS
    Generates standard Microsoft WinGet package manifests for spelech.RigSwitch.

.PARAMETER Version
    The package version (e.g., "1.0.0").

.PARAMETER InstallerSha256
    The SHA256 hash of the compiled setup executable. If omitted, computes it from dist/RigSwitch-Setup-v<Version>.exe.

.PARAMETER OutputDirectory
    Directory where manifests are written (defaults to "dist/winget/manifests/s/spelech/RigSwitch/<Version>").
#>
[CmdletBinding()]
param (
    [string]$Version = "1.0.0",
    [string]$InstallerSha256,
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$cleanVersion = $Version.TrimStart('v')
$today = (Get-Date).ToString("yyyy-MM-dd")

if (-not $InstallerSha256) {
    $installerExe = Join-Path $RepoRoot "dist\RigSwitch-Setup-v$cleanVersion.exe"
    if (Test-Path $installerExe) {
        $InstallerSha256 = (Get-FileHash -Path $installerExe -Algorithm SHA256).Hash
    } else {
        $InstallerSha256 = "0000000000000000000000000000000000000000000000000000000000000000"
    }
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $RepoRoot "dist\winget\manifests\s\spelech\RigSwitch\$cleanVersion"
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

$downloadUrl = "https://github.com/spelech/RigSwitch/releases/download/v$cleanVersion/RigSwitch-Setup-v$cleanVersion.exe"

# 1. Version Manifest
$versionYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.6.0.schema.json

PackageIdentifier: spelech.RigSwitch
PackageVersion: $cleanVersion
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
"@

# 2. Installer Manifest
$installerYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.6.0.schema.json

PackageIdentifier: spelech.RigSwitch
PackageVersion: $cleanVersion
InstallerLocale: en-US
InstallerType: inno
Scope: user
InstallModes:
  - interactive
  - silent
  - silentWithProgress
InstallerSwitches:
  Silent: /VERYSILENT /NORESTART
  SilentWithProgress: /SILENT /NORESTART
UpgradeBehavior: install
ReleaseDate: $today
Installers:
  - Architecture: x64
    InstallerUrl: $downloadUrl
    InstallerSha256: $InstallerSha256
ManifestType: installer
ManifestVersion: 1.6.0
"@

# 3. Default Locale Manifest
$localeYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json

PackageIdentifier: spelech.RigSwitch
PackageVersion: $cleanVersion
PackageLocale: en-US
Publisher: spelech
PublisherUrl: https://github.com/spelech
PublisherSupportUrl: https://github.com/spelech/RigSwitch/issues
PackageName: RigSwitch
PackageUrl: https://github.com/spelech/RigSwitch
License: MIT
LicenseUrl: https://github.com/spelech/RigSwitch/blob/develop/LICENSE
Copyright: Copyright (c) 2026 spelech
ShortDescription: Ultra-fast Windows hardware control plane and system tray switcher between Desk and Sim Rig profiles.
Description: RigSwitch is an ultra-fast, native Windows system tray control plane and workstation orchestrator. It switches instantly between a Desk Setup and a Sim Rig Setup via global hotkeys or the system tray, disabling the inactive display at the Win32 CCD driver level and routing CoreAudio playback endpoints with fail-safe gates.
Tags:
  - display
  - monitor
  - sim-rig
  - audio
  - system-tray
  - hotkey
ManifestType: defaultLocale
ManifestVersion: 1.6.0
"@

$versionFile = Join-Path $OutputDirectory "spelech.RigSwitch.yaml"
$installerFile = Join-Path $OutputDirectory "spelech.RigSwitch.installer.yaml"
$localeFile = Join-Path $OutputDirectory "spelech.RigSwitch.locale.en-US.yaml"

$versionYaml | Set-Content -Path $versionFile -Encoding utf8
$installerYaml | Set-Content -Path $installerFile -Encoding utf8
$localeYaml | Set-Content -Path $localeFile -Encoding utf8

Write-Host "Generated winget manifests in: $OutputDirectory" -ForegroundColor Green
Write-Host "  - $(Split-Path $versionFile -Leaf)" -ForegroundColor White
Write-Host "  - $(Split-Path $installerFile -Leaf)" -ForegroundColor White
Write-Host "  - $(Split-Path $localeFile -Leaf)" -ForegroundColor White
