@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP V2.2 - Inicio Integral Produccion (Local + Internet)
color 0b

echo ==============================================================================
echo       HIDROCONTROL PTAP V2.2 - INICIO COMPLETO PLANTA CON TUNEL CLOUDFLARE
echo ==============================================================================
echo.

set "PATH=%APPDATA%\npm;C:\Users\luchi\AppData\Roaming\npm;C:\Users\Sandra\AppData\Roaming\npm;%ProgramFiles%\nodejs;%PATH%"

echo [*] 1/6 Iniciando motor de adquisicion Node-RED (Puerto 1880)...
if exist "%APPDATA%\npm\node-red.cmd" (
    start "1. Node-RED (PLC Siemens)" cmd /k "title Node-RED && \"%APPDATA%\npm\node-red.cmd\""
) else if exist "C:\Users\luchi\AppData\Roaming\npm\node-red.cmd" (
    start "1. Node-RED (PLC Siemens)" cmd /k "title Node-RED && \"C:\Users\luchi\AppData\Roaming\npm\node-red.cmd\""
) else (
    start "1. Node-RED (PLC Siemens)" cmd /k "title Node-RED && node-red.cmd || npx node-red"
)
timeout /t 4 /nobreak >nul

echo [*] 2/6 Iniciando Agente Edge Local + Store y Forward SQLite (Puerto 1882)...
start "2. Agente Edge MO" cmd /k "cd /d %~dp0services\edge-agent && node edge-agent.js"
timeout /t 2 /nobreak >nul

echo [*] 3/6 Iniciando Pasarela de Alertas WhatsApp (Puerto 5089)...
start "3. WhatsApp Gateway" cmd /k "cd /d %~dp0services\whatsapp-gateway && node server.js"
timeout /t 2 /nobreak >nul

echo [*] 4/6 Iniciando Servidor SCADA Blazor (Puerto 5088)...
start "4. Dashboard SCADA" cmd /k "cd /d %~dp0dashboard\PTAPControl && dotnet run --urls http://0.0.0.0:5088"
timeout /t 4 /nobreak >nul

echo [*] 5/6 Conectando Tunel Seguro Cloudflare a Internet...
set CONFIG_FILE=%~dp0tools\tunnel-token.txt
if exist "%CONFIG_FILE%" (
    set /p TOKEN=<"%CONFIG_FILE%"
    start "5. Tunel Cloudflare Edge" cmd /k "title Cloudflare Tunnel && "%~dp0tools\cloudflared.exe" tunnel run --token %TOKEN%"
) else (
    echo [AVISO] No se encontro token en tools\tunnel-token.txt.
)

timeout /t 2 /nobreak >nul
start http://localhost:5088

echo [*] 6/6 Conectando Pantalla Fisica SEEKU por RS-485 (Mitsubishi FX)...
start "Pantalla SEEKU HMI" powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Enlace-Pantalla-SEEKU-FX.ps1"
timeout /t 2 /nobreak >nul

echo [*] Abriendo Consola Local de Respaldo en Servidor (100%% Offline)...
start msedge.exe --app="http://localhost:1882/panel" --window-size=1250,820 || start chrome.exe --app="http://localhost:1882/panel" --window-size=1250,820 || start http://localhost:1882/panel

echo.
echo ==============================================================================
echo  [+] SISTEMA COMPLETO OPERANDO:
echo      - Node-RED (PLC S7-1200) : http://localhost:1880
echo      - Consola Local Servidor : http://localhost:1882/panel (Sin internet ni login)
echo      - WhatsApp Gateway       : http://localhost:5089/qr
echo      - Dashboard Local SCADA  : http://localhost:5088
echo      - Pantalla Fisica SEEKU  : Transmitiendo en vivo por USB-RS485
echo      - Dominio en Internet    : https://hidrocontrol.potenzia.com
echo ==============================================================================
echo.
pause
