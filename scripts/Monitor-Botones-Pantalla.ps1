# ==============================================================================
# HIDROCONTROL PTAP - MONITOR EN VIVO DE PULSACIONES (SEEKU TÁCTIL)
# ==============================================================================
param(
    [string]$PortName = "COM4",
    [int]$BaudRate = 38400
)

$host.UI.RawUI.WindowTitle = "HIDROCONTROL - Monitor en Vivo Pantalla Táctil SEEKU"

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

# 7 bits, Paridad Par (Even), 1 stop bit
$sp = New-Object System.IO.Ports.SerialPort $PortName, $BaudRate, "Even", 7, "One"
$sp.ReadTimeout = 150
$sp.WriteTimeout = 150

try {
    $sp.Open()
    Write-Host "==============================================================================" -ForegroundColor Cyan
    Write-Host "   HIDROCONTROL PTAP - MONITOR EN VIVO DE PULSACIONES (SEEKU TACTIL)" -ForegroundColor Cyan
    Write-Host "==============================================================================" -ForegroundColor Cyan
    Write-Host "[+] Conectado exitosamente a $PortName @ $BaudRate (7E1)." -ForegroundColor Green
    Write-Host "-> TOCA LOS SWITCHES EN TU PANTALLA FISICA (BOMBA / SULFATO / CLORO)..." -ForegroundColor Yellow
    Write-Host "   (Presiona Ctrl + C para salir)" -ForegroundColor Gray
    Write-Host "------------------------------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host ""

    # Leemos 2 bytes desde 0x0100 (M0..M15 clasico) y 2 bytes desde 0x0800
    $cmd0100 = Build-FxReadCommand 0x0100 2
    $lastState = ""
    $heartbeat = 0

    while ($true) {
        $sp.DiscardInBuffer()
        $sp.Write($cmd0100, 0, $cmd0100.Length)
        Start-Sleep -Milliseconds 50

        if ($sp.BytesToRead -gt 0) {
            $buf = New-Object byte[] $sp.BytesToRead
            $sp.Read($buf, 0, $buf.Length) | Out-Null
            
            # Formato respuesta: STX + DATA_HEX + ETX + SUM
            if ($buf.Length -ge 7) {
                # Limpiar paridad si viene con bit alto
                for ($i = 0; $i -lt $buf.Length; $i++) { $buf[$i] = $buf[$i] -band 0x7F }
                
                $byte0Str = [Text.Encoding]::ASCII.GetString($buf, 1, 2)
                $byte1Str = [Text.Encoding]::ASCII.GetString($buf, 3, 2)
                
                $byte0 = 0
                $byte1 = 0
                [byte]::TryParse($byte0Str, [System.Globalization.NumberStyles]::HexNumber, $null, [ref]$byte0) | Out-Null
                [byte]::TryParse($byte1Str, [System.Globalization.NumberStyles]::HexNumber, $null, [ref]$byte1) | Out-Null

                # M0, M1, M2 en Byte 0
                $m0 = ($byte0 -band 0x01) -ne 0
                $m1 = ($byte0 -band 0x02) -ne 0
                $m2 = ($byte0 -band 0x04) -ne 0

                # M10, M12, M14 en Byte 1 (por compatibilidad si estan en M10)
                $m10 = ($byte1 -band 0x04) -ne 0
                $m12 = ($byte1 -band 0x10) -ne 0
                $m14 = ($byte1 -band 0x40) -ne 0

                $bombaActive   = ($m0 -or $m10)
                $sulfatoActive = ($m1 -or $m12)
                $cloroActive   = ($m2 -or $m14)

                $stateKey = "$bombaActive-$sulfatoActive-$cloroActive-$byte0Str$byte1Str"
                $time = Get-Date -Format "HH:mm:ss.fff"

                if ($stateKey -ne $lastState) {
                    $isInit = ($lastState -eq "")
                    $lastState = $stateKey

                    $bText = if ($bombaActive)   { "[ON ] ENCENDIDA" } else { "[OFF] APAGADA  " }
                    $sText = if ($sulfatoActive) { "[ON ] ENCENDIDO" } else { "[OFF] APAGADO  " }
                    $cText = if ($cloroActive)   { "[ON ] ENCENDIDO" } else { "[OFF] APAGADO  " }

                    $color = if ($isInit) { "Cyan" } else { "Yellow" }
                    $tag   = if ($isInit) { "ESTADO INICIAL" } else { "EVENTO TACTIL! " }

                    Write-Host "[$time] [$tag] Bomba: $bText | Sulfato: $sText | Cloro: $cText (Hex: $byte0Str $byte1Str)" -ForegroundColor $color
                    if (-not $isInit) { [Console]::Beep(1200, 100) }
                } else {
                    $heartbeat++
                    if (($heartbeat % 25) -eq 0) {
                        $bText = if ($bombaActive)   { "ON " } else { "OFF" }
                        $sText = if ($sulfatoActive) { "ON " } else { "OFF" }
                        $cText = if ($cloroActive)   { "ON " } else { "OFF" }
                        Write-Host "[$time] [Escuchando...] Bomba:$bText | Sulf:$sText | Cloro:$cText" -ForegroundColor DarkGray
                    }
                }
            }
        }
        Start-Sleep -Milliseconds 60
    }
} catch {
    Write-Host "[-] Error: $_" -ForegroundColor Red
} finally {
    if ($sp -and $sp.IsOpen) {
        $sp.Close()
        Write-Host "[*] Puerto $PortName cerrado." -ForegroundColor Gray
    }
}
