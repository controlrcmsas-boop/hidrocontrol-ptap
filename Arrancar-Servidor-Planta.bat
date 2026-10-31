@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP - Servidor Edge de Planta
color 0b

echo ==============================================================================
echo        HIDROCONTROL PTAP - ARRANQUE DEL SERVIDOR EDGE DE PLANTA
echo ==============================================================================
echo.

echo [*] 1/3 Verificando enlace seguro Tailscale (Puerto 1880)...
tailscale funnel --bg 1880 >nul 2>&1

echo [*] 2/3 Iniciando motor de adquisicion Node-RED...
set "PATH=%APPDATA%\npm;C:\Users\Sandra\AppData\Roaming\npm;%ProgramFiles%\nodejs;%PATH%"
if exist "%APPDATA%\npm\node-red.cmd" (
    start "Node-RED (PLC Siemens)" cmd /k "title Node-RED && \"%APPDATA%\npm\node-red.cmd\""
) else if exist "C:\Users\Sandra\AppData\Roaming\npm\node-red.cmd" (
    start "Node-RED (PLC Siemens)" cmd /k "title Node-RED && \"C:\Users\Sandra\AppData\Roaming\npm\node-red.cmd\""
) else (
    start "Node-RED (PLC Siemens)" cmd /k "title Node-RED && node-red.cmd || npx node-red"
)

timeout /t 4 /nobreak >nul

echo [*] 3/3 Iniciando Agente Local SQLite + Watchdog Offline...
node "%~dp0services\edge-agent\edge-agent.js"

pause
