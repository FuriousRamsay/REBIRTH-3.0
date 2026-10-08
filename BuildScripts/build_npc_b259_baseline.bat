@echo off
setlocal
set SCRIPT_DIR=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%NPC\Invoke-RebirthNpcB259Baseline.ps1" %*
exit /b %ERRORLEVEL%
