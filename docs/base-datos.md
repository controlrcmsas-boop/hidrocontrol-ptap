# Base de datos - HIDROCONTROL PTAP

## Motor recomendado

Opcion A: PostgreSQL.
Opcion B: SQL Server Express si se prefiere ecosistema Microsoft.

Para este demo, PostgreSQL es muy buena opcion para historicos. SQL Server Express tambien es valido si se quiere mantener todo mas Microsoft/Visual Studio.

## Tablas iniciales

```text
tags
telemetry_history
quality_history
alarm_events
command_log
equipment_events
system_events
```

## Historicos principales

### quality_history

Tabla recomendada para reportes de calidad de agua.

```text
id
tag_id
value_numeric
unit
quality
timestamp
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

### telemetry_history

Historico general de proceso.

```text
id
tag_id
value_numeric
value_bool
quality
timestamp
```

Tags principales:

```text
caudal_bomba_principal
caudal_distribucion
presion_bomba_principal
nivel_tanque_agua_cruda
```

### command_log

Registro de comandos enviados desde el dashboard.

```text
id
target
command
payload
operator
source
status
requested_at
executed_at
result_message
```

Ejemplos:

```text
bomba_principal start
dosificador_sulfato stop
dosificador_cloro start
```

### equipment_events

Eventos de cambio de estado de equipos.

```text
id
equipment_id
event_type
previous_state
new_state
source
timestamp
message
```

Ejemplos:

```text
B1 encendida
B1 apagada
B1 falla
B2 encendida
B3 apagada
```

### alarm_events

Eventos de alarma.

```text
id
tag_id
alarm_type
value_numeric
message
state
started_at
acknowledged_at
cleared_at
operator
```
