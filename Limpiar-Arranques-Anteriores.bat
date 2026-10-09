@echo off
chcp 65001 >nul
title Limpiar Arranques Anteriores - HIDROCONTROL PTAP
color 0e

echo ==============================================================================
echo       LIMPIEZA DE ARRANQUES AUTOMÁTICOS ANTERIORES EN WINDOWS
echo ==============================================================================
echo.

:: 1. Eliminar tareas programadas viejas que coincidan con nombres del proyecto
echo [*] 1/3 Buscando y eliminando tareas programadas viejas...
schtasks /delete /tn "HidrocontrolServidorPlanta" /f >nul 2>&1
schtasks /delete /tn "HidrocontrolPlantaServer" /f >nul 2>&1
schtasks /delete /tn "Hidrocontrol" /f >nul 2>&1
schtasks /delete /tn "NodeRED" /f >nul 2>&1
schtasks /delete /tn "PTAP" /f >nul 2>&1
echo [+] Tareas programadas anteriores limpiadas.
echo.

:: 2. Abrir la carpeta de Inicio de Windows (shell:startup)
echo [*] 2/3 Abriendo la carpeta de Inicio del usuario...
echo     Si ves algún acceso directo viejo (ej: Iniciar-TODO, Iniciar-Dashboard, etc.),
echo     simplemente selecciónalo y bórralo (Supr).
start "" explorer.exe shell:startup
echo.

:: 3. Abrir la configuración de Aplicaciones de Inicio de Windows
echo [*] 3/3 Abriendo la pantalla de Aplicaciones de Inicio de Windows...
start "" ms-settings:startupapps
echo.

echo ==============================================================================
echo  Listo. Revisa la ventana de la carpeta y desactiva lo que sea viejo.
echo  Luego ya puedes ejecutar: Instalar-Auto-Arranque-Servidor.bat
echo ==============================================================================
echo.
pause
