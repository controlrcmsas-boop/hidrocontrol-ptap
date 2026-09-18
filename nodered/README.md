# Node-RED - HIDROCONTROL PTAP

Flujos recomendados para importar y mantener separados:

```text
nodered/flows/hidrocontrol-ptap-read-api-flow.json
nodered/flows/hidrocontrol-ptap-command-flow.json
nodered/flows/hidrocontrol-ptap-history-flow.json
nodered/flows/hidrocontrol-ptap-alarms-flow.json
```

No usar como flujo principal el archivo antiguo `hidrocontrol-ptap-flow.json`; se conserva solo como referencia inicial.

## Requisitos

Instalar en Node-RED:

```text
node-red-contrib-s7
node-red-contrib-postgresql
```

PLC configurado:

```text
IP: 192.168.1.110
Rack: 0
Slot: 1
PUT/GET habilitado en TIA Portal
DB10_SCADA_READ con acceso optimizado desactivado
DB11_SCADA_WRITE con acceso optimizado desactivado
```

## Offsets usados desde TIA

Lecturas DB10:

```text
DB10,X0.0    bomba_principal_estado
DB10,X0.1    dosificador_sulfato_estado
DB10,X0.2    dosificador_cloro_estado
DB10,X0.3    nivel_tanque_agua_cruda
DB10,X0.4    modo_remoto_habilitado
DB10,X0.5    plc_en_falla
DB10,X0.6    comando_rechazado
DB10,X0.7    ultimo_comando_aceptado
DB10,REAL2   ph_agua_cruda
DB10,REAL6   turbidez_agua_cruda
DB10,REAL10  conductividad_agua_cruda
DB10,REAL14  ph_agua_tratada
DB10,REAL18  turbidez_agua_tratada
DB10,REAL22  conductividad_agua_tratada
DB10,REAL26  caudal_bomba_principal
DB10,REAL30  caudal_distribucion
DB10,REAL34  presion_bomba_principal
DB10,DINT38  watchdog_plc
```

Comandos DB11:

```text
DB11,X0.0    comando_bomba_principal_start
DB11,X0.1    comando_bomba_principal_stop
DB11,X0.2    comando_dosificador_sulfato_start
DB11,X0.3    comando_dosificador_sulfato_stop
DB11,X0.4    comando_dosificador_cloro_start
DB11,X0.5    comando_dosificador_cloro_stop
DB11,X0.6    reset_alarmas
DB11,X0.7    watchdog_scada
DB11,DINT2   command_sequence
```

## Endpoints activos por flujo

Lectura/API:

```http
GET /api/telemetry/current
GET /api/system/status
```

Comandos:

```http
POST /api/control/command
GET /api/control/last-command
```

Historicos:

```http
GET /api/telemetry/quality-history?tag=ph_agua_tratada&limit=200
```

Alarmas:

```http
GET /api/alarms/active
GET /api/alarms/history?limit=100
GET /api/alarm-limits
POST /api/alarm-limits
GET /api/notification-emails
POST /api/notification-emails
```

## Notificacion por correo

La V2 agrega destinatarios en PostgreSQL mediante `notification_emails`.
Para envio real de correos criticos desde Node-RED se debe configurar SMTP en un nodo de correo y dispararlo cuando `severity = 'critical'`.

## Importar en Node-RED

1. Menu superior derecho.
2. Import.
3. Clipboard.
4. Copiar el contenido del flujo JSON requerido.
5. Import.
6. Deploy.

## Nota sobre comandos

El flujo valida `modo_remoto_habilitado` y `plc_en_falla` antes de aceptar comandos.

El flujo convierte los comandos en pulsos: escribe TRUE al bit de DB11, espera 500 ms y vuelve a escribir FALSE.
