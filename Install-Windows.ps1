$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceApp = Join-Path $Here 'DeckSoundboard'
$SourcePlugin = Join-Path $Here 'com.nichol.decksoundboard.sdPlugin'
$AppDest = Join-Path $env:LOCALAPPDATA 'DeckSoundboard'
$PluginRoot = Join-Path $env:APPDATA 'HotSpot\StreamDock\plugins'
$PluginDest = Join-Path $PluginRoot 'com.nichol.decksoundboard.sdPlugin'

if (-not (Test-Path (Join-Path $SourceApp 'DeckSoundboard.exe'))) { throw 'DeckSoundboard.exe was not found next to this installer.' }
if (-not (Test-Path (Join-Path $SourcePlugin 'manifest.json'))) { throw 'The FIFINE plugin folder was not found next to this installer.' }

# Capture the exact Control Deck executable before closing it so we can relaunch
# the same installation instead of relying only on guessed install paths.
$FifineExe = $null
$FifineProcesses = @()
$FifineProcesses += @(Get-Process StreamDock -ErrorAction SilentlyContinue)
$FifineProcesses += @(Get-Process 'fifine Control Deck' -ErrorAction SilentlyContinue)

foreach ($proc in $FifineProcesses) {
    if ($proc -and -not $FifineExe) {
        try {
            if ($proc.Path -and (Test-Path $proc.Path)) {
                $FifineExe = $proc.Path
            }
        } catch { }
    }
}

# Get-Process.Path can occasionally be unavailable, so use Win32_Process as a fallback.
if (-not $FifineExe) {
    try {
        $wmiProc = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ieq 'StreamDock.exe' -or $_.Name -ieq 'fifine Control Deck.exe' } |
            Select-Object -First 1
        if ($wmiProc -and $wmiProc.ExecutablePath -and (Test-Path $wmiProc.ExecutablePath)) {
            $FifineExe = $wmiProc.ExecutablePath
        }
    } catch { }
}

Write-Host 'Stopping DeckSoundboard / FIFINE Control Deck if running...'
Get-Process DeckSoundboard -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process 'DeckSoundboard.D6Plugin' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process StreamDock -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process 'fifine Control Deck' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 1000

New-Item $AppDest -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $SourceApp '*') $AppDest -Recurse -Force

New-Item $PluginRoot -ItemType Directory -Force | Out-Null
Remove-Item $PluginDest -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item $SourcePlugin $PluginDest -Recurse -Force

$Cache = Join-Path $env:APPDATA 'HotSpot\StreamDock\cache'
if (Test-Path $Cache) { Get-ChildItem $Cache -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host 'Starting DeckSoundboard...'
Start-Process (Join-Path $AppDest 'DeckSoundboard.exe')
Start-Sleep -Milliseconds 500

$Started = $false
if ($FifineExe -and (Test-Path $FifineExe)) {
    Write-Host "Restarting FIFINE Control Deck from: $FifineExe"
    try {
        Start-Process $FifineExe
        $Started = $true
    } catch { }
}

# Fallbacks for installing while Control Deck was not running, or when Windows did
# not expose its executable path to the installer.
if (-not $Started) {
    $Candidates = @(
      (Join-Path $env:LOCALAPPDATA 'Programs\fifine Control Deck\fifine Control Deck.exe'),
      (Join-Path $env:LOCALAPPDATA 'Programs\FIFINE Control Deck\FIFINE Control Deck.exe'),
      (Join-Path $env:LOCALAPPDATA 'Programs\StreamDock\StreamDock.exe'),
      (Join-Path $env:PROGRAMFILES 'fifine Control Deck\fifine Control Deck.exe'),
      (Join-Path $env:PROGRAMFILES 'FIFINE Control Deck\FIFINE Control Deck.exe'),
      (Join-Path ${env:PROGRAMFILES(X86)} 'fifine Control Deck\fifine Control Deck.exe'),
      (Join-Path ${env:PROGRAMFILES(X86)} 'FIFINE Control Deck\FIFINE Control Deck.exe')
    )
    foreach ($exe in $Candidates) {
        if ($exe -and (Test-Path $exe)) {
            Write-Host "Restarting FIFINE Control Deck from: $exe"
            Start-Process $exe
            $Started = $true
            break
        }
    }
}

# Last-resort: resolve a FIFINE / StreamDock Start Menu shortcut.
if (-not $Started) {
    try {
        $ShortcutRoots = @(
            (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'),
            (Join-Path $env:PROGRAMDATA 'Microsoft\Windows\Start Menu\Programs')
        )
        $shell = New-Object -ComObject WScript.Shell
        foreach ($root in $ShortcutRoots) {
            if (-not (Test-Path $root)) { continue }
            $lnk = Get-ChildItem $root -Filter '*.lnk' -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match 'FIFINE|Control Deck|StreamDock' } |
                Select-Object -First 1
            if ($lnk) {
                $shortcut = $shell.CreateShortcut($lnk.FullName)
                if ($shortcut.TargetPath -and (Test-Path $shortcut.TargetPath)) {
                    Write-Host "Restarting FIFINE Control Deck from shortcut: $($shortcut.TargetPath)"
                    Start-Process $shortcut.TargetPath
                    $Started = $true
                    break
                }
            }
        }
    } catch { }
}

Write-Host ''
Write-Host 'Installed.'
Write-Host "App:    $AppDest"
Write-Host "Plugin: $PluginDest"
if (-not $Started) { Write-Host 'Please open FIFINE Control Deck manually.' }
Write-Host ''
Write-Host "In FIFINE Control Deck, look for the 'DeckSoundboard' category and drag 'Sound Clip' onto a D6 key."
