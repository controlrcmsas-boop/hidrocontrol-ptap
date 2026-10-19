@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP V2.2 - Inicio Integral Producci?n (Local + Internet)
color 0b

echo ==============================================================================
echo       HIDROCONTROL PTAP V2.2 - INICIO COMPLETO PLANTA CON T?NEL CLOUDFLARE
echo ==============================================================================
echo.
echo [*] 1/4 Iniciando motor de adquisici?n Node-RED (Puerto 1880)...
start "1. Node-RED (PLC Siemens)" cmd /k "node-red.cmd"
timeout /t 3 /nobreak >nul

echo [*] 2/4 Iniciando Pasarela de Alertas WhatsApp (Puerto 5089)...
start "2. WhatsApp Gateway" cmd /k "cd /d %~dp0services\whatsapp-gateway && node server.js"
timeout /t 2 /nobreak >nul

echo [*] 3/4 Iniciando Servidor SCADA Blazor (Puerto 5088)...
start "3. Dashboard SCADA" cmd /k "cd /d %~dp0dashboard\PTAPControl && dotnet run --urls http://0.0.0.0:5088"
timeout /t 4 /nobreak >nul

echo [*] 4/4 Conectando T?nel Seguro Cloudflare a Internet...
set CONFIG_FILE=%~dp0tools\tunnel-token.txt
if exist "%CONFIG_FILE%" (
    set /p TOKEN=<"%CONFIG_FILE%"
    start "4. Tunel Cloudflare Edge" cmd /k "title Cloudflare Tunnel && "%~dp0tools\cloudflared.exe" tunnel run --token %TOKEN%"
) else (
    echo [AVISO] No se encontr? token en tools\tunnel-token.txt. Ejecuta Iniciar-Tunel-Cloudflare-Produccion.bat para configurarlo.
)

timeout /t 2 /nobreak >nul
start http://localhost:5088

echo.
echo ==============================================================================
echo  [+] SISTEMA COMPLETO OPERANDO:
echo      - Node-RED (PLC S7-1200): http://localhost:1880
echo      - WhatsApp Gateway     : http://localhost:5089/qr
echo      - Dashboard Local      : http://localhost:5088
echo      - Dominio en Internet  : https://hidrocontrol.potenzia.com
echo ==============================================================================
echo.
pause
