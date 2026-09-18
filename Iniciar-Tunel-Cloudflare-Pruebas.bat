@echo off
title Tunel Cloudflare - HIDROCONTROL PTAP (Prueba Inmediata)
echo ==============================================================================
echo   HIDROCONTROL PTAP - TUNEL CLOUDFLARE (MODO PRUEBA INMEDIATA)
echo ==============================================================================
echo.
echo Iniciando tunel HTTPS publico temporal hacia Kestrel (http://localhost:5088)...
echo.
echo Copia la URL HTTPS que aparecera a continuacion (terminada en .trycloudflare.com)
echo para abrir el SCADA desde cualquier dispositivo o celular.
echo.
"%~dp0tools\cloudflared.exe" tunnel --url http://localhost:5088
pause
