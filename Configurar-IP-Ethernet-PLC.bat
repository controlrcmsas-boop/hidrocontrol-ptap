@echo off
chcp 65001 >nul
title Configurar IP Est?tica para PLC Siemens S7-1200
color 0a

echo ===============================================================================
echo      CONFIGURAR IP EST?TICA PARA COMUNICACI?N CON PLC SIEMENS (192.168.1.50)
echo ===============================================================================
echo.
echo Este script configurar? la tarjeta Ethernet con:
echo   - Direcci?n IP : 192.168.1.50
echo   - M?scara      : 255.255.255.0
echo   - Puerta enlace: (ninguna)
echo.

net session >nul 2>&1
if %errorlevel% neq 0 (
    color 0c
    echo [ERROR] Este script requiere Permisos de Administrador para cambiar la IP.
    echo Por favor haz clic derecho en este archivo y selecciona:
    echo "Ejecutar como administrador".
    echo.
    pause
    exit /b 1
)

echo [*] Configurando tarjeta 'Ethernet' con IP est?tica 192.168.1.50...
netsh interface ipv4 set address name="Ethernet" source=static address=192.168.1.50 mask=255.255.255.0

if %errorlevel% equ 0 (
    echo [+] [OK] IP 192.168.1.50 configurada exitosamente en la tarjeta 'Ethernet'.
) else (
    color 0e
    echo [!] No se pudo aplicar con el nombre 'Ethernet'. Intentando con adaptadores disponibles...
)

echo [*] Protegiendo conexion Wi-Fi (Prioridad Metrica: Wi-Fi=10, Ethernet=50)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetIPInterface -InterfaceAlias '*Wi-Fi*' -ErrorAction SilentlyContinue | Set-NetIPInterface -InterfaceMetric 10 -ErrorAction SilentlyContinue; Get-NetIPInterface -InterfaceAlias '*Ethernet*' -ErrorAction SilentlyContinue | Set-NetIPInterface -InterfaceMetric 50 -ErrorAction SilentlyContinue"


echo.
echo [*] Verificando comunicaci?n inmediata con el PLC (192.168.1.110)...
ping -n 3 -w 1000 192.168.1.110
echo.
echo ===============================================================================
echo Si el ping respondi? con ?xito, ya puedes ejecutar Iniciar-TODO-Con-Internet.bat
echo ===============================================================================
pause
