<#
.SYNOPSIS
    Builds the self-contained RigSwitch release binary and compiles the Inno Setup installer.

.PARAMETER Version
    The semantic version string to package (defaults to "1.0.0").

.EXAMPLE
    .\scripts\build-installer.ps1 -Version "1.0.0"
#>
[CmdletBinding()]
param (
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Building RigSwitch v$Version Installer" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Publish Release self-contained single-file executable
Write-Host "`n[1/4] Publishing self-contained win-x64 executable..." -ForegroundColor Yellow
$publishDir = Join-Path $RepoRoot "publish"
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

dotnet publish src/RigSwitch.App/RigSwitch.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# 2. Locate Inno Setup Compiler (iscc.exe)
Write-Host "`n[2/4] Locating Inno Setup 6 compiler (iscc.exe)..." -ForegroundColor Yellow
$isccPaths = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$iscc = $null
foreach ($path in $isccPaths) {
    if ($path -and (Test-Path $path)) {
        $iscc = $path
        break
    }
}

if (-not $iscc) {
    throw "Inno Setup compiler (ISCC.exe) not found. Install it via 'winget install JRSoftware.InnoSetup' or install Inno Setup 6."
}
Write-Host "Found compiler: $iscc" -ForegroundColor Green

# 3. Compile installer
Write-Host "`n[3/4] Compiling installer..." -ForegroundColor Yellow
$distDir = Join-Path $RepoRoot "dist"
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

$issScript = Join-Path $RepoRoot "packaging\installer.iss"
& $iscc "/DMyAppVersion=$Version" $issScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
}

# 4. Verify output and calculate SHA256 checksum
Write-Host "`n[4/4] Verifying output and computing checksum..." -ForegroundColor Yellow
$installerExe = Join-Path $distDir "RigSwitch-Setup-v$Version.exe"
if (-not (Test-Path $installerExe)) {
    throw "Expected installer artifact not found: $installerExe"
}

$hash = (Get-FileHash -Path $installerExe -Algorithm SHA256).Hash
$hashFile = "$installerExe.sha256"
"$hash  $(Split-Path $installerExe -Leaf)" | Set-Content -Path $hashFile -Encoding utf8

$fileSizeMb = [math]::Round(((Get-Item $installerExe).Length / 1MB), 2)
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  Build Succeeded!" -ForegroundColor Green
Write-Host "  Installer: $installerExe ($fileSizeMb MB)" -ForegroundColor White
Write-Host "  SHA256:    $hash" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
