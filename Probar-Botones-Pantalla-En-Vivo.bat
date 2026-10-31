@echo off
title HIDROCONTROL PTAP - Monitor Pantalla SEEKU
color 0b
echo ==============================================================================
echo       HIDROCONTROL PTAP - MONITOR EN VIVO DE PULSACIONES (SEEKU TACTIL)
echo ==============================================================================
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Monitor-Botones-Pantalla.ps1"
echo.
pause
