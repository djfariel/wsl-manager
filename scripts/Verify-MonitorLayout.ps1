$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$monitorRoot = Join-Path $repoRoot 'tools/WorldGate.Monitor'
$workflowPath = Join-Path $repoRoot '.github/workflows/monitor-verify.yml'

$requiredProjects = @(
    'src/WorldGate.Monitor.Core/WorldGate.Monitor.Core.csproj',
    'src/WorldGate.Monitor/WorldGate.Monitor.csproj',
    'tests/WorldGate.Monitor.Core.Tests/WorldGate.Monitor.Core.Tests.csproj'
)

foreach ($relative in $requiredProjects) {
    $path = Join-Path $monitorRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required monitor project is missing: $path"
    }
}

if (-not (Test-Path -LiteralPath $workflowPath -PathType Leaf)) {
    throw "Monitor Verify workflow is missing: $workflowPath"
}

$workflow = Get-Content -LiteralPath $workflowPath -Raw

foreach ($required in @(
    'name: Monitor Verify',
    'WorldGate.Monitor.Core.Tests.csproj',
    'WorldGate.Monitor.csproj',
    'EnableWindowsTargeting=true',
    'Verify-MonitorLayout.ps1'
)) {
    if ($workflow -notmatch [regex]::Escape($required)) {
        throw "Monitor Verify workflow is missing required contract '$required'."
    }
}

foreach ($forbidden in @(
    'WorldGate.csproj',
    'Verify-HeadlessLifecycle.ps1',
    'WorldGateAcceptance=true',
    'Test-MonitorProjectIsolation.ps1'
)) {
    if ($workflow -match [regex]::Escape($forbidden)) {
        throw "Monitor Verify must not reference WorldGate mod coupling ('$forbidden')."
    }
}

Write-Host 'wsl-manager monitor layout and CI contract passed.'
