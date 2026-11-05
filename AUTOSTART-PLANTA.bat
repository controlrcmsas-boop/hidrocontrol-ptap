@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP V2.2 - ARRANQUE AUTOMATICO INTELIGENTE
color 0b

echo ==============================================================================
echo       HIDROCONTROL PTAP V2.2 - ARRANQUE TOTAL DE PLANTA (AUTO-SENSADO)
echo ==============================================================================
echo.

set "PATH=%APPDATA%\npm;C:\Users\luchi\AppData\Roaming\npm;C:\Users\Sandra\AppData\Roaming\npm;%ProgramFiles%\nodejs;%PATH%"

echo [*] Ajustando prioridades de red (Wi-Fi Internet / Ethernet PLC)...
powershell.exe -NoProfile -Command "try { Set-NetIPInterface -InterfaceAlias 'Wi-Fi' -InterfaceMetric 10 -ErrorAction SilentlyContinue; Set-NetIPInterface -InterfaceAlias 'Ethernet' -InterfaceMetric 50 -ErrorAction SilentlyContinue } catch {}" >nul 2>&1

echo [*] Verificando conectividad a Internet...
ping -n 1 -w 1500 8.8.8.8 >nul 2>&1
if %errorlevel% equ 0 (
    echo  [+] ESTADO DE RED: ONLINE (Internet disponible)
    set MODO_RED=ONLINE
) else (
    echo  [!] ESTADO DE RED: OFFLINE (Modo Respaldo Local Autónomo en Servidor)
    set MODO_RED=OFFLINE
)
echo.

echo [*] 1/6 Iniciando motor de adquisicion Node-RED (PLC Siemens S7-1200 en 1880)...
if exist "%APPDATA%\npm\node-red.cmd" (
    start "1. Node-RED (PLC Siemens)" cmd /k "title Node-RED && \"%APPDATA%\npm\node-red.cmd\""
) else if exist "C:\Users\luchi\AppData\Roaming\npm\node-red.cmd" (
    start "1. Node-RED (PLC Siemens)" cmd /k "title Node-RED && \"C:\Users\luchi\AppData\Roaming\npm\node-red.cmd\""
) else (
    start "1. Node-RED (PLC Siemens)" cmd /k "title Node-RED && node-red.cmd || npx node-red"
)
timeout /t 4 /nobreak >nul

echo [*] 2/6 Iniciando Agente Edge MO Local + SQLite (Puerto 1882)...
start "2. Agente Edge MO" cmd /k "title Agente Edge MO && cd /d %~dp0services\edge-agent && node edge-agent.js"
timeout /t 2 /nobreak >nul

echo [*] 3/6 Iniciando Pasarela de Alertas WhatsApp (Puerto 5089)...
start "3. WhatsApp Gateway" cmd /k "title WhatsApp Gateway && cd /d %~dp0services\whatsapp-gateway && node server.js"
timeout /t 2 /nobreak >nul

echo [*] 4/6 Iniciando Servidor SCADA Blazor (Puerto 5088)...
start "4. Dashboard SCADA" cmd /k "title Dashboard SCADA && cd /d %~dp0dashboard\PTAPControl && dotnet run --urls http://0.0.0.0:5088"
timeout /t 3 /nobreak >nul

echo [*] 5/6 Conectando Pantalla Fisica SEEKU por RS-485 (Mitsubishi FX)...
start "5. Pantalla SEEKU HMI" powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Enlace-Pantalla-SEEKU-FX.ps1"
timeout /t 2 /nobreak >nul

if "%MODO_RED%"=="ONLINE" (
    echo [*] 6/6 Conectando Tunel Seguro Cloudflare a Internet...
    set CONFIG_FILE=%~dp0tools\tunnel-token.txt
    if exist "%CONFIG_FILE%" (
        set /p TOKEN=<"%CONFIG_FILE%"
        start "6. Tunel Cloudflare Edge" cmd /k "title Cloudflare Tunnel && "%~dp0tools\cloudflared.exe" tunnel run --token %TOKEN%"
    )
) else (
    echo [*] 6/6 Tunel Cloudflare omitido por modo offline local.
)

timeout /t 2 /nobreak >nul
echo [*] Verificando enlace de WhatsApp (Puerto 5089)...
powershell.exe -NoProfile -Command "try { Start-Sleep -Seconds 2; $s = Invoke-RestMethod -Uri 'http://localhost:5089/status' -TimeoutSec 3; if (-not $s.connected) { Write-Host '  [!] WhatsApp requiere vinculacion. Abriendo codigo QR...' -ForegroundColor Yellow; Start-Process 'http://localhost:5089/qr' } else { Write-Host ('  [+] WhatsApp CONECTADO: ' + $s.user) -ForegroundColor Green } } catch {}"

echo [*] Abriendo Consola Local de Operacion (http://localhost:1882/panel)...
start msedge.exe --app="http://localhost:1882/panel" --window-size=1250,820 || start chrome.exe --app="http://localhost:1882/panel" --window-size=1250,820 || start http://localhost:1882/panel

echo.
echo ==============================================================================
echo  [+] SISTEMA TOTALMENTE OPERATIVO (%MODO_RED%)
echo      - Node-RED (PLC S7-1200) : http://localhost:1880
echo      - Consola Local Servidor : http://localhost:1882/panel
echo      - Dashboard SCADA        : http://localhost:5088
echo      - Pantalla SEEKU HMI     : Enlace RS-485 Activo
if "%MODO_RED%"=="ONLINE" (
echo      - Tunel Cloudflare       : Activo hacia la Nube
)
echo ==============================================================================
echo.

