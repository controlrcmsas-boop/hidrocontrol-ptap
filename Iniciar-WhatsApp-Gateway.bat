@echo off
chcp 65001 >nul
title Pasarela WhatsApp - HIDROCONTROL PTAP
color 0a

echo ========================================================
echo   HIDROCONTROL PTAP - PASARELA EMISORA DE WHATSAPP
echo ========================================================
echo.
cd /d "%~dp0services\whatsapp-gateway"
node server.js
pause
