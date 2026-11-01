<#
.SYNOPSIS
  Stop the WSL Manager tray app and run Run-WslManagerDev.ps1 (build + launch).

.DESCRIPTION
  Stops WorldGateMonitor.exe if running, then delegates to Run-WslManagerDev.ps1.
  Pass through -NoBuild when a recent Release build is already on disk.

.EXAMPLE
  ./scripts/Restart-WslManagerDev.ps1

.EXAMPLE
  ./scripts/Restart-WslManagerDev.ps1 -NoBuild
#>
[CmdletBinding()]
param(
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Get-Process -Name WorldGateMonitor -ErrorAction SilentlyContinue | Stop-Process -Force

$params = @{}
if ($NoBuild) {
    $params['NoBuild'] = $true
}

& (Join-Path $PSScriptRoot 'Run-WslManagerDev.ps1') @params
