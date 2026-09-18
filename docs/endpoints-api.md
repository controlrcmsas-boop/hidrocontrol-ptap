# API Node-RED - HIDROCONTROL PTAP

## PLC y protocolo

- PLC: Siemens S7-1200 CPU 1214C, referencia 214-1BG40-0XB0.
- IP PLC: 192.168.1.110.
- Protocolo: S7 nativo.
- Dashboard Blazor: http://localhost:5088
- Node-RED: http://localhost:1880

## Telemetria actual

```http
GET /api/telemetry/current
```

## Estado del sistema

```http
GET /api/system/status
```

## Historicos de calidad

```http
GET /api/telemetry/quality-history?tag=ph_agua_tratada&limit=200
```

Tags principales:

```text
ph_agua_cruda
turbidez_agua_cruda
conductividad_agua_cruda
ph_agua_tratada
turbidez_agua_tratada
conductividad_agua_tratada
```

## Alarmas

```http
GET /api/alarms/active
GET /api/alarms/history?limit=100
GET /api/alarm-limits
POST /api/alarm-limits
```

Ejemplo para modificar un limite:

```json
{
  "id": 1,
  "enabled": true,
  "severity": "high",
  "limit_low": 6.5,
  "limit_high": 8.5,
  "bool_alarm_value": null,
  "message": "pH de agua cruda fuera de limite"
}
```

Alarmas iniciales configuradas:

| variable | tipo | condicion inicial |
|---|---|---|
| ph_agua_cruda | range | menor a 6.5 o mayor a 8.5 |
| ph_agua_tratada | range | menor a 6.5 o mayor a 8.5 |
| turbidez_agua_cruda | high | mayor a 20 NTU |
| turbidez_agua_tratada | high | mayor a 5 NTU |
| cloro_residual | range | preparado, deshabilitado hasta agregar sensor |
| bomba_principal_estado | bool_equals | alarma si FALSE |
| nivel_tanque_agua_cruda | bool_equals | alarma si TRUE |

## Prender/apagar equipo

```http
POST /api/control/command
```

Cuerpo para prender:

```json
{
  "target": "bomba_principal",
  "command": "start",
  "operator": "admin",
  "source": "dashboard"
}
```

Targets/comandos permitidos:

| target | command | descripcion |
|---|---|---|
| bomba_principal | start | Prender B1 |
| bomba_principal | stop | Apagar B1 |
| dosificador_sulfato | start | Prender B2 |
| dosificador_sulfato | stop | Apagar B2 |
| dosificador_cloro | start | Prender B3 |
| dosificador_cloro | stop | Apagar B3 |

## Regla clave

El dashboard no escribe directamente al PLC. Todo comando entra por Node-RED, se valida, se registra y luego se envia al PLC.
