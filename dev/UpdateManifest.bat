@echo off
chcp 65001 >nul
cd /d "%~dp0"
title Update Manifest
echo.
echo Updating AD-Samples\ad-manifest.json ...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0UpdateManifest.ps1" %*
set ERR=%ERRORLEVEL%
echo.
if not "%ERR%"=="0" (
    echo [FAILED] Error code: %ERR%
    echo See error above. Log saved to update-manifest-error.log
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0UpdateManifest.ps1" %* 2>> "%~dp0update-manifest-error.log"
)
pause
exit /b %ERR%
