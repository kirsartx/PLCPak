@echo off
cd /d "%~dp0"
if exist "%~dp0PLCPak_v1.7.4.exe" (
    start "" "%~dp0PLCPak_v1.7.4.exe"
    exit /b 0
)
for %%F in ("%~dp0*.exe") do (
    start "" "%%~fF"
    exit /b 0
)
echo [ERROR] No .exe found in: %~dp0
pause
exit /b 1