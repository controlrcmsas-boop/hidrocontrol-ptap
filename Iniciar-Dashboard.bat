@echo off
chcp 65001 >nul
title Dashboard SCADA - HIDROCONTROL PTAP V2.2
color 0a

echo ==============================================================================
echo           HIDROCONTROL PTAP V2.2 - INICIANDO DASHBOARD SCADA
echo ==============================================================================
echo.
echo [*] Conectando internamente con Node-RED en http://localhost:1880...
echo [*] Abriendo Dashboard en http://localhost:5088...
echo.

timeout /t 2 /nobreak >nul
start "" http://localhost:5088

cd /d "%~dp0dashboard\PTAPControl"
dotnet run --urls "http://0.0.0.0:5088"
pause
