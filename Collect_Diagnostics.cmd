@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect_Diagnostics.ps1"
set "RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %RESULT%
