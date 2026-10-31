@echo off
chcp 65001 >nul
title Iniciar HIDROCONTROL PTAP V2.2 (Modo Local Offline de Planta)
echo ==============================================================================
echo        HIDROCONTROL PTAP V2.2 - INICIO COMPLETO OFFLINE EN PLANTA
echo ==============================================================================
echo.

set "PATH=%APPDATA%\npm;C:\Users\luchi\AppData\Roaming\npm;C:\Users\Sandra\AppData\Roaming\npm;%ProgramFiles%\nodejs;%PATH%"

echo [*] 1/5 Iniciando motor de adquisicion Node-RED (PLC Siemens S7-1200)...
if exist "%APPDATA%\npm\node-red.cmd" (
    start "1. Node-RED (PLC Siemens)" cmd /k "title Node-RED && \"%APPDATA%\npm\node-red.cmd\""
) else if exist "C:\Users\luchi\AppData\Roaming\npm\node-red.cmd" (
    start "1. Node-RED (PLC Siemens)" cmd /k "title Node-RED && \"C:\Users\luchi\AppData\Roaming\npm\node-red.cmd\""
) else (
    start "1. Node-RED (PLC Siemens)" cmd /k "title Node-RED && node-red.cmd || npx node-red"
)
timeout /t 4 /nobreak >nul

echo [*] 2/5 Iniciando Agente Edge Local + Store y Forward SQLite (Puerto 1882)...
start "2. Agente Edge MO" cmd /k "cd /d %~dp0services\edge-agent && node edge-agent.js"
timeout /t 2 /nobreak >nul

echo [*] 3/5 Iniciando Pasarela de Alertas WhatsApp (Puerto 5089)...
start "3. WhatsApp Gateway" cmd /k "cd /d %~dp0services\whatsapp-gateway && node server.js"
timeout /t 2 /nobreak >nul

echo [*] 4/5 Iniciando Servidor SCADA Blazor (Puerto 5088)...
start "4. Dashboard SCADA" cmd /k "cd /d %~dp0dashboard\PTAPControl && dotnet run --urls http://0.0.0.0:5088"
timeout /t 4 /nobreak >nul

echo [*] 5/5 Conectando Pantalla Fisica SEEKU por RS-485 (Mitsubishi FX)...
start "Pantalla SEEKU HMI" powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Enlace-Pantalla-SEEKU-FX.ps1"
timeout /t 2 /nobreak >nul

echo [*] Abriendo Consola Local de Servidor (100%% Offline)...
start msedge.exe --app="http://localhost:1882/panel" --window-size=1250,820 || start chrome.exe --app="http://localhost:1882/panel" --window-size=1250,820 || start http://localhost:1882/panel

echo.
echo ==============================================================================
echo  [+] SISTEMA OFFLINE OPERANDO:
echo      - Node-RED (PLC S7-1200) : http://localhost:1880
echo      - Consola Local Servidor : http://localhost:1882/panel (Sin internet ni login)
echo      - Dashboard SCADA        : http://localhost:5088
echo      - Pantalla Fisica SEEKU  : RS-485 enlazada
echo ==============================================================================
echo.
pause
