@echo off
title Tunel Cloudflare - HIDROCONTROL PTAP (Produccion)
echo ==============================================================================
echo   HIDROCONTROL PTAP - TUNEL CLOUDFLARE PRODUCCION (hidrocontrol.potenzia.com)
echo ==============================================================================
echo.

set CONFIG_FILE=%~dp0tools\tunnel-token.txt

if not exist "%CONFIG_FILE%" (
    echo [AVISO] No se encontro el token configurado en tools\tunnel-token.txt
    echo.
    echo Pasos para configurar tu dominio hidrocontrol.potenzia.com:
    echo 1. Ve a Cloudflare Zero Trust: https://one.dash.cloudflare.com
    echo 2. Networks -> Tunnels -> Create Tunnel -> Nombre: hidrocontrol
    echo 3. Copia el token que te entrega Cloudflare.
    echo 4. Pegalo dentro del archivo tools\tunnel-token.txt
    echo.
    set /p USER_TOKEN="O pega el token directamente aqui y presiona Enter: "
    if "%USER_TOKEN%"=="" (
        echo Operacion cancelada.
        pause
        exit /b 1
    )
    echo %USER_TOKEN%> "%CONFIG_FILE%"
)

set /p TOKEN=<"%CONFIG_FILE%"

echo Conectando tunel a Cloudflare Edge para hidrocontrol.potenzia.com...
"%~dp0tools\cloudflared.exe" tunnel run --token %TOKEN%
pause
