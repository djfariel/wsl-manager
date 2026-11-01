<#
.SYNOPSIS
  Build and launch WorldGate.Monitor (WSL Manager tray app) for local dev iteration.

.DESCRIPTION
  Standard post-change launch path: after review-ready UI or tray work, run this script
  from the repository root (stop any existing WorldGateMonitor tray first to avoid duplicates).

  Runs via $PSScriptRoot. Uses `dotnet run` on the WinForms project with Release +
  EnableWindowsTargeting so you get a fast rebuild-and-run loop without a separate publish
  step. The tray app starts in a new process and this script exits.

.PARAMETER NoBuild
  Skip `dotnet build` and launch immediately (still uses `dotnet run`, which builds if needed
  unless you combine with a prior build — for a true skip, we only skip the explicit build step).

.EXAMPLE
  ./scripts/Run-WslManagerDev.ps1

.EXAMPLE
  ./scripts/Run-WslManagerDev.ps1 -NoBuild
#>
[CmdletBinding()]
param(
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tools/WorldGate.Monitor/src/WorldGate.Monitor/WorldGate.Monitor.csproj'

if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Monitor project not found: $project"
}

$dotnetArgs = @(
    'run',
    '--project', $project,
    '--configuration', 'Release',
    '-p:EnableWindowsTargeting=true',
    '--no-launch-profile'
)

if ($NoBuild) {
    $dotnetArgs += '--no-build'
}
else {
    Write-Host 'Building WorldGate.Monitor (Release, EnableWindowsTargeting)...'
    dotnet build $project --configuration Release -p:EnableWindowsTargeting=true
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

Write-Host 'Starting WSL Manager tray app (new window)...'
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = 'dotnet'
$startInfo.Arguments = ($dotnetArgs -join ' ')
$startInfo.WorkingDirectory = $repoRoot
$startInfo.UseShellExecute = $true
[System.Diagnostics.Process]::Start($startInfo) | Out-Null

Write-Host 'Launch requested. Look for the WorldGateMonitor tray icon.'
