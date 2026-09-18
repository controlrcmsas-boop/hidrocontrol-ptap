@echo off
title Iniciar HIDROCONTROL PTAP V2.2
echo ========================================================
echo   Iniciando Node-RED, WhatsApp Gateway y Dashboard...
echo ========================================================

start "Node-RED" cmd /k "node-red.cmd"
timeout /t 3 /nobreak >nul

start "WhatsApp Gateway" cmd /k "cd /d %~dp0services\whatsapp-gateway && node server.js"
timeout /t 2 /nobreak >nul

start "Dashboard SCADA" cmd /k "cd /d %~dp0dashboard\PTAPControl && dotnet run --urls http://0.0.0.0:5088"
timeout /t 3 /nobreak >nul

start http://localhost:5088
echo Sistema iniciado exitosamente.
