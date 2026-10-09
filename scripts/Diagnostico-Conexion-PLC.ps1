Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "    HIDROCONTROL PTAP V2.2 - DIAGNOSTICO DE ENLACE CON PLC SIEMENS (192.168.1.110)" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/4] Inspeccionando adaptadores de red de este computador..." -ForegroundColor Cyan
$adapters = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -notlike '*Loopback*' }
$hasPlcSubnet = $false

foreach ($a in $adapters) {
    $ip = $a.IPAddress
    $alias = $a.InterfaceAlias
    $status = "OTRA RED"
    $color = "Gray"
    if ($ip -like "192.168.1.*") {
        $hasPlcSubnet = $true
        $status = "RANGO CORRECTO PARA PLC (192.168.1.x)"
        $color = "Green"
    } elseif ($ip -like "169.254.*") {
        $status = "APIPA (SIN DHCP - REQUIERE IP ESTATICA)"
        $color = "Red"
    }
    Write-Host ("   - Adaptador: " + $alias + " | IP: " + $ip + " [" + $status + "]") -ForegroundColor $color
}
Write-Host ""

if (-not $hasPlcSubnet) {
    Write-Host "   [!] ALERTA CRITICA DE RED:" -ForegroundColor Red
    Write-Host "       Ninguna tarjeta de red tiene una IP en la subred 192.168.1.xxx." -ForegroundColor Yellow
    Write-Host "       Windows esta usando DHCP o APIPA (169.254.x.x) y no puede comunicarse con el PLC." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "       SOLUCION:" -ForegroundColor Cyan
    Write-Host "       1. Ve a 'Configuracion de red e Internet' -> 'Cambiar opciones del adaptador'." -ForegroundColor Cyan
    Write-Host "       2. Clic derecho en tu tarjeta Ethernet -> Propiedades." -ForegroundColor Cyan
    Write-Host "       3. Protocolo de Internet version 4 (TCP/IPv4) -> Propiedades." -ForegroundColor Cyan
    Write-Host "       4. Asigna IP estatica: 192.168.1.50 | Mascara: 255.255.255.0" -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host "   [+] Tarjeta de red en el rango correcto 192.168.1.xxx." -ForegroundColor Green
    Write-Host ""
}

Write-Host "[2/4] Probando PING fisico a 192.168.1.110 (PLC Siemens S7-1200)..." -ForegroundColor Cyan
$ping = Test-Connection -ComputerName 192.168.1.110 -Count 2 -Quiet -ErrorAction SilentlyContinue
if ($ping) {
    Write-Host "   [+] [PING EXITOSO] El PLC Siemens responde en red." -ForegroundColor Green
} else {
    Write-Host "   [-] [PING FALLIDO] El PLC 192.168.1.110 NO responde a ping." -ForegroundColor Red
    Write-Host "       Posibles causas:" -ForegroundColor Yellow
    Write-Host "       a) El cable de red Ethernet no esta bien enchufado al puerto del PC." -ForegroundColor Gray
    Write-Host "       b) La tarjeta de red del PC no tiene IP estatica (ej: 192.168.1.50)." -ForegroundColor Gray
    Write-Host "       c) En TIA Portal cambiaron la IP del PLC a otra diferente." -ForegroundColor Gray
    Write-Host "       d) El PLC esta apagado o en reinicio." -ForegroundColor Gray
}
Write-Host ""

Write-Host "[3/4] Probando Puerto S7 (TCP 102 - ISO on TCP)..." -ForegroundColor Cyan
try {
    $tcp = Test-NetConnection -ComputerName 192.168.1.110 -Port 102 -WarningAction SilentlyContinue
    if ($tcp.TcpTestSucceeded) {
        Write-Host "   [+] [PUERTO 102 ABIERTO] Comunicacion S7 activa y lista para Node-RED." -ForegroundColor Green
    } else {
        Write-Host "   [-] [PUERTO 102 CERRADO O INACCESIBLE]" -ForegroundColor Red
        Write-Host "       [!] ESTE ES EL ORIGEN DEL ERROR DE NODE-RED: timeout connecting to the transport." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "       CAUSA PRINCIPAL TRAS PROGRAMAR CON TIA PORTAL:" -ForegroundColor White
        Write-Host "       En TIA Portal NO habilitaron la casilla PUT/GET en la CPU:" -ForegroundColor Yellow
        Write-Host "       1. Abrir proyecto en TIA Portal V17." -ForegroundColor Cyan
        Write-Host "       2. Clic derecho en PLC_1 -> Propiedades -> Proteccion y seguridad." -ForegroundColor Cyan
        Write-Host "       3. Seccion 'Mecanismos de conexion' -> Marcar la casilla:" -ForegroundColor Cyan
        Write-Host "          'Permitir acceso mediante comunicacion PUT/GET del interlocutor remoto'." -ForegroundColor Yellow
        Write-Host "       4. Guardar, compilar Hardware y descargar al PLC." -ForegroundColor Cyan
    }
} catch {
    Write-Host ("   [-] Error probando puerto 102: " + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ""

Write-Host "[4/4] Probando estado de Node-RED local (Puerto 1880)..." -ForegroundColor Cyan
try {
    $nr = Invoke-WebRequest -Uri "http://127.0.0.1:1880" -UseBasicParsing -TimeoutSec 2
    if ($nr.StatusCode -eq 200) {
        Write-Host "   [+] Node-RED esta corriendo en http://127.0.0.1:1880." -ForegroundColor Green
    }
} catch {
    Write-Host "   [!] Node-RED no esta respondiendo en el puerto 1880." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
