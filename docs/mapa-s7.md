# Mapa S7 nativo - HIDROCONTROL PTAP

## Objetivo

Definir un DB de comunicacion en TIA Portal V17 para que Node-RED lea y escriba datos de forma ordenada usando protocolo S7 nativo.

## Recomendacion

Crear dos bloques de datos dedicados:

```text
DB10_SCADA_READ
DB11_SCADA_WRITE
```

Separar lectura y escritura reduce confusion y hace mas seguro el mando remoto.

## DB10_SCADA_READ - PLC hacia Node-RED

| direccion sugerida | tag_id | tipo_s7 | descripcion |
|---|---|---|---|
| DB10.DBX0.0 | bomba_principal_estado | BOOL | Estado real B1 |
| DB10.DBX0.1 | dosificador_sulfato_estado | BOOL | Estado real B2 |
| DB10.DBX0.2 | dosificador_cloro_estado | BOOL | Estado real B3 |
| DB10.DBX0.3 | nivel_tanque_agua_cruda | BOOL | Sensor digital S1 |
| DB10.DBD4 | ph_agua_cruda | REAL | Sensor S2 escalado |
| DB10.DBD8 | turbidez_agua_cruda | REAL | Sensor S3 escalado |
| DB10.DBD12 | conductividad_agua_cruda | REAL | Sensor S4 escalado |
| DB10.DBD16 | ph_agua_tratada | REAL | Sensor S5 escalado |
| DB10.DBD20 | turbidez_agua_tratada | REAL | Sensor S6 escalado |
| DB10.DBD24 | conductividad_agua_tratada | REAL | Sensor S7 escalado |
| DB10.DBD28 | caudal_bomba_principal | REAL | Sensor S8 escalado |
| DB10.DBD32 | caudal_distribucion | REAL | Sensor S9 escalado |
| DB10.DBD36 | presion_bomba_principal | REAL | Sensor S10 escalado |

## DB11_SCADA_WRITE - Node-RED hacia PLC

| direccion sugerida | tag_id | tipo_s7 | descripcion |
|---|---|---|---|
| DB11.DBX0.0 | comando_bomba_principal_start | BOOL | Solicitud prender B1 |
| DB11.DBX0.1 | comando_bomba_principal_stop | BOOL | Solicitud apagar B1 |
| DB11.DBX0.2 | comando_dosificador_sulfato_start | BOOL | Solicitud prender B2 |
| DB11.DBX0.3 | comando_dosificador_sulfato_stop | BOOL | Solicitud apagar B2 |
| DB11.DBX0.4 | comando_dosificador_cloro_start | BOOL | Solicitud prender B3 |
| DB11.DBX0.5 | comando_dosificador_cloro_stop | BOOL | Solicitud apagar B3 |

## Senales recomendadas de seguridad

Se recomienda agregar luego:

| tag_id | tipo_s7 | descripcion |
|---|---|---|
| modo_remoto_habilitado | BOOL | Permite comandos desde dashboard |
| plc_en_falla | BOOL | Falla general |
| comunicacion_scada_ok | BOOL | Pulso/watchdog desde Node-RED |
| comando_rechazado | BOOL | PLC rechazo comando por condicion insegura |
| ultimo_comando_aceptado | BOOL | Confirmacion de comando aceptado |

## Nota de seguridad

Los comandos remotos deben ser solicitudes, no salidas directas. El PLC debe decidir si ejecuta con base en permisos, modo remoto, fallas, enclavamientos y condiciones de proceso.
