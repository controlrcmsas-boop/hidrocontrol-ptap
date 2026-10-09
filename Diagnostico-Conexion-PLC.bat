@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP - Diagnóstico de Comunicación PLC Siemens S7-1200
color 0b

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Diagnostico-Conexion-PLC.ps1"

echo.
pause
