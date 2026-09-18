@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP V2.2 - Agente Supervisor Local
color 0a

echo ===============================================================================
echo        HIDROCONTROL PTAP V2.2 - SUPERVISOR VIRTUAL LOCAL (OFFLINE)
echo ===============================================================================
echo.

echo [*] Verificando servicio local de Ollama...
curl -s http://localhost:11434/api/tags >nul 2>&1
if %errorlevel% neq 0 (
    echo [*] Iniciando servidor de inferencia local Ollama en segundo plano...
    start /min "" ollama serve
    timeout /t 3 /nobreak >nul
) else (
    echo [+] Servidor local Ollama ya está activo y respondiendo en http://localhost:11434.
)

echo [*] Iniciando Dashboard HIDROCONTROL PTAP...
cd /d "%~dp0dashboard\PTAPControl"
start "" http://localhost:5088/analisis-ia
dotnet run --launch-profile http
pause