@echo off
chcp 65001 >nul
title Desinstalar Auto-Arranque - HIDROCONTROL PTAP
color 0e

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

schtasks /delete /tn "HidrocontrolServidorPlanta" /f >nul 2>&1
echo Tarea de auto-arranque eliminada correctamente.
pause
