@echo off
cd /d "%~dp0"
if exist "%~dp0PLCPak_v1.8.0.exe" (
    start "" "%~dp0PLCPak_v1.8.0.exe"
    exit /b 0
)
if exist "%~dp0PLCPak.ps1" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0PLCPak.ps1"
    exit /b %ERRORLEVEL%
)
for %%F in ("%~dp0PLCPak*.exe") do (
    start "" "%%~fF"
    exit /b 0
)
echo [ERROR] No PLCPak executable found in: %~dp0
pause
exit /b 1