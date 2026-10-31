@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP - Enlace Pantalla Física SEEKU
color 0a

echo ==============================================================================
echo       HIDROCONTROL PTAP - ENLACE PANTALLA FÍSICA SEEKU WSB7048R
echo ==============================================================================
echo.
echo [*] Conectando con la pantalla en protocolo Mitsubishi FX (D0..D2, M0..M2)...
echo [*] Detectando puerto USB-RS485 automaticamente (COM3 / COM4)...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Enlace-Pantalla-SEEKU-FX.ps1"

echo.
pause
