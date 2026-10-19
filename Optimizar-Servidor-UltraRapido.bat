@echo off
chcp 65001 >nul
title Optimizaci?n de Rendimiento - HIDROCONTROL Servidor
color 0b

echo ==============================================================================
echo        OPTIMIZADOR DE RENDIMIENTO Y VELOCIDAD PARA EL SERVIDOR
echo ==============================================================================
echo.

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [*] Solicitando permisos de Administrador...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo [*] 1/5 Desactivando SysMain (Superfetch) que satura el disco al 100%%...
net stop sysmain >nul 2>&1
sc config sysmain start=disabled >nul 2>&1
echo [+] SysMain desactivado. El disco dejar? de estar clavado al 100%%.

echo.
echo [*] 2/5 Desactivando indexaci?n en segundo plano (Windows Search)...
net stop WSearch >nul 2>&1
sc config WSearch start=disabled >nul 2>&1
echo [+] Windows Search desactivado.

echo.
echo [*] 3/5 Configurando Plan de Energ?a a Alto Rendimiento...
powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c >nul 2>&1
echo [+] Energ?a configurada a m?ximo rendimiento.

echo.
echo [*] 4/5 Abriendo aplicaciones de inicio para que desactives lo innecesario...
start ms-settings:startupapps

echo.
echo [*] 5/5 Limpiando archivos temporales...
del /q /f /s "%TEMP%\*" >nul 2>&1

echo.
echo ==============================================================================
echo  [+] LISTO: El computador ahora respirar? mucho m?s libre y r?pido.
echo      En la ventana de 'Aplicaciones de inicio' que se acaba de abrir,
echo      DESACTIVA todo lo que no uses (Teams, OneDrive, Spotify, etc.).
echo ==============================================================================
echo.
pause
