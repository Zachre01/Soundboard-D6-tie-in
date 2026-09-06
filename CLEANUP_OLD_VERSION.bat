@echo off
setlocal
echo Stopping DeckSoundboard processes...
taskkill /F /IM DeckSoundboard.exe >nul 2>nul
taskkill /F /IM DeckSoundboard.D6Plugin.exe >nul 2>nul

echo.
echo DeckSoundboard processes have been stopped.
echo If Windows still says an OLD extracted folder is open, close any old
 echo BUILD_AND_INSTALL command windows that are still sitting on "Press any key".
echo.
cd /d "%TEMP%"
timeout /t 2 /nobreak >nul
exit /b 0
