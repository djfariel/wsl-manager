# WorldGate Monitor

WorldGate Monitor is a Windows tray application for the repository's WSL2/K3s Actions Runner Controller (ARC) pool. It correlates GitHub Actions job state with the local `worldgate-linux` scale set so the Windows desktop can show whether CI is idle, queued, actively using an ephemeral runner, failed, or unavailable.

The monitor is read-only. GitHub and Kubernetes credentials remain inside WSL; the Windows process invokes the existing authenticated `gh` and `kubectl` commands through `wsl.exe`.

## Tray states

| State | Color | Meaning |
| --- | --- | --- |
| Healthy | Green | ARC is healthy and no monitored job is active; the latest monitored completion succeeded or none has completed yet. |
| Queued | Yellow | A GitHub job labeled `worldgate-linux` is waiting for a runner. |
| Running | Blue | A monitored job is in progress and/or an ARC runner pod is active. |
| Degraded | Red | ARC controller/listener health is bad, or the latest monitored completion failed/cancelled. |
| Unavailable | Gray | WSL, GitHub CLI, Kubernetes, configuration, or provider output is unavailable. |

Hover text is intentionally compact because Windows limits native `NotifyIcon` tooltip length. **Left-click** the tray icon to open the WSL Manager home window (`.wslconfig` editor). Right-click for CI status, **Open WSL Manager**, **Restart WSL**, and the rest of the tray menu.

## Notifications

Notifications are emitted through Windows' built-in tray notification API only on meaningful transitions and are deduplicated. Windows controls their presentation, retention, and suppression according to the current notification settings. The first successful poll establishes a baseline, so starting the monitor does not replay old workflow completions.

Typical notifications are:

```text
WorldGate CI started
PR #16 — Verify
Runner pod: worldgate-linux-xxxxx
```

```text
WorldGate CI passed
PR #16 — Verify
Duration: 4m 37s
```

```text
WorldGate CI failed
PR #16 — Verify
Failed job: Integration Tests
```

If a job remains queued for the configured warning threshold while ARC is reachable and no runner pod appears, one `WorldGate runner delayed` notification is emitted for that queued episode.

When more than one runner pod is active, the monitor reports the active runner count instead of guessing which pod belongs to the displayed GitHub job.

Use **Test Notification** from the tray menu to verify native notification delivery without waiting for a workflow transition.

## Requirements

- Windows 10 or Windows 11 with WSL2.
- .NET 8 SDK to run `install.ps1` (the installed application itself is self-contained).
- A WSL distribution containing an authenticated GitHub CLI (`gh`) with access to `djfariel/WorldGate`.
- `kubectl` inside that WSL distribution configured for the local K3s cluster.
- The WorldGate ARC deployment described in `infra/arc/README.md`.

Validate the WSL side before installing:

```powershell
wsl.exe -- gh auth status
wsl.exe -- gh api repos/djfariel/WorldGate/actions/runs?per_page=1
wsl.exe -- kubectl get pods -n arc-systems
wsl.exe -- kubectl get pods -n arc-runners
```

If ARC is installed in a non-default WSL distribution, set `wslDistribution` in the configuration described below.

## Install

From a Windows PowerShell session at the WorldGate repository root:

```powershell
.\tools\WorldGate.Monitor\install.ps1
```

The installer publishes a self-contained `win-x64` build to:

```text
%LOCALAPPDATA%\WorldGateMonitor
```

It also creates a per-user `WorldGate Monitor.lnk` in the Windows Startup folder and starts the monitor immediately. Administrator privileges are not required.

## Configuration

On first launch the monitor creates:

```text
%LOCALAPPDATA%\WorldGateMonitor\config.json
```

Default configuration:

```json
{
  "repository": "djfariel/WorldGate",
  "runnerScaleSetName": "worldgate-linux",
  "controllerNamespace": "arc-systems",
  "runnerNamespace": "arc-runners",
  "wslDistribution": null,
  "pollIntervalSeconds": 10,
  "queueWarningSeconds": 90
}
```

Use **Open Configuration** from the tray menu to edit the file in Notepad, then restart the monitor for changes to take effect. The configuration contains no credentials.

## Tray menu

Right-click the icon to see:

- resolved WorldGate state;
- current/recent PR, workflow and job;
- ARC controller/listener health;
- active runner pod or runner count;
- **Open GitHub Run**;
- **Refresh Now**;
- **Test Notification**;
- **Open Configuration**;
- **Exit**.

## Uninstall

From a Windows PowerShell session at the repository root:

```powershell
.\tools\WorldGate.Monitor\uninstall.ps1
```

This stops the monitor, removes its Startup shortcut, and removes `%LOCALAPPDATA%\WorldGateMonitor`. It does not modify WSL, GitHub authentication, K3s, Kubernetes credentials, or ARC.

## Troubleshooting

If the icon is gray, run the same commands the monitor depends on:

```powershell
wsl.exe -- gh auth status
wsl.exe -- gh api repos/djfariel/WorldGate/actions/runs?per_page=1
wsl.exe -- kubectl get pods -n arc-systems -o wide
wsl.exe -- kubectl get pods -n arc-runners -o wide
```

If those work only with `wsl.exe -d <distribution>`, put that distribution name in `wslDistribution`.

If the icon is red while GitHub is idle, inspect ARC directly:

```powershell
wsl.exe -- kubectl get pods -n arc-systems -o wide
wsl.exe -- kubectl get pods -n arc-runners -o wide
```

The application never parses the human-readable tables above; its actual polling uses Kubernetes JSON and GitHub API JSON.

## CI isolation

The monitor is not part of the Terraria mod's `Verify` action and is not built or tested by the ARC infrastructure workflow. Its automated checks live in the separate `.github/workflows/monitor-verify.yml` workflow.

`Monitor Verify` does **not** run on ordinary pushes or PR synchronization commits. It runs automatically only when a relevant PR is opened, reopened, or marked ready for review, and it can also be started manually with `workflow_dispatch`.

`Monitor Verify` currently uses the available `worldgate-linux` runner pool because GitHub-hosted jobs for this repository fail during runner admission. That is only a runner allocation choice: the workflow itself remains monitor-only and does not build or run the mod.

`Monitor Verify` runs only the monitor project-isolation contract, the platform-neutral monitor core tests, and the Windows-targeting monitor build. It does not build `WorldGate.csproj`, run the mod test suite, start tModLoader, or execute world-generation acceptance checks.

The mod's `Verify` workflow is something the installed monitor observes; it is not part of the monitor's own CI workflow.

## Development verification

Project/CI isolation contract:

```powershell
.\scripts\Test-MonitorProjectIsolation.ps1
```

Cross-platform core tests:

```powershell
dotnet test .\tools\WorldGate.Monitor\tests\WorldGate.Monitor.Core.Tests\WorldGate.Monitor.Core.Tests.csproj --configuration Release
```

Windows shell compilation:

```powershell
dotnet build .\tools\WorldGate.Monitor\src\WorldGate.Monitor\WorldGate.Monitor.csproj --configuration Release -p:EnableWindowsTargeting=true
```

Graphical acceptance remains Windows-side: install the application, trigger a mod `Verify` workflow as an observed workload, observe yellow -> blue -> green/red transitions, verify the transition notifications, and confirm WSL/K3s outages degrade the tray without terminating the process.
