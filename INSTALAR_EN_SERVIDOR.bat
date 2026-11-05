@echo off
chcp 65001 >nul
title INSTALAR ACTUALIZACION EN SERVIDOR HIDROCONTROL
color 0a

echo ==============================================================================
echo       INSTALADOR DE ACTUALIZACION EN SERVIDOR DE PLANTA
echo ==============================================================================
echo.
set "DEST=C:\SERVIDOR_HIDROCONTROL"

if not exist "%DEST%" (
    echo [ERROR] No se encontro la carpeta de destino: %DEST%
    pause
    exit /b 1
)

echo [*] Copiando archivos del Dashboard SCADA (Home.razor, AnalisisIA, DLL)...
xcopy /Y /E /I "dashboard\PTAPControl\*" "%DEST%\dashboard\PTAPControl\" >nul

echo [*] Copiando Agente Edge Local y Consola...
copy /Y "services\edge-agent\edge-agent.js" "%DEST%\services\edge-agent\" >nul
copy /Y "Consola-Local-Servidor.html" "%DEST%\" >nul
copy /Y "Abrir-Consola-Local-Servidor.bat" "%DEST%\" >nul

echo [*] Copiando Enlace de Pantalla SEEKU...
copy /Y "scripts\Enlace-Pantalla-SEEKU-FX.ps1" "%DEST%\scripts\" >nul
copy /Y "AUTOSTART-PLANTA.bat" "%DEST%\" >nul
copy /Y "Iniciar-TODO-Con-Internet.bat" "%DEST%\" >nul
copy /Y "Iniciar-TODO.bat" "%DEST%\" >nul

echo [*] Actualizando flujo Node-RED...
copy /Y "nodered\flows\hidrocontrol-ptap-flow.json" "%DEST%\nodered\flows\" >nul
if exist "%USERPROFILE%\.node-red\flows.json" (
    copy /Y "nodered\flows\hidrocontrol-ptap-flow.json" "%USERPROFILE%\.node-red\flows.json" >nul
)

echo [*] Creando lanzador directo en tu Escritorio...
copy /Y "%DEST%\AUTOSTART-PLANTA.bat" "%USERPROFILE%\Desktop\ARRANCAR_PLANTA_Y_PRESENTACION.bat" >nul 2>&1

echo.
echo ==============================================================================
echo  [+] ACTUALIZACION INSTALADA EXITOSAMENTE
echo      - Coma de turbidez corregida (2.64 NTU)
echo      - Historial de alarmas y graficas persistente
echo      - Pasarela de WhatsApp automatizada
echo      - Enlace de pantalla fisica SEEKU optimizado
echo ==============================================================================
echo.
set /p START_NOW="Presiona [S] para Iniciar la planta ahora o [Enter] para salir: "
if /i "%START_NOW%"=="S" (
    start "" "%DEST%\AUTOSTART-PLANTA.bat"
)
