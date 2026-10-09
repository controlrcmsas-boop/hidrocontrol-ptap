@echo off
chcp 65001 >nul
title HIDROCONTROL PTAP - Diagnóstico de Conexión Servidor y PLC
color 0b

echo ===============================================================================
echo            HIDROCONTROL PTAP V2.2 - DIAGNÓSTICO INTEGRAL DE PLANTA
echo ===============================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command "& {
    $prodSettings = 'dashboard\PTAPControl\appsettings.Production.json'
    $baseUrl = 'http://localhost:1880/'
    if (Test-Path $prodSettings) {
        $json = Get-Content $prodSettings -Raw | ConvertFrom-Json
        if ($json.NodeRed.BaseUrl) { $baseUrl = $json.NodeRed.BaseUrl }
    }
    
    Write-Host '[1/4] Endpoint configurado en Dashboard:' -ForegroundColor Cyan
    Write-Host '      '$baseUrl -ForegroundColor Yellow
    Write-Host ''
    
    Write-Host '[2/4] Probando enlace HTTP con Node-RED...' -ForegroundColor Cyan
    try {
        $testUrl = $baseUrl.TrimEnd('/') + '/api/telemetry/current'
        $resp = Invoke-WebRequest -Uri $testUrl -UseBasicParsing -TimeoutSec 4
        if ($resp.StatusCode -eq 200) {
            Write-Host '      [+] [EXITO] Node-RED responde correctamente (HTTP 200).' -ForegroundColor Green
            $data = $resp.Content | ConvertFrom-Json
            Write-Host ('      [i] Estado PLC reportado: ' + ($data.plc_connected ? 'CONECTADO Y LEYENDO' : 'DESCONECTADO')) -ForegroundColor ($data.plc_connected ? 'Green' : 'Yellow')
            Write-Host ('      [i] Turbiedad Cruda: ' + $data.turbiedad_cruda + ' NTU') -ForegroundColor Gray
            Write-Host ('      [i] Nivel Tanque: ' + $data.nivel_tanque_lavado + ' %') -ForegroundColor Gray
        } else {
            Write-Host ('      [!] Node-RED respondió con código: ' + $resp.StatusCode) -ForegroundColor Yellow
        }
    } catch {
        Write-Host ('      [-] [FALLO] No se pudo conectar a ' + $baseUrl + ' (' + $_.Exception.Message + ')') -ForegroundColor Red
        Write-Host '          (Verifica que Node-RED esté corriendo en el servidor con Iniciar-TODO.bat).' -ForegroundColor Gray
    }
    Write-Host ''
    
    Write-Host '[3/4] Probando comunicación física con PLC Siemens S7-1200 (192.168.1.110)...' -ForegroundColor Cyan
    $ping = Test-Connection -ComputerName 192.168.1.110 -Count 2 -Quiet -ErrorAction SilentlyContinue
    if ($ping) {
        Write-Host '      [+] [EXITO] PLC Siemens S7-1200 (192.168.1.110) respondiendo en red local.' -ForegroundColor Green
    } else {
        Write-Host '      [!] [AVISO] El PLC (192.168.1.110) no responde a ping directo.' -ForegroundColor Yellow
        Write-Host '          (Normal si el cable Ethernet está desconectado temporalmente por programación).' -ForegroundColor Gray
    }
    Write-Host ''
    
    Write-Host '[4/4] Resumen general:' -ForegroundColor Cyan
    Write-Host '      Una vez el PLC esté conectado físicamente, ejecuta Iniciar-TODO-Con-Internet.bat' -ForegroundColor Gray
    Write-Host '      para levantar Node-RED, WhatsApp, el Dashboard y el enlace web a Cloudflare.' -ForegroundColor Gray
}"

echo.
echo ===============================================================================
pause
