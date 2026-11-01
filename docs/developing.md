# Developing wsl-manager

This repository hosts **WorldGate.Monitor** under `tools/WorldGate.Monitor/` while the product migrates toward a standalone WSL manager. Builds target .NET 8.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 for running or installing the WinForms tray app (core library and tests run on Linux/macOS/Windows)

## Repository layout check

Confirms expected project paths and that CI is not coupled to the WorldGate mod:

```powershell
./scripts/Verify-MonitorLayout.ps1
```

## Cross-platform core tests

```powershell
dotnet test ./tools/WorldGate.Monitor/tests/WorldGate.Monitor.Core.Tests/WorldGate.Monitor.Core.Tests.csproj --configuration Release
```

## Windows shell compilation

WinForms project; on non-Windows hosts use Windows targeting (same as CI):

```powershell
dotnet build ./tools/WorldGate.Monitor/src/WorldGate.Monitor/WorldGate.Monitor.csproj --configuration Release -p:EnableWindowsTargeting=true
```

## Solution file

Open `wsl-manager.sln` at the repository root in Visual Studio or Rider for all Monitor projects.

## Install and acceptance (Windows)

See `tools/WorldGate.Monitor/README.md` for `install.ps1`, configuration, and tray acceptance (poll GitHub Actions / ARC via WSL).

## WSL Manager window

Left-click the tray icon (or choose **Open WSL Manager** from the menu) to edit `%USERPROFILE%\.wslconfig` with labeled fields for common `[wsl2]` settings. **Save .wslconfig** validates and writes atomically; you are prompted to run **Restart WSL** (`wsl --shutdown`) when settings need a full shutdown. **Restart WSL** is also available from the tray menu.

## CI

Pull requests that touch Monitor paths run [.github/workflows/monitor-verify.yml](../.github/workflows/monitor-verify.yml): layout verification, core tests, and Windows-targeting build. The workflow does not run on every push or on draft PR sync commits; use **Run workflow** for manual verification.
