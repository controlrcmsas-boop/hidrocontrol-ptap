param([string]$PortName="",[int]$BaudRate=9600)
$ErrorActionPreference="SilentlyContinue"

function Get-Crc([byte[]]$b,[int]$l){
    [uint16]$c=0xFFFF
    for($i=0;$i -lt $l;$i++){
        $c=$c -bxor [uint16]$b[$i]
        for($j=0;$j -lt 8;$j++){
            if(($c -band 1) -ne 0){$c=($c -shr 1) -bxor 0xA001}else{$c=$c -shr 1}
        }
    }
    return [byte[]]([byte]($c -band 0xFF),[byte](($c -shr 8) -band 0xFF))
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   HIDROCONTROL PTAP - RESPONDEDOR ESCLAVO MODBUS PARA HMI" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$availablePorts = [System.IO.Ports.SerialPort]::GetPortNames()
Write-Host "[INFO] Puertos COM detectados: $($availablePorts -join ', ')" -ForegroundColor Gray

if([string]::IsNullOrWhiteSpace($PortName)){
    if($availablePorts.Count -eq 1){
        $PortName = $availablePorts[0]
    }elseif($availablePorts -contains "COM4"){
        $PortName = "COM4"
    }elseif($availablePorts -contains "COM3"){
        $PortName = "COM3"
    }else{
        $PortName = "COM4"
    }
}

Write-Host "[+] Escuchando en $PortName a $BaudRate baudios..." -ForegroundColor Cyan

try{
    $p=New-Object System.IO.Ports.SerialPort $PortName,$BaudRate,"None",8,"One"
    $p.ReadTimeout=100
    $p.WriteTimeout=200
    $p.DtrEnable=$false
    $p.RtsEnable=$false
    $p.Open()
    Write-Host "[+] [PUERTO ABIERTO] Receptor listo. Esperando tramas de la pantalla..." -ForegroundColor Green
}catch{
    Write-Host "[-] ERROR abriendo $PortName : $($_.Exception.Message)" -ForegroundColor Red
    pause
    exit
}

$t=@{ph=7.0;turb=0.5;cond=0.0;b1=0;b2=0;b3=0}
$rxList=New-Object System.Collections.Generic.List[byte]
$lastPoll=0
$lastHeartbeat=0
$totalFrames=0

while($true){
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

    # 1. Consultar telemetria fresca de Node-RED cada 500ms
    if(($now - $lastPoll) -ge 500){
        $lastPoll=$now
        try{
            $r=Invoke-RestMethod -Uri "http://127.0.0.1:1880/api/telemetry/current" -TimeoutSec 1
            if($r -and $r.values){
                $v=$r.values
                $pVal=if($v.ph_agua_tratada -ne $null){[double]$v.ph_agua_tratada}elseif($v.ph_agua_cruda -ne $null){[double]$v.ph_agua_cruda}else{7.0}
                $tVal=if($v.turbidez_agua_tratada -ne $null){[double]$v.turbidez_agua_tratada}elseif($v.turbidez_agua_cruda -ne $null){[double]$v.turbidez_agua_cruda}else{0.5}
                $cVal=if($v.conductividad_agua_tratada -ne $null){[double]$v.conductividad_agua_tratada}elseif($v.conductividad_agua_cruda -ne $null){[double]$v.conductividad_agua_cruda}else{0.0}

                if($tVal -gt 1000 -or $tVal -lt 0){
                    $tVal=[Math]::Max(0.5,[Math]::Round(((485.0 - $tVal)*2.0),1))
                }
                if($pVal -lt 0.1){ $pVal=7.0 }

                $t.ph=[Math]::Round($pVal,2)
                $t.turb=[Math]::Round($tVal,1)
                $t.cond=[Math]::Round($cVal,1)

                $t.b1=if($v.bomba_principal_estado){1}else{0}
                $t.b2=if($v.dosificador_sulfato_estado){1}else{0}
                $t.b3=if($v.dosificador_cloro_estado){1}else{0}
            }
        }catch{}
    }

    # 2. Leer cualquier byte que llegue del puerto COM
    if($p.BytesToRead -gt 0){
        $bCount=$p.BytesToRead
        $tempBuf=New-Object byte[] $bCount
        $readCount=$p.Read($tempBuf,0,$bCount)
        for($i=0;$i -lt $readCount;$i++){
            $rxList.Add($tempBuf[$i])
        }
        $hexStr=($tempBuf[0..($readCount-1)] | ForEach-Object { $_.ToString("X2") }) -join " "
        Write-Host "[$([DateTime]::Now.ToString('HH:mm:ss.fff'))] [RX PANTALLA]: $hexStr" -ForegroundColor Yellow
    }

    # 3. Soporte para protocolo Mitsubishi FX (si la pantalla manda 0x05 ENQ)
    if($rxList.Count -eq 1 -and $rxList[0] -eq 0x05){
        $ack=New-Object byte[] 1
        $ack[0]=0x06
        $p.Write($ack,0,1)
        Write-Host "[$([DateTime]::Now.ToString('HH:mm:ss'))] [ACK ENVIADO]: Conexion FX establecida" -ForegroundColor Cyan
        $rxList.Clear()
    }

    # 4. Procesar peticion Modbus (Master pide datos)
    if($rxList.Count -ge 8){
        $id=$rxList[0]
        $fn=$rxList[1]
        $addr=($rxList[2] -shl 8) -bor $rxList[3]
        $cnt=($rxList[4] -shl 8) -bor $rxList[5]

        # Funcion 3 o 4: Lectura de Registros (pH, Turbidez, Conductividad)
        if($fn -eq 3 -or $fn -eq 4){
            $n=[Math]::Min($cnt,20)
            $bc=$n*2
            $out=New-Object byte[] (3+$bc+2)
            $out[0]=$id;$out[1]=$fn;$out[2]=[byte]$bc
            for($r=0;$r -lt $n;$r++){
                $cur=$addr+$r
                [int]$val=0
                if($cur -eq 0){$val=[int]($t.ph*100)}
                elseif($cur -eq 1){$val=[int]($t.turb*10)}
                elseif($cur -eq 2){$val=[int]$t.cond}
                elseif($cur -ge 10){
                    $ci=($cur - 10)*2
                    $m="Agente: Estable     "
                    $c1=if($ci -lt $m.Length){[byte][char]$m[$ci]}else{32}
                    $c2=if(($ci+1) -lt $m.Length){[byte][char]$m[$ci+1]}else{32}
                    $val=($c1 -shl 8) -bor $c2
                }
                $out[3+($r*2)]=[byte](($val -shr 8) -band 0xFF)
                $out[3+($r*2)+1]=[byte]($val -band 0xFF)
            }
            $crc=Get-Crc $out (3+$bc)
            $out[3+$bc]=$crc[0];$out[3+$bc+1]=$crc[1]
            $p.Write($out,0,$out.Length)
            $totalFrames++
            Write-Host "[$([DateTime]::Now.ToString('HH:mm:ss'))] [RESPUESTA A PANTALLA #$totalFrames] pH:$($t.ph) | Turb:$($t.turb) | Cond:$($t.cond)" -ForegroundColor Green
            $rxList.Clear()
        }
        # Funcion 1 o 2: Lectura de Coils (Actuadores Bomba, Sulfato, Cloro)
        elseif($fn -eq 1 -or $fn -eq 2){
            $cb=0
            if($t.b1){$cb=$cb -bor 1}
            if($t.b2){$cb=$cb -bor 2}
            if($t.b3){$cb=$cb -bor 4}
            $out=New-Object byte[] 6
            $out[0]=$id;$out[1]=$fn;$out[2]=1;$out[3]=[byte]$cb
            $crc=Get-Crc $out 4
            $out[4]=$crc[0];$out[5]=$crc[1]
            $p.Write($out,0,6)
            $totalFrames++
            Write-Host "[$([DateTime]::Now.ToString('HH:mm:ss'))] [RESPUESTA ACTUADORES #$totalFrames] Bomba:$($t.b1) | Sulf:$($t.b2) | Cloro:$($t.b3)" -ForegroundColor Green
            $rxList.Clear()
        }else{
            # Descartar primer byte si no coincide para buscar sincronia
            $rxList.RemoveAt(0)
        }
    }

    # 5. Latido informativo cada 5 segundos si no hay trafico
    if(($now - $lastHeartbeat) -ge 5000){
        $lastHeartbeat=$now
        if($totalFrames -eq 0){
            Write-Host "[$([DateTime]::Now.ToString('HH:mm:ss'))] [RECEPTOR ACTIVO] Esperando que la pantalla envie peticion..." -ForegroundColor Cyan
        }
    }

    Start-Sleep -Milliseconds 20
}
