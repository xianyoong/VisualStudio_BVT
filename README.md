# Visual Studio BVT — WinAppDriver UI Automation

Automated **Build Verification Tests (BVT)** that drive the Visual Studio IDE (`devenv.exe`)
through its real UI using [WinAppDriver](https://github.com/microsoft/WinAppDriver) + Appium.
The suite launches Visual Studio, creates a new Console App project, builds it, and runs it —
verifying each step against the live UI tree.

- **Test framework:** xUnit (`net8.0`)
- **UI automation:** WinAppDriver + Appium (`OpenQA.Selenium.Appium.Windows`)
- **App under test:** Visual Studio (`devenv.exe`) — any edition (Community / Professional /
  Enterprise) and version (2019 / 2022 / 2026…), auto-detected.

---

## Contents

- [Requirements](#requirements)
- [Quick install (one command)](#quick-install-one-command)
- [Manual install](#manual-install)
- [How to run the tests](#how-to-run-the-tests)
- [Configuration](#configuration)
- [Running against a VM / Dev Box](#running-against-a-vm--dev-box)
- [Project structure](#project-structure)
- [Troubleshooting](#troubleshooting)

---

## Requirements

A clean Windows 10/11 machine needs:

| Requirement | Why |
|---|---|
| **Windows 10/11** | WinAppDriver only automates Windows desktop apps. |
| **.NET 8 SDK** | To build and run `dotnet test`. |
| **Git** | To clone the repo. |
| **WinAppDriver** | The UI-automation server the tests connect to. |
| **Windows Developer Mode** | Required by WinAppDriver. |
| **Visual Studio** | The app under test (any edition/version). |
| **An unlocked, interactive desktop** | UI automation cannot drive a locked session. |

The one-command installer below sets up everything except Visual Studio (optional flag).

---

## Quick install (one command)

Open **PowerShell as Administrator** on the target machine and run:

```powershell
irm https://raw.githubusercontent.com/xianyoong/VisualStudio_BVT/main/install.ps1 | iex
```

This will:

1. Verify `winget` is available
2. Install **Git** (if missing)
3. Install the **.NET 8 SDK** (if missing)
4. Enable **Windows Developer Mode**
5. Download + install **WinAppDriver**
6. Clone the repo to `%USERPROFILE%\VisualStudio_BVT`
7. Restore NuGet packages and build

> **Note:** Administrator rights are required (WinAppDriver install + Developer Mode).

### Include Visual Studio in the install

Visual Studio is **skipped by default** (it's a large download). To install it too, run the
script from file with the switch:

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -InstallVisualStudio
```

If Visual Studio is already installed (any edition/version), you don't need this — the tests
locate `devenv.exe` automatically.

---

## Manual install

If you prefer to set things up by hand:

```powershell
# 1. Install prerequisites (Administrator PowerShell)
winget install --id Git.Git --silent
winget install --id Microsoft.DotNet.SDK.8 --silent

# 2. Install WinAppDriver
#    Download the MSI from:
#    https://github.com/microsoft/WinAppDriver/releases
#    then run it (silent install):
#    msiexec /i WindowsApplicationDriver_1.2.1.msi /quiet

# 3. Enable Developer Mode
#    Settings > System > For developers > Developer Mode = On

# 4. Clone and build
git clone https://github.com/xianyoong/VisualStudio_BVT.git
cd VisualStudio_BVT
dotnet restore
dotnet build
```

---

## How to run the tests

WinAppDriver must be running **before** you start the tests.

**Step 1 — Start WinAppDriver** (keep this window open):

```powershell
& "C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe"
```

**Step 2 — In a second PowerShell window, run the tests:**

```powershell
cd "$env:USERPROFILE\VisualStudio_BVT"
dotnet test
```

**Run a single test:**

```powershell
dotnet test --filter "FullyQualifiedName~BuildSolutionTest"
dotnet test --filter "FullyQualifiedName~LaunchVisualStudioTestBVT"
```

> **Important:** Do not lock the screen or disconnect RDP while tests run — UI automation
> requires an active, unlocked desktop. The test will momentarily take control of the UI.

### What the tests do

| Test | Steps |
|---|---|
| `LaunchVisualStudioTestBVT` | Launches Visual Studio and verifies the shell becomes ready. |
| `BuildSolutionTest` | Creates a new **Console App**, selects a target framework, creates the project, builds it, runs it, and verifies the Output pane. |

---

## Configuration

Inputs come from [`config_global.json`](config_global.json) — never hard-coded:

```jsonc
{
  "Servers": {
    "Default": { "Host": "http://127.0.0.1:4723/", "Type": "WinAppDriver" }
  },
  "Apps": {
    "Default": {
      "Path": "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\devenv.exe",
      "Name": "Visual Studio 2022"
    }
  }
}
```

### Visual Studio path resolution

You normally don't need to set `Apps:Default:Path`. The tests resolve `devenv.exe` in this
order (first existing wins):

1. **`VS_APP_PATH`** environment variable (explicit override)
2. `config_global.json` → `Apps:Default:Path` (only if the file exists)
3. **`vswhere.exe`** — the official VS locator (finds any edition/version)
4. Filesystem scan of the standard install roots
5. A hard-coded default (last resort)

To force a specific install for one session:

```powershell
$env:VS_APP_PATH = "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe"
dotnet test
```

---

## Running against a VM / Dev Box

WinAppDriver drives the UI of the **machine it runs on**. To target a remote VM / Dev Box,
run WinAppDriver **on that VM** and point the tests at it:

1. **On the VM:** install Visual Studio + WinAppDriver, then start WinAppDriver bound to the
   network interface and open the firewall for port 4723:
   ```powershell
   & "C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe" 0.0.0.0 4723
   ```
2. **In `config_global.json`:** set the host to the VM:
   ```jsonc
   "Servers": { "Default": { "Host": "http://<VM-IP-or-hostname>:4723/", "Type": "WinAppDriver" } }
   ```

> The VM must stay **logged in with an unlocked desktop** for the duration of the run.

---

## Project structure

```
VisualStudio_BVT/
├─ install.ps1                 # One-command bootstrap for a clean machine
├─ config_global.json          # Server endpoint + app path + users (no hard-coded inputs)
├─ VisualStudioTests.csproj     # net8.0 xUnit test project
├─ test/
│  └─ BVT_Test.cs              # The BVT test cases (xUnit)
├─ helper/
│  ├─ Helper.cs                # Reusable WinAppDriver UI actions (XPath-first, wait-then-act)
│  └─ Config.cs                # Strongly typed config_global.json reader
├─ TestCasesData/              # Human-readable test-case descriptions
└─ tools/                      # Supporting scripts (CSV/test-case generation)
```

---

## Troubleshooting

| Symptom | Cause / Fix |
|---|---|
| `The system cannot find the file specified` at session start | `devenv.exe` not found. Install Visual Studio or set `VS_APP_PATH`. |
| `Currently selected window has been closed` | Old issue with the VS splash screen — pull the latest code and rebuild. |
| `Cannot connect to ... 4723` / connection refused | WinAppDriver isn't running. Start it (Step 1). |
| `You must enable Developer Mode` | Turn on Developer Mode (the installer does this). |
| Test fails finding a dialog control | UI differs by VS version. The framework dropdown is handled version-agnostically; other locators may need tuning for your VS build. |
| Tests hang / fail randomly | The desktop is locked or RDP disconnected. Keep the session unlocked. |
| `Already up to date` after a fix | The clone didn't receive the change — confirm you pushed and are pulling the same branch. |

---

## Updating

```powershell
cd "$env:USERPROFILE\VisualStudio_BVT"
git pull
dotnet build
```
