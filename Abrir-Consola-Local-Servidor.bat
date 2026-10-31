@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP - CONSOLA LOCAL DE PLANTA (RESPALDO OFFLINE)
color 0b

echo ==============================================================================
echo    HIDROCONTROL PTAP - CONSOLA LOCAL DE EMERGENCIA EN SERVIDOR
echo ==============================================================================
echo.
echo [*] Abriendo Consola Local en modo Aplicacion de Escritorio (100%% Offline)...

start msedge.exe --app="http://localhost:1882/panel" --window-size=1250,820
if errorlevel 1 (
    start chrome.exe --app="http://localhost:1882/panel" --window-size=1250,820
    if errorlevel 1 (
        start http://localhost:1882/panel
    )
)
