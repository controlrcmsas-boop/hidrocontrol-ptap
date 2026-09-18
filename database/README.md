# Base de datos HIDROCONTROL PTAP

Motor seleccionado: PostgreSQL 16.

## Base creada

```text
hidrocontrol_ptap
```

## Usuario de aplicacion para Node-RED

```text
Host: 127.0.0.1
Port: 5432
Database: hidrocontrol_ptap
User: hidrocontrol_app
Password: hidrocontrol2026
```

## Usuario administrador usado para instalacion

```text
User: postgres
Password: adminptap
```

## Scripts

Ejecutados/creados en este orden:

```text
00-create-database.sql
01-schema.sql
02-seed-tags.sql
03-app-user.sql
04-alarm-limits.sql
05-notification-emails.sql
06-critical-email-notification-state.sql
99-verify.sql
```

## Tablas creadas

```text
tags
telemetry_history
quality_history
command_log
alarm_events
notification_emails
equipment_events
system_events
```

## Vistas

```text
v_latest_quality
v_latest_telemetry
```

## Historicos de calidad

Tags principales para `quality_history`:

```text
ph_agua_cruda
turbidez_agua_cruda
conductividad_agua_cruda
ph_agua_tratada
turbidez_agua_tratada
conductividad_agua_tratada
```

## Proceso

Tags principales para `telemetry_history`:

```text
caudal_bomba_principal
caudal_distribucion
presion_bomba_principal
nivel_tanque_agua_cruda
bomba_principal_estado
dosificador_sulfato_estado
dosificador_cloro_estado
modo_remoto_habilitado
plc_en_falla
```

## Flow Node-RED de historicos

Importar:

```text
nodered/flows/hidrocontrol-ptap-history-flow.json
```

Endpoint agregado:

```http
GET /api/telemetry/quality-history?tag=ph_agua_tratada&limit=200
```
