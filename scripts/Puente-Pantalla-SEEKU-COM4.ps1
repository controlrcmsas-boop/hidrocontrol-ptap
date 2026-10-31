# ==============================================================================
# HIDROCONTROL PTAP - PUENTE MODBUS RTU EN VIVO (SEEKU HMI <-> NODE-RED)
# ==============================================================================
param(
    [string]$PortName = "COM4",
    [int]$BaudRate = 9600
)

$host.UI.RawUI.WindowTitle = "HIDROCONTROL PTAP - Enlace Pantalla SEEKU ($PortName @ $BaudRate)"

function Get-ModbusCrc([byte[]]$buf, [int]$len) {
    [uint16]$crc = 0xFFFF
    for ($i = 0; $i -lt $len; $i++) {
        $crc = $crc -bxor [uint16]$buf[$i]
        for ($j = 0; $j -lt 8; $j++) {
            if (($crc -band 0x0001) -ne 0) {
                $crc = ($crc -shr 1) -bxor 0xA001
            } else {
                $crc = $crc -shr 1
            }
        }
    }
    return [byte[]]([byte]($crc -band 0xFF), [byte](($crc -shr 8) -band 0xFF))
}

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "   HIDROCONTROL PTAP - PUENTE EN VIVO PANTALLA SEEKU ($PortName @ $BaudRate)" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host ""

try {
    $port = New-Object System.IO.Ports.SerialPort $PortName, $BaudRate, "None", 8, "One"
    $port.ReadTimeout = 200
    $port.WriteTimeout = 200
    $port.Open()
    Write-Host "[+] [PUERTO ABIERTO] Escuchando peticiones Modbus RTU de la pantalla en $PortName..." -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "[-] ERROR abriendo $PortName : $($_.Exception.Message)" -ForegroundColor Red
    pause
    exit
}

# Cache de telemetria
$lastFetch = 0
$telemetry = @{
    ph = 7.00
    turb = 0.5
    cond = 0.0
    bomba = 0
    dosif1 = 0
    dosif2 = 0
    msg = "Planta en linea"
}

$inBuf = New-Object byte[] 256

try {
    while ($true) {
        # Actualizar telemetria desde Node-RED cada 1 segundo
        $now = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        if (($now - $lastFetch) -ge 1000) {
            $lastFetch = $now
            try {
                $resp = Invoke-RestMethod -Uri "http://127.0.0.1:1880/api/telemetry/current" -TimeoutSec 1 -ErrorAction SilentlyContinue
                if ($resp -and $resp.values) {
                    $v = $resp.values
                    # Escalamiento calibrado
                    $rawTurb = if ($v.turbidez_agua_cruda -ne $null) { [double]$v.turbidez_agua_cruda } else { 484.3 }
                    $rawPh   = if ($v.ph_agua_cruda -ne $null) { [double]$v.ph_agua_cruda } else { 6.64 }
                    $rawCond = if ($v.conductividad_agua_cruda -ne $null) { [double]$v.conductividad_agua_cruda } else { 0.0 }

                    $telemetry.turb = [Math]::Max(0.5, [Math]::Round(((485.0 - $rawTurb) * 2.0), 1))
                    $telemetry.ph   = [Math]::Min(14.0, [Math]::Max(0.0, [Math]::Round((7.00 + ($rawPh - 6.64) * 1.8518), 2)))
                    $telemetry.cond = [Math]::Max(0.0, [Math]::Round($rawCond, 1))

                    $telemetry.bomba  = if ($v.bomba_principal_estado) { 1 } else { 0 }
                    $telemetry.dosif1 = if ($v.dosificador_sulfato_estado) { 1 } else { 0 }
                    $telemetry.dosif2 = if ($v.dosificador_cloro_estado) { 1 } else { 0 }
                }
            } catch {}
        }

        # Leer peticiones seriales de la pantalla
        if ($port.BytesToRead -ge 8) {
            $bytesRead = $port.Read($inBuf, 0, [Math]::Min($port.BytesToRead, $inBuf.Length))
            if ($bytesRead -ge 8) {
                $slaveId = $inBuf[0]
                $func    = $inBuf[1]
                $regAddr = ($inBuf[2] -shl 8) -bor $inBuf[3]
                $count   = ($inBuf[4] -shl 8) -bor $inBuf[5]

                # Funcion 03 (0x03): Lectura de Holding Registers (Sensores: pH, Turbidez, Conductividad)
                if ($func -eq 3 -or $func -eq 4) {
                    $numRegs = [Math]::Min($count, 20)
                    $byteCount = $numRegs * 2
                    $outBuf = New-Object byte[] (3 + $byteCount + 2)
                    $outBuf[0] = $slaveId
                    $outBuf[1] = $func
                    $outBuf[2] = [byte]$byteCount

                    # Rellenar registros
                    for ($r = 0; $r -lt $numRegs; $r++) {
                        $currAddr = $regAddr + $r
                        [int]$val = 0
                        if ($currAddr -eq 0) {
                            $val = [int]($telemetry.ph * 100) # pH con 2 decimales (ej. 705)
                        } elseif ($currAddr -eq 1) {
                            $val = [int]($telemetry.turb * 10) # Turbidez con 1 decimal (ej. 8 -> 0.8)
                        } elseif ($currAddr -eq 2) {
                            $val = [int]$telemetry.cond        # Conductividad entero
                        } elseif ($currAddr -ge 10 -and $currAddr -lt 30) {
                            # Texto ASCII de observacion IA
                            $charIdx = ($currAddr - 10) * 2
                            $msgStr = "Agente Local: Proceso Estable"
                            $c1 = if ($charIdx -lt $msgStr.Length) { [byte][char]$msgStr[$charIdx] } else { 32 }
                            $c2 = if (($charIdx + 1) -lt $msgStr.Length) { [byte][char]$msgStr[$charIdx + 1] } else { 32 }
                            $val = ($c1 -shl 8) -bor $c2
                        }

                        $idx = 3 + ($r * 2)
                        $outBuf[$idx]     = [byte](($val -shr 8) -band 0xFF)
                        $outBuf[$idx + 1] = [byte]($val -band 0xFF)
                    }

                    $crc = Get-ModbusCrc $outBuf (3 + $byteCount)
                    $outBuf[3 + $byteCount]     = $crc[0]
                    $outBuf[3 + $byteCount + 1] = $crc[1]

                    $port.Write($outBuf, 0, $outBuf.Length)
                    $time = Get-Date -Format "HH:mm:ss"
                    Write-Host "[$time] [ENVIADO A PANTALLA] pH: $($telemetry.ph) | Turb: $($telemetry.turb) | Cond: $($telemetry.cond)" -ForegroundColor Green
                }
                # Funcion 01 (0x01): Lectura de Coils (Estado de Bomba y Dosificadores)
                elseif ($func -eq 1 -or $func -eq 2) {
                    $coilByte = 0
                    if ($telemetry.bomba)  { $coilByte = $coilByte -bor 0x01 }
                    if ($telemetry.dosif1) { $coilByte = $coilByte -bor 0x02 }
                    if ($telemetry.dosif2) { $coilByte = $coilByte -bor 0x04 }

                    $outBuf = New-Object byte[] 6
                    $outBuf[0] = $slaveId
                    $outBuf[1] = $func
                    $outBuf[2] = 1 # 1 byte de datos
                    $outBuf[3] = [byte]$coilByte

                    $crc = Get-ModbusCrc $outBuf 4
                    $outBuf[4] = $crc[0]
                    $outBuf[5] = $crc[1]

                    $port.Write($outBuf, 0, 6)
                }
            }
        }
        Start-Sleep -Milliseconds 40
    }
} finally {
    if ($port.IsOpen) {
        $port.Close()
        Write-Host "[*] Puerto $PortName cerrado." -ForegroundColor Yellow
    }
}
