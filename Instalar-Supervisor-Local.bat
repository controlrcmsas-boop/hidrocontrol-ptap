@echo off
chcp 65001 >nul
title Instalación del Supervisor Local PTAP (Ollama)
color 0b

echo ===============================================================================
echo        HIDROCONTROL PTAP V2.2 - INSTALACIÓN DE AGENTE LOCAL OFFLINE
echo ===============================================================================
echo.
echo [1/3] Verificando si Ollama está instalado en Windows...
where ollama >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] Ollama no fue detectado en el PATH del sistema.
    echo [*] Intentando instalación automática mediante Windows Package Manager (winget)...
    echo.
    winget install -e --id Ollama.Ollama --accept-source-agreements --accept-package-agreements
    if %errorlevel% neq 0 (
        echo.
        echo [X] No se pudo instalar automáticamente con winget.
        echo [*] Por favor descarga e instala Ollama manualmente desde: https://ollama.com/download/windows
        echo [*] Luego vuelve a ejecutar este instalador.
        echo.
        pause
        exit /b 1
    )
    echo [+] Ollama instalado correctamente.
    echo [*] Reiniciando entorno...
) else (
    echo [+] Ollama ya está instalado en el sistema.
)

echo.
echo [2/3] Descargando modelo base Llama 3.2 (3B) optimizado para CPU...
echo [*] Espere mientras se descargan las ponderaciones (~2.0 GB)...
ollama pull llama3.2:3b
if %errorlevel% neq 0 (
    echo [X] Error al descargar llama3.2:3b. Verifique su conexión temporal de descarga.
    pause
    exit /b 1
)

echo.
echo [3/3] Compilando el modelo especializado 'ptap-supervisor' con manuales de planta...
cd /d "%~dp0"
ollama create ptap-supervisor -f Modelfile.ptap
if %errorlevel% neq 0 (
    echo [X] Error al crear el modelo ptap-supervisor.
    pause
    exit /b 1
)

echo.
echo ===============================================================================
echo [+] ¡CAPACITACIÓN E INSTALACIÓN LOCAL COMPLETADA CON ÉXITO!
echo [+] El modelo 'ptap-supervisor' está listo para operar 100%% offline.
echo ===============================================================================
echo.
pause