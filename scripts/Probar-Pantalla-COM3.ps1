$portName = "COM3"
$baudRate = 9600

Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "   HIDROCONTROL PTAP - DIAGNOSTICO EN VIVO DE PANTALLA SEEKU ($portName @ $baudRate)" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "[*] Abriendo puerto $portName..." -ForegroundColor Yellow

try {
    $port = New-Object System.IO.Ports.SerialPort $portName, $baudRate, [System.IO.Ports.Parity]::None, 8, [System.IO.Ports.StopBits]::One
    $port.ReadTimeout = 500
    $port.WriteTimeout = 500
    $port.Open()
    Write-Host "[+] [PUERTO ABIERTO] Escuchando comunicacion de la pantalla en $portName..." -ForegroundColor Green
    Write-Host "    --> Toca cualquier boton en la pantalla tactil (BOMBA / DOSIFICADOR)..." -ForegroundColor White
    Write-Host "    --> Presiona Ctrl + C para detener la prueba en cualquier momento." -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "[-] ERROR abriendo $portName : $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "    Verifica que el cable este conectado y que ningun otro programa tenga tomado el puerto." -ForegroundColor Yellow
    exit
}

$buffer = New-Object byte[] 256

try {
    while ($true) {
        if ($port.BytesToRead -gt 0) {
            $bytesRead = $port.Read($buffer, 0, [Math]::Min($port.BytesToRead, $buffer.Length))
            if ($bytesRead -gt 0) {
                $hex = ($buffer[0..($bytesRead - 1)] | ForEach-Object { $_.ToString("X2") }) -join " "
                $time = Get-Date -Format "HH:mm:ss.fff"
                Write-Host "[$time] [RECIBIDO DE PANTALLA]: $hex" -ForegroundColor Green

                # Si es un comando de escritura Modbus (Funcion 05 o 06), le respondemos el eco
                # para que la pantalla confirme que el PLC recibio la orden
                if ($bytesRead -ge 6 -and ($buffer[1] -eq 5 -or $buffer[1] -eq 6)) {
                    $port.Write($buffer, 0, $bytesRead)
                    Write-Host "[$time] [ECO ENVIADO A PANTALLA]: Orden confirmada" -ForegroundColor Cyan
                }
            }
        }
        Start-Sleep -Milliseconds 50
    }
} finally {
    if ($port.IsOpen) {
        $port.Close()
        Write-Host ""
        Write-Host "[*] Puerto $portName cerrado." -ForegroundColor Yellow
    }
}
