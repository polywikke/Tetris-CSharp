@echo off
rem One-click launcher for the Tetris course project.
rem Double-click this file, or run it from a terminal.
chcp 65001 >nul
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*

if errorlevel 1 (
    echo.
    echo Startup failed. Read the message above.
    pause
)

endlocal
