@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-OldVersionMigration.ps1"
set "taskExitCode=%ERRORLEVEL%"
if not "%taskExitCode%"=="0" echo Migration failed. Read the error above before starting MateEngine.
echo.
pause
endlocal & exit /b %taskExitCode%
