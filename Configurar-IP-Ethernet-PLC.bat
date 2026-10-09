@echo off
chcp 65001 >nul
title Configurar IP Estática para PLC Siemens S7-1200
color 0a

echo ===============================================================================
echo      CONFIGURAR IP ESTÁTICA PARA COMUNICACIÓN CON PLC SIEMENS (192.168.1.50)
echo ===============================================================================
echo.
echo Este script configurará la tarjeta Ethernet con:
echo   - Dirección IP : 192.168.1.50
echo   - Máscara      : 255.255.255.0
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

echo [*] Configurando tarjeta 'Ethernet' con IP estática 192.168.1.50...
netsh interface ipv4 set address name="Ethernet" source=static address=192.168.1.50 mask=255.255.255.0

if %errorlevel% equ 0 (
    echo [+] [OK] IP 192.168.1.50 configurada exitosamente en la tarjeta 'Ethernet'.
) else (
    color 0e
    echo [!] No se pudo aplicar con el nombre 'Ethernet'. Intentando con adaptadores disponibles...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "& { $adapter = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceAlias -notlike '*Wi-Fi*' -and $_.InterfaceAlias -notlike '*Loopback*' } | Select-Object -First 1; if ($adapter) { New-NetIPAddress -InterfaceAlias $adapter.InterfaceAlias -IPAddress 192.168.1.50 -PrefixLength 24 -ErrorAction SilentlyContinue; Write-Host ('[+] IP configurada en: ' + $adapter.InterfaceAlias) -ForegroundColor Green } else { Write-Host '[!] Conecta el cable Ethernet y vuelve a ejecutar.' -ForegroundColor Yellow } }"
)

echo.
echo [*] Verificando comunicación inmediata con el PLC (192.168.1.110)...
ping -n 3 -w 1000 192.168.1.110
echo.
echo ===============================================================================
echo Si el ping respondió con éxito, ya puedes ejecutar Iniciar-TODO-Con-Internet.bat
echo ===============================================================================
pause
