$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceApp = Join-Path $Here 'DeckSoundboard'
$SourcePlugin = Join-Path $Here 'com.nichol.decksoundboard.sdPlugin'
$AppDest = Join-Path $env:LOCALAPPDATA 'DeckSoundboard'
$PluginRoot = Join-Path $env:APPDATA 'HotSpot\StreamDock\plugins'
$PluginDest = Join-Path $PluginRoot 'com.nichol.decksoundboard.sdPlugin'

if (-not (Test-Path (Join-Path $SourceApp 'DeckSoundboard.exe'))) { throw 'DeckSoundboard.exe was not found next to this installer.' }
if (-not (Test-Path (Join-Path $SourcePlugin 'manifest.json'))) { throw 'The FIFINE plugin folder was not found next to this installer.' }

Write-Host 'Stopping DeckSoundboard / FIFINE Control Deck if running...'
Get-Process DeckSoundboard -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process 'DeckSoundboard.D6Plugin' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process StreamDock -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process 'fifine Control Deck' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800

New-Item $AppDest -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $SourceApp '*') $AppDest -Recurse -Force

New-Item $PluginRoot -ItemType Directory -Force | Out-Null
Remove-Item $PluginDest -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item $SourcePlugin $PluginDest -Recurse -Force

$Cache = Join-Path $env:APPDATA 'HotSpot\StreamDock\cache'
if (Test-Path $Cache) { Get-ChildItem $Cache -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host 'Starting DeckSoundboard...'
Start-Process (Join-Path $AppDest 'DeckSoundboard.exe')

$Candidates = @(
  (Join-Path $env:LOCALAPPDATA 'Programs\fifine Control Deck\fifine Control Deck.exe'),
  (Join-Path $env:LOCALAPPDATA 'Programs\StreamDock\StreamDock.exe'),
  (Join-Path $env:PROGRAMFILES 'fifine Control Deck\fifine Control Deck.exe')
)
$Started = $false
foreach ($exe in $Candidates) { if (Test-Path $exe) { Start-Process $exe; $Started = $true; break } }

Write-Host ''
Write-Host 'Installed.'
Write-Host "App:    $AppDest"
Write-Host "Plugin: $PluginDest"
if (-not $Started) { Write-Host 'Please open FIFINE Control Deck manually.' }
Write-Host ''
Write-Host "In FIFINE Control Deck, look for the 'DeckSoundboard' category and drag 'Sound Clip' onto a D6 key."
