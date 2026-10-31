# ==============================================================================
# HIDROCONTROL PTAP - ENLACE MAESTRO BIDIRECCIONAL PANTALLA SEEKU (MITSUBISHI FX)
# ==============================================================================
# 1. Telemetría Siemens S7-1200 -> Pantalla SEEKU:
#      - D0 : pH Agua Cruda (escala x100)
#      - D1 : Turbidez (escala x10)
#      - D2 : Conductividad (escala x10)
#      - D10: Mensaje Agente IA
#      - D8013..D8018: Sincronización Automática de Fecha y Hora del Sistema
# 2. Pantalla SEEKU (Botones M0, M1, M2) -> Siemens S7-1200 (Node-RED API):
#      - M0: Bomba Principal
#      - M1: Dosificador Sulfato
#      - M2: Dosificador Cloro
# ==============================================================================
param(
    [string]$PortName = "",
    [int]$BaudRate = 38400
)

$host.UI.RawUI.WindowTitle = "HIDROCONTROL PTAP - Enlace Pantalla SEEKU FX (Bidireccional)"

function Write-Log([string]$msg, [string]$color = "White") {
    $time = Get-Date -Format "HH:mm:ss"
    Write-Host "[$time] $msg" -ForegroundColor $color
}

# 1. Detección automática del puerto COM del adaptador USB-RS485 (CH340 / FTDI / Prolific)
if ([string]::IsNullOrWhiteSpace($PortName)) {
    Write-Log "[*] Buscando adaptador USB-RS485..." "Yellow"
    $pnp = Get-PnpDevice -Class Ports -ErrorAction SilentlyContinue | Where-Object { 
        $_.Present -and ($_.FriendlyName -match "CH340|USB-SERIAL|Serial|COM\d+") 
    }
    if ($pnp) {
        $m = [regex]::Match($pnp[0].FriendlyName, "COM\d+")
        if ($m.Success) {
            $PortName = $m.Value
            Write-Log "[+] Detectado automaticamente puerto: $PortName ($($pnp[0].FriendlyName))" "Green"
        }
    }
    if ([string]::IsNullOrWhiteSpace($PortName)) {
        $PortName = "COM4"
        Write-Log "[!] Usando puerto predeterminado: $PortName" "Yellow"
    }
}

# 2. Funciones constructoras de tramas Mitsubishi FX
function Build-FxWriteCommand([int]$memAddr, [byte[]]$bytesToWrite) {
    $stx = [byte]0x02
    $etx = [byte]0x03
    $cmd = [Text.Encoding]::ASCII.GetBytes("1")
    $addrHex = $memAddr.ToString("X4").ToUpper()
    $addrBytes = [Text.Encoding]::ASCII.GetBytes($addrHex)
    $lenHex = $bytesToWrite.Length.ToString("X2").ToUpper()
    $lenBytes = [Text.Encoding]::ASCII.GetBytes($lenHex)
    $dataHex = ($bytesToWrite | ForEach-Object { $_.ToString("X2").ToUpper() }) -join ""
    $dataBytes = [Text.Encoding]::ASCII.GetBytes($dataHex)
    
    $sumPayload = @() + $cmd + $addrBytes + $lenBytes + $dataBytes + $etx
    $checksumInt = 0
    foreach ($b in $sumPayload) { $checksumInt = ($checksumInt + $b) % 256 }
    $checksumBytes = [Text.Encoding]::ASCII.GetBytes($checksumInt.ToString("X2").ToUpper())
    
    $packet = New-Object byte[] (1 + $sumPayload.Length + 2)
    $packet[0] = $stx
    [Array]::Copy($sumPayload, 0, $packet, 1, $sumPayload.Length)
    $packet[$packet.Length - 2] = $checksumBytes[0]
    $packet[$packet.Length - 1] = $checksumBytes[1]
    return $packet
}

function Build-FxReadCommand([int]$memAddr, [int]$byteCount) {
    $stx = [byte]0x02
    $etx = [byte]0x03
    $cmd = [Text.Encoding]::ASCII.GetBytes("0")
    $addrHex = $memAddr.ToString("X4").ToUpper()
    $addrBytes = [Text.Encoding]::ASCII.GetBytes($addrHex)
    $lenHex = $byteCount.ToString("X2").ToUpper()
    $lenBytes = [Text.Encoding]::ASCII.GetBytes($lenHex)
    
    $sumPayload = @() + $cmd + $addrBytes + $lenBytes + $etx
    $checksumInt = 0
    foreach ($b in $sumPayload) { $checksumInt = ($checksumInt + $b) % 256 }
    $checksumBytes = [Text.Encoding]::ASCII.GetBytes($checksumInt.ToString("X2").ToUpper())
    
    $packet = New-Object byte[] (1 + $sumPayload.Length + 2)
    $packet[0] = $stx
    [Array]::Copy($sumPayload, 0, $packet, 1, $sumPayload.Length)
    $packet[$packet.Length - 2] = $checksumBytes[0]
    $packet[$packet.Length - 1] = $checksumBytes[1]
    return $packet
}

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "   HIDROCONTROL PTAP - ENLACE PANTALLA SEEKU HMI BIDIRECCIONAL ($PortName)" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan

# 3. Apertura de puerto serial con los parámetros probados: 38400, Even, 7, One
$port = $null
try {
    $port = New-Object System.IO.Ports.SerialPort $PortName, $BaudRate, "Even", 7, "One"
    $port.ReadTimeout = 120
    $port.WriteTimeout = 120
    $port.Open()
    Write-Log "[+] Puerto $PortName abierto correctamente a $BaudRate baud (7E1)." "Green"
} catch {
    Write-Log "[-] ERROR abriendo $PortName : $($_.Exception.Message)" "Red"
    Write-Log "    Asegurate de que el cable USB-RS485 este conectado." "Yellow"
    return
}

# 4. Variables de control de estado
$lastTelemetryTime = 0
$lastClockSyncTime = 0
$prevButtonState = @{ m0 = $false; m1 = $false; m2 = $false }
$isFirstRead = $true

# Trama para consultar M0..M15 (0x0100)
$cmdReadM = Build-FxReadCommand 0x0100 2

try {
    while ($true) {
        $now = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

        # ======================================================================
        # A. LECTURA DE BOTONES TÁCTILES DESDE LA PANTALLA (Cada 200 ms)
        # ======================================================================
        $port.DiscardInBuffer()
        $port.Write($cmdReadM, 0, $cmdReadM.Length)
        Start-Sleep -Milliseconds 40

        if ($port.BytesToRead -gt 0) {
            $buf = New-Object byte[] $port.BytesToRead
            $port.Read($buf, 0, $buf.Length) | Out-Null

            if ($buf.Length -ge 7) {
                # Limpiar bit de paridad
                for ($i = 0; $i -lt $buf.Length; $i++) { $buf[$i] = $buf[$i] -band 0x7F }
                $byte0Str = [Text.Encoding]::ASCII.GetString($buf, 1, 2)
                $byte0 = 0
                [byte]::TryParse($byte0Str, [System.Globalization.NumberStyles]::HexNumber, $null, [ref]$byte0) | Out-Null

                $currM0 = ($byte0 -band 0x01) -ne 0
                $currM1 = ($byte0 -band 0x02) -ne 0
                $currM2 = ($byte0 -band 0x04) -ne 0

                if ($isFirstRead) {
                    $prevButtonState.m0 = $currM0
                    $prevButtonState.m1 = $currM1
                    $prevButtonState.m2 = $currM2
                    $isFirstRead = $false
                    Write-Log "[ESTADO INICIAL PANTALLA] Bomba:$($currM0) | Sulfato:$($currM1) | Cloro:$($currM2)" "DarkGray"
                } else {
                    # Detección de cambios de estado (Toggle)
                    if ($currM0 -ne $prevButtonState.m0) {
                        $prevButtonState.m0 = $currM0
                        $cmdAction = if ($currM0) { "start" } else { "stop" }
                        Write-Log "[BOTÓN TÁCTIL] Bomba Principal -> $cmdAction" "Yellow"
                        [Console]::Beep(1200, 80)
                        try {
                            $body = @{ target = "bomba_principal"; command = $cmdAction; source = "SEEKU_HMI_M0" } | ConvertTo-Json
                            Invoke-RestMethod -Uri "http://127.0.0.1:1880/api/control/command" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 1 -ErrorAction SilentlyContinue | Out-Null
                        } catch {}
                    }

                    if ($currM1 -ne $prevButtonState.m1) {
                        $prevButtonState.m1 = $currM1
                        $cmdAction = if ($currM1) { "start" } else { "stop" }
                        Write-Log "[BOTÓN TÁCTIL] Dosificador Sulfato -> $cmdAction" "Yellow"
                        [Console]::Beep(1200, 80)
                        try {
                            $body = @{ target = "dosificador_sulfato"; command = $cmdAction; source = "SEEKU_HMI_M1" } | ConvertTo-Json
                            Invoke-RestMethod -Uri "http://127.0.0.1:1880/api/control/command" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 1 -ErrorAction SilentlyContinue | Out-Null
                        } catch {}
                    }

                    if ($currM2 -ne $prevButtonState.m2) {
                        $prevButtonState.m2 = $currM2
                        $cmdAction = if ($currM2) { "start" } else { "stop" }
                        Write-Log "[BOTÓN TÁCTIL] Dosificador Cloro -> $cmdAction" "Yellow"
                        [Console]::Beep(1200, 80)
                        try {
                            $body = @{ target = "dosificador_cloro"; command = $cmdAction; source = "SEEKU_HMI_M2" } | ConvertTo-Json
                            Invoke-RestMethod -Uri "http://127.0.0.1:1880/api/control/command" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 1 -ErrorAction SilentlyContinue | Out-Null
                        } catch {}
                    }
                }
            }
        }

        # ======================================================================
        # B. ENVÍO DE TELEMETRÍA SIEMENS S7 -> PANTALLA (Cada 1 segundo)
        # ======================================================================
        if (($now - $lastTelemetryTime) -ge 1000) {
            $lastTelemetryTime = $now
            
            $phVal = 7.20
            $turbVal = 25.0
            $condVal = 0.9
            $bombaReal = $false
            $sulfReal = $false
            $cloroReal = $false
            
            try {
                $resp = Invoke-RestMethod -Uri "http://127.0.0.1:1880/api/telemetry/current" -TimeoutSec 1 -ErrorAction SilentlyContinue
                if ($resp -and $resp.values) {
                    $v = $resp.values
                    if ($v.ph_agua_cruda -ne $null) { $phVal = [Math]::Round([double]$v.ph_agua_cruda, 2) }
                    if ($v.turbidez_agua_cruda -ne $null) { $turbVal = [Math]::Round([double]$v.turbidez_agua_cruda, 1) }
                    if ($v.conductividad_agua_cruda -ne $null) { $condVal = [Math]::Round([double]$v.conductividad_agua_cruda, 1) }
                    $bombaReal = [bool]$v.bomba_principal_estado
                    $sulfReal  = [bool]$v.dosificador_sulfato_estado
                    $cloroReal = [bool]$v.dosificador_cloro_estado
                }
            } catch {}

            # D0 = pH * 100, D1 = Turb * 10, D2 = Cond * 10
            $d0Int = [int]($phVal * 100)
            $d1Int = [int]($turbVal * 10)
            $d2Int = [int]($condVal * 10)

            $dBytes = New-Object byte[] 6
            $dBytes[0] = [byte]($d0Int -band 0xFF); $dBytes[1] = [byte](($d0Int -shr 8) -band 0xFF)
            $dBytes[2] = [byte]($d1Int -band 0xFF); $dBytes[3] = [byte](($d1Int -shr 8) -band 0xFF)
            $dBytes[4] = [byte]($d2Int -band 0xFF); $dBytes[5] = [byte](($d2Int -shr 8) -band 0xFF)

            $cmdD = Build-FxWriteCommand 0x1000 $dBytes
            $port.DiscardInBuffer()
            $port.Write($cmdD, 0, $cmdD.Length)
            Start-Sleep -Milliseconds 40

            # Observación dinámica de MO para la pantalla física (Estrictamente 100 caracteres)
            $statusMsg = ""
            if (-not $bombaReal) {
                $statusMsg = "MO: PLANTA EN ESPERA. BOMBA DETENIDA. SISTEMA EN REPOSO Y SEGURIDAD FISICA ACTIVA."
            } elseif ($turbVal -gt 5.0) {
                $statusMsg = "MO: ALERTA TURBIDEZ " + $turbVal + " NTU. DOSIFICANDO SULFATO PARA FLOCULACION Y PROTECCION."
            } elseif ($phVal -lt 5.5) {
                $statusMsg = "MO: ALERTA pH ACIDO " + $phVal + ". CLORACION PAUSADA PREVENTIVAMENTE. SE REQUIERE CORRECCION."
            } elseif ($condVal -gt 1200.0) {
                $statusMsg = "MO: ALERTA CONDUCTIVIDAD " + $condVal + " uS/cm. POSIBLE INTRUSION DE SALES DISUELTAS."
            } else {
                $statusMsg = "MO: OPERACION NOMINAL. TURBIDEZ Y pH EN RANGO VERDE. CLORACION PREVENTIVA ACTIVA."
            }

            # Formateo estricto a 100 caracteres exactos (50 registros Modbus D10..D59)
            $paddedText = $statusMsg.PadRight(100).Substring(0, 100)
            $allBytes = [Text.Encoding]::ASCII.GetBytes($paddedText)
            
            # Bloque 1: D10..D25 (32 bytes)
            $b1 = New-Object byte[] 32; [Array]::Copy($allBytes, 0, $b1, 0, 32)
            $cmdB1 = Build-FxWriteCommand 0x1014 $b1
            $port.DiscardInBuffer(); $port.Write($cmdB1, 0, $cmdB1.Length); Start-Sleep -Milliseconds 25

            # Bloque 2: D26..D41 (32 bytes)
            $b2 = New-Object byte[] 32; [Array]::Copy($allBytes, 32, $b2, 0, 32)
            $cmdB2 = Build-FxWriteCommand 0x1034 $b2
            $port.DiscardInBuffer(); $port.Write($cmdB2, 0, $cmdB2.Length); Start-Sleep -Milliseconds 25

            # Bloque 3: D42..D57 (32 bytes)
            $b3 = New-Object byte[] 32; [Array]::Copy($allBytes, 64, $b3, 0, 32)
            $cmdB3 = Build-FxWriteCommand 0x1054 $b3
            $port.DiscardInBuffer(); $port.Write($cmdB3, 0, $cmdB3.Length); Start-Sleep -Milliseconds 25

            # Bloque 4: D58..D59 (4 bytes)
            $b4 = New-Object byte[] 4; [Array]::Copy($allBytes, 96, $b4, 0, 4)
            $cmdB4 = Build-FxWriteCommand 0x1074 $b4
            $port.DiscardInBuffer(); $port.Write($cmdB4, 0, $cmdB4.Length); Start-Sleep -Milliseconds 25
        }

        # ======================================================================
        # C. SINCRONIZACIÓN AUTOMÁTICA DE HORA DEL SISTEMA AL PLC (Cada 60 segundos)
        # ======================================================================
        if (($now - $lastClockSyncTime) -ge 60000 -or $lastClockSyncTime -eq 0) {
            $lastClockSyncTime = $now
            $dt = Get-Date
            # Registros especiales FX3U RTC: D8013=segundos, D8014=minutos, D8015=horas, D8016=día, D8017=mes, D8018=año(2 digitos)
            # D8013 en memoria FX = 0x1000 + (8013 * 2) = 0x4E9A
            $rtcBytes = New-Object byte[] 12
            $rtcVals = @($dt.Second, $dt.Minute, $dt.Hour, $dt.Day, $dt.Month, ($dt.Year % 100))
            for ($k = 0; $k -lt 6; $k++) {
                $rtcBytes[$k * 2]     = [byte]($rtcVals[$k] -band 0xFF)
                $rtcBytes[$k * 2 + 1] = [byte](($rtcVals[$k] -shr 8) -band 0xFF)
            }
            try {
                $cmdRTC = Build-FxWriteCommand 0x4E9A $rtcBytes
                $port.DiscardInBuffer()
                $port.Write($cmdRTC, 0, $cmdRTC.Length)
                Start-Sleep -Milliseconds 30
            } catch {}
        }

        Start-Sleep -Milliseconds 120
    }
} catch {
    Write-Log "[-] Error en ciclo de comunicacion: $($_.Exception.Message)" "Red"
} finally {
    if ($port -and $port.IsOpen) {
        $port.Close()
        Write-Log "[*] Puerto serial cerrado." "Gray"
    }
}
