# DeckSoundboard v0.1 test build

A Windows soundboard controller designed for the FIFINE AmpliGame D6 / HotSpot StreamDock host.

## What this test version does

- D6 button presses are received directly by a FIFINE/StreamDock plugin — no F11/F12/F13 keyboard shortcut is needed.
- Each D6 `Sound Clip` action can be assigned to any sound inside DeckSoundboard.
- Press once: DeckSoundboard holds your configured game PTT key, waits the lead-in delay, and plays the clip.
- Press the same button again: the clip stops and PTT releases immediately.
- Press another assigned sound: the current sound stops and the new one begins.
- Natural clip end: PTT releases after the configured tail delay.
- Emergency Stop always stops playback and releases PTT.
- Configuration is saved to `%APPDATA%\DeckSoundboard\settings.json`.
- Audio output can be set to a Wave Link playback device.
- App can start with Windows and minimize to the system tray.


## Easiest test path

On Windows, extract the ZIP and double-click **`BUILD_AND_INSTALL.bat`**. It checks for the .NET 8 SDK, offers to install it through Windows `winget` if needed, builds both EXEs, installs the app/plugin, and starts DeckSoundboard.

## Build

You need the **.NET 8 SDK** only to build the project. The published application is self-contained.

1. Right-click `Build-Windows.ps1` and run with PowerShell, or open PowerShell in this folder:

   `powershell -ExecutionPolicy Bypass -File .\Build-Windows.ps1`

2. The finished build is created under `dist\`.
3. Run:

   `powershell -ExecutionPolicy Bypass -File .\dist\Install-Windows.ps1`

## First-time setup

1. Start DeckSoundboard.
2. Open **Settings**.
3. Set your exact game's push-to-talk key, for example `V`.
4. Set **Audio output** to the Wave Link device you want the soundboard routed through.
5. Click **Save settings**.
6. Go to **Sounds** and click **Add sound**.
7. Open FIFINE Control Deck.
8. Find the **DeckSoundboard** category.
9. Drag **Sound Clip** onto a D6 key.
10. Press that physical D6 key once.
11. Return to DeckSoundboard -> **D6 Buttons**. The most recently pressed key is marked `last pressed`.
12. Select it, click **Assign sound**, and choose the clip.
13. Press the D6 key again to test.

Repeat steps 9-12 for more buttons.

## PTT key formats in v0.1

Supported examples:

- Letters/numbers: `V`, `T`, `5`
- Function keys: `F1` through `F24`
- Named keys: `Space`, `Tab`, `Enter`, `Shift`, `Ctrl`, `Alt`, `Escape`, `Insert`, `Delete`, arrows, etc.
- Mouse buttons: `Mouse1`, `Mouse2`, `Mouse3`, `Mouse4` / `XButton1`, `Mouse5` / `XButton2`

This first build expects a **single PTT key**. Multi-key PTT combinations can be added after the direct D6 path is verified on your machine.

## Saved settings

`%APPDATA%\DeckSoundboard\settings.json`

The sound files themselves are not copied. DeckSoundboard saves their file paths, so keep the audio files in a permanent folder.

## FIFINE plugin location

The installer copies the plugin to:

`%APPDATA%\HotSpot\StreamDock\plugins\com.nichol.decksoundboard.sdPlugin`

The D6 plugin talks only to the local DeckSoundboard process at:

`http://127.0.0.1:42761`

No network service is exposed outside your PC.

## Current v0.1 limitations

- This source package was generated in a Linux environment, so the Windows binaries could not be compiled/tested here.
- The first test focuses on MP3/WAV and formats supported by NAudio/Windows codecs.
- D6 keys show a generic `SOUND` label in this first build; dynamic clip titles/icons are a planned next step once the D6 communication is confirmed.
- Multi-key PTT combinations are not in the first test build.

## v0.1.3 changes
- PTT release is driven by the audio file's reported TotalTime, not PlaybackStopped.
- Optional PTT tail delay is added after the full clip duration.
- Same-button press still cancels the clip/timer and releases PTT immediately.
- Added Settings -> Enable push-to-talk checkbox for open-mic use.
- Clicking the main window X now exits instead of only hiding to the tray.
- Exit force-stops playback/releases PTT and terminates DeckSoundboard.D6Plugin helpers.
- Installer also stops DeckSoundboard.D6Plugin before updating.
- BUILD_AND_INSTALL exits cleanly after success so its command window does not keep the extracted folder open.
- Added CLEANUP_OLD_VERSION.bat for stopping lingering DeckSoundboard processes.
