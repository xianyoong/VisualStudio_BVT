# install.ps1 - One-line bootstrap for VisualStudio_BVT (WinAppDriver UI automation).
#
# Usage on a fresh Windows 10/11 machine (run in an elevated PowerShell):
#   irm https://raw.githubusercontent.com/xianyoong/VisualStudio_BVT/main/install.ps1 | iex
#
# If your execution policy blocks scripts, use:
#   powershell -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/xianyoong/VisualStudio_BVT/main/install.ps1 | iex"
#
# What this installs on a clean VM:
#   1. git                 (via winget)            - to clone the repo
#   2. .NET 8 SDK          (via winget)            - to build / run `dotnet test`
#   3. WinAppDriver        (official MSI)          - the UI-automation server
#   4. Windows Developer Mode                      - REQUIRED by WinAppDriver
#   5. The repo            (git clone)             - into %USERPROFILE%\VisualStudio_BVT
#   6. NuGet dependencies  (dotnet restore/build)  - Appium.WebDriver, xUnit, etc.
#
# Optional:
#   -InstallVisualStudio   also install Visual Studio 2022 Community (the app under test, large download)
#
# NOTE: installing WinAppDriver and enabling Developer Mode require administrator rights.

[CmdletBinding()]
param(
    [switch]$InstallVisualStudio
)

$ErrorActionPreference = "Stop"

# ---- configuration ---------------------------------------------------------
$RepoUrl          = "https://github.com/xianyoong/VisualStudio_BVT.git"
$RepoDir          = Join-Path $HOME "VisualStudio_BVT"
$WinAppDriverVer  = "1.2.1"
$WinAppDriverMsi  = "https://github.com/microsoft/WinAppDriver/releases/download/v$WinAppDriverVer/WindowsApplicationDriver_$WinAppDriverVer.msi"
$WinAppDriverExe  = "C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe"

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "    $msg" -ForegroundColor Yellow }

function Add-ToSessionPath($dir) {
    if (-not ($env:Path -split ';' | Where-Object { $_ -ieq $dir })) {
        $env:Path = "$dir;$env:Path"
    }
}

function Update-PathFromRegistry {
    # winget installs land in machine/user PATH; refresh this session without a restart.
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path", "User")
}

function Test-IsAdmin {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object System.Security.Principal.WindowsPrincipal($id)).IsInRole(
        [System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

# ---- 0. admin check --------------------------------------------------------
if (-not (Test-IsAdmin)) {
    throw "This script must be run as Administrator (WinAppDriver install + Developer Mode require it). Re-open PowerShell with 'Run as administrator' and retry."
}

# ---- 1. winget -------------------------------------------------------------
Write-Step "Checking for winget..."
if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    throw "winget (App Installer) is not available. Install it from the Microsoft Store ('App Installer'), then re-run this script."
}
Write-Ok "winget present: $((winget --version) 2>&1)"

# ---- 2. git ----------------------------------------------------------------
Write-Step "Checking for git..."
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Warn "git not found. Installing via winget..."
    winget install --id Git.Git --silent --accept-package-agreements --accept-source-agreements
    Update-PathFromRegistry
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw "git installed but not on PATH. Open a new PowerShell and retry."
    }
    Write-Ok "git installed."
} else {
    Write-Ok "git already installed: $((git --version) 2>&1)"
}

# ---- 3. .NET 8 SDK ---------------------------------------------------------
Write-Step "Checking for .NET 8 SDK..."
$hasNet8 = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $hasNet8 = (dotnet --list-sdks 2>&1 | Select-String -SimpleMatch "8.") -ne $null
}
if (-not $hasNet8) {
    Write-Warn ".NET 8 SDK not found. Installing via winget..."
    winget install --id Microsoft.DotNet.SDK.8 --silent --accept-package-agreements --accept-source-agreements
    Update-PathFromRegistry
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet installed but not on PATH. Open a new PowerShell and retry."
    }
    Write-Ok ".NET 8 SDK installed: $((dotnet --version) 2>&1)"
} else {
    Write-Ok ".NET SDK already present: $((dotnet --version) 2>&1)"
}

# ---- 4. Windows Developer Mode (required by WinAppDriver) -------------------
Write-Step "Enabling Windows Developer Mode (required by WinAppDriver)..."
$devModeKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
if (-not (Test-Path $devModeKey)) {
    New-Item -Path $devModeKey -Force | Out-Null
}
New-ItemProperty -Path $devModeKey -Name "AllowDevelopmentWithoutDevLicense" `
    -PropertyType DWord -Value 1 -Force | Out-Null
Write-Ok "Developer Mode enabled."

# ---- 5. WinAppDriver -------------------------------------------------------
Write-Step "Checking for WinAppDriver..."
if (Test-Path $WinAppDriverExe) {
    Write-Ok "WinAppDriver already installed at $WinAppDriverExe"
} else {
    Write-Warn "WinAppDriver not found. Downloading + installing v$WinAppDriverVer ..."
    $msiPath = Join-Path $env:TEMP "WindowsApplicationDriver_$WinAppDriverVer.msi"
    Invoke-WebRequest -Uri $WinAppDriverMsi -OutFile $msiPath
    Start-Process msiexec.exe -ArgumentList "/i `"$msiPath`" /quiet /norestart" -Wait
    Remove-Item $msiPath -ErrorAction SilentlyContinue
    if (-not (Test-Path $WinAppDriverExe)) {
        throw "WinAppDriver MSI ran but the executable was not found at $WinAppDriverExe."
    }
    Write-Ok "WinAppDriver installed."
}

# ---- 6. Visual Studio (optional, app under test) ---------------------------
if ($InstallVisualStudio) {
    Write-Step "Installing Visual Studio 2022 Community (this is a large download)..."
    winget install --id Microsoft.VisualStudio.2022.Community --silent `
        --accept-package-agreements --accept-source-agreements
    Write-Ok "Visual Studio 2022 Community installed."
} else {
    Write-Warn "Skipping Visual Studio install (the app under test). Re-run with -InstallVisualStudio to include it,"
    Write-Warn "or set the VS_APP_PATH environment variable / config_global.json to an existing devenv.exe."
}

# ---- 7. Repo ---------------------------------------------------------------
Write-Step "Fetching repo into $RepoDir..."
if (Test-Path (Join-Path $RepoDir ".git")) {
    Push-Location $RepoDir
    git pull --ff-only
    Pop-Location
    Write-Ok "Repo updated (git pull)."
} elseif (Test-Path $RepoDir) {
    throw "$RepoDir exists but is not a git repo. Move/delete it and re-run."
} else {
    git clone $RepoUrl $RepoDir
    Write-Ok "Repo cloned."
}

# ---- 8. Restore + build ----------------------------------------------------
Write-Step "Restoring NuGet packages and building..."
Push-Location $RepoDir
try {
    dotnet restore
    dotnet build --no-restore -c Debug
} finally {
    Pop-Location
}
Write-Ok "Build complete."

# ---- 9. Done ---------------------------------------------------------------
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " VisualStudio_BVT is installed at: $RepoDir" -ForegroundColor Green
Write-Host ""
Write-Host " To run the tests:" -ForegroundColor Green
Write-Host "   1. Start WinAppDriver (keep this window open):" -ForegroundColor White
Write-Host "      & `"$WinAppDriverExe`"" -ForegroundColor White
Write-Host "   2. In another terminal:" -ForegroundColor White
Write-Host "      cd `"$RepoDir`"" -ForegroundColor White
Write-Host "      dotnet test" -ForegroundColor White
Write-Host ""
Write-Host " Notes:" -ForegroundColor Green
Write-Host "   * The VM must stay logged in with an UNLOCKED interactive desktop -" -ForegroundColor White
Write-Host "     WinAppDriver cannot drive the UI of a locked session." -ForegroundColor White
Write-Host "   * Set VS_APP_PATH or edit config_global.json (Apps:Default:Path) to point" -ForegroundColor White
Write-Host "     at devenv.exe if Visual Studio is installed elsewhere." -ForegroundColor White
Write-Host "============================================================" -ForegroundColor Green
