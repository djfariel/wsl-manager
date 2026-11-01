$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\WorldGate.Monitor\WorldGate.Monitor.csproj'
$installDirectory = Join-Path $env:LOCALAPPDATA 'WorldGateMonitor'
$executable = Join-Path $installDirectory 'WorldGateMonitor.exe'
$startupDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
$shortcutPath = Join-Path $startupDirectory 'WorldGate Monitor.lnk'

Get-Process -Name 'WorldGateMonitor' -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $installDirectory

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Published monitor executable was not found at $executable"
}

$wshShell = New-Object -ComObject WScript.Shell
$shortcut = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $executable
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Description = 'WorldGate GitHub Actions / ARC runner monitor'
$shortcut.Save()

Start-Process -FilePath $executable
Write-Host "WorldGate Monitor installed to $installDirectory"
Write-Host "Startup shortcut: $shortcutPath"
