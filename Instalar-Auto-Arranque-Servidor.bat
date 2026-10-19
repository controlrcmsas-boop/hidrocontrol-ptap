@echo off
chcp 65001 >nul
title Instalar Auto-Arranque de Planta - HIDROCONTROL PTAP
color 0b

echo ==============================================================================
echo    INSTALADOR DE AUTO-ARRANQUE AUTOM?TICO AL ENCENDER EL COMPUTADOR
echo ==============================================================================
echo.

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [*] Solicitando permisos de Administrador para registrar la tarea de inicio...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set SCRIPT_PATH=%~dp0Arrancar-Servidor-Planta-Tailscale.bat

echo [*] Registrando tarea programada en Windows (Ejecuci?n al iniciar sesi?n)...
schtasks /create /tn "HidrocontrolServidorPlanta" /tr "\"%SCRIPT_PATH%\"" /sc onlogon /rl highest /f >nul 2>&1

if %errorlevel% equ 0 (
    color 0a
    echo.
    echo ==============================================================================
    echo  [+] [?XITO ROTUNDO]
    echo      La tarea de inicio autom?tico fue instalada en Windows.
    echo.
    echo      A partir de ahora:
    echo      Cada vez que enciendas o reinicies este computador servidor,
    echo      Node-RED y Tailscale Funnel arrancar?n solos con permisos
    echo      de Administrador en segundo plano sin que tengas que tocar nada.
    echo ==============================================================================
) else (
    color 0c
    echo [-] Error registrando la tarea programada. Aseg?rate de ejecutar como Administrador.
)

echo.
pause
