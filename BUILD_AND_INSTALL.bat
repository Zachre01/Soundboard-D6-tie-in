@echo off
setlocal
cd /d "%~dp0"
echo =====================================
echo      DeckSoundboard Test Setup
echo =====================================
echo.
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
  echo .NET 6 SDK was not found.
  where winget >nul 2>nul
  if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Windows Package Manager ^(winget^) was not found either.
    echo Install the Microsoft .NET 6 SDK, then run this file again.
    pause
    exit /b 1
  )
  echo Installing Microsoft .NET 6 SDK with winget...
  winget install --id Microsoft.DotNet.SDK.6 --exact --accept-package-agreements --accept-source-agreements
  if %ERRORLEVEL% NEQ 0 (
    echo .NET SDK installation failed.
    pause
    exit /b 1
  )
  echo.
  echo The SDK was installed. If dotnet is not immediately available,
  echo close this window and run BUILD_AND_INSTALL.bat again.
  where dotnet >nul 2>nul
  if %ERRORLEVEL% NEQ 0 (
    pause
    exit /b 0
  )
)

echo Building Windows application and D6 plugin...
powershell -NoProfile -ExecutionPolicy Bypass -File ".\Build-Windows.ps1"
if %ERRORLEVEL% NEQ 0 (
  echo.
  echo BUILD FAILED. Copy the error shown above back to ChatGPT.
  pause
  exit /b 1
)

echo.
echo Installing DeckSoundboard and FIFINE plugin...
powershell -NoProfile -ExecutionPolicy Bypass -File ".\dist\Install-Windows.ps1"
if %ERRORLEVEL% NEQ 0 (
  echo.
  echo INSTALL FAILED. Copy the error shown above back to ChatGPT.
  pause
  exit /b 1
)

echo.
echo Finished. DeckSoundboard should now be running.
echo Open FIFINE Control Deck and look for the DeckSoundboard category.
echo.
echo Closing this setup window so this extracted folder is not kept open.
cd /d "%TEMP%"
exit /b 0
