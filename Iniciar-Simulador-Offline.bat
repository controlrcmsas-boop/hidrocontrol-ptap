@echo off
title Iniciar Simulador PTAP Offline - HIDROCONTROL PTAP V2.2
chcp 65001 >nul
color 0b

echo ===============================================================================
echo        HIDROCONTROL PTAP V2.2 - SIMULADOR ESTOCÁSTICO OFFLINE
echo ===============================================================================
echo.
echo [*] Iniciando servidor de simulación en http://localhost:1881...
echo.

start "Simulador PTAP (Puerto 1881)" cmd /k "cd /d %~dp0simulator && node ptap-simulator.js"
timeout /t 2 /nobreak >nul

echo [+] Simulador activo en segundo plano.
echo.
echo [*] Iniciando Dashboard SCADA PTAP conectado al simulador...
set NODERED_BASE_URL=http://localhost:1881/

cd /d "%~dp0dashboard\PTAPControl"
start "" http://localhost:5088
dotnet run --urls http://0.0.0.0:5088
