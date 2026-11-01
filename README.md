# wsl-manager

Dedicated repository for Windows/WSL tooling extracted from [WorldGate](https://github.com/djfariel/WorldGate).

**Phase 1** imports **WorldGate.Monitor** (the tray app historically referred to as "WorldGate Manager"): a read-only Windows system-tray monitor that polls GitHub Actions and Kubernetes inside WSL for the ARC runner pool.

| Path | Role |
| --- | --- |
| `tools/WorldGate.Monitor/` | Monitor install scripts, WinForms shell, and platform-neutral core |

See `tools/WorldGate.Monitor/README.md` for install, configuration, and troubleshooting.

## Development

Build, test, layout verification, and CI notes: [docs/developing.md](docs/developing.md).

## Migration

Product rename and scope expansion (generic WSL manager, optional `infra/arc` recipes) are tracked in the wsl-manager project plan. WorldGate continues to host Monitor until this repo is canonical.
