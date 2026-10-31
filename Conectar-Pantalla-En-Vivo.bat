@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP - Enlace en Vivo Pantalla SEEKU
color 0b

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Puente-Pantalla-SEEKU-COM4.ps1" -PortName COM4 -BaudRate 9600

pause
