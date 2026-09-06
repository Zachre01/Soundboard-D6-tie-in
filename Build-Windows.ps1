$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Dist = Join-Path $Root 'dist'
$AppOut = Join-Path $Dist 'DeckSoundboard'
$PluginOut = Join-Path $Dist 'com.nichol.decksoundboard.sdPlugin'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 6 SDK is required to build. Install the .NET 6 SDK, then run this script again.'
}

Remove-Item $Dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item $AppOut -ItemType Directory -Force | Out-Null
New-Item (Join-Path $PluginOut 'plugin') -ItemType Directory -Force | Out-Null

Write-Host 'Publishing DeckSoundboard app...'
dotnet publish (Join-Path $Root 'src\DeckSoundboard\DeckSoundboard.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $AppOut

Write-Host 'Publishing D6 plugin...'
dotnet publish (Join-Path $Root 'src\DeckSoundboard.D6Plugin\DeckSoundboard.D6Plugin.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -o (Join-Path $PluginOut 'plugin')

Copy-Item (Join-Path $Root 'plugin\com.nichol.decksoundboard.sdPlugin\manifest.json') $PluginOut
Copy-Item (Join-Path $Root 'plugin\com.nichol.decksoundboard.sdPlugin\imgs') $PluginOut -Recurse
Copy-Item (Join-Path $Root 'Install-Windows.ps1') $Dist
Copy-Item (Join-Path $Root 'README.md') $Dist
Copy-Item (Join-Path $Root 'CLEANUP_OLD_VERSION.bat') $Dist

Write-Host ''
Write-Host "Build complete: $Dist"
Write-Host 'Run dist\Install-Windows.ps1 to install the app and FIFINE plugin.'
