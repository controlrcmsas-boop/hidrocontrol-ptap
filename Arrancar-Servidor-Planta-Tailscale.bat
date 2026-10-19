@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP - Servidor de Planta Autom?tico
color 0a

:: 1. Auto-elevaci?n a permisos de Administrador si no los tiene
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [*] Solicitando permisos de Administrador...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo ==============================================================================
echo        HIDROCONTROL PTAP - ARRANQUE AUTOM?TICO DE PLANTA Y TAILSCALE
echo ==============================================================================
echo.

:: 2. Asegurar que Tailscale Funnel est? activo hacia internet en segundo plano
echo [*] 1/2 Verificando t?nel seguro Tailscale (Puerto 1880)...
tailscale funnel --bg 1880 >nul 2>&1
if %errorlevel% equ 0 (
    echo [+] [OK] Tailscale Funnel activo en https://desktop-u8frjng.tail82f1e5.ts.net/
) else (
    echo [!] Tailscale ya estaba activo o se iniciar? con la sesi?n.
)
echo.

:: 3. Iniciar Node-RED en entorno Node.js
echo [*] 2/2 Iniciando motor de adquisici?n Node-RED (Lectura PLC Siemens)...
start "Node-RED - Enlace PLC Siemens" cmd /k "title Node-RED (PLC S7-1200) && node-red.cmd || node-red"

timeout /t 4 /nobreak >nul

echo.
echo ==============================================================================
echo  [+] SERVIDOR DE PLANTA EN L?NEA:
echo      - Node-RED Local : http://localhost:1880
echo      - Monitoreo Web  : https://hidrocontrol.potenzia.app
echo ==============================================================================
echo.
echo Esta ventana se cerrar? en 5 segundos (Node-RED quedar? operando en segundo plano).
timeout /t 5 >nul
exit
