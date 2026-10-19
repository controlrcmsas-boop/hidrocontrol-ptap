@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP - Diagn?stico de Comunicaci?n PLC Siemens S7-1200
color 0b

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Diagnostico-Conexion-PLC.ps1"

echo.
pause
