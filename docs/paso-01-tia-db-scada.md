# Paso 01 - Crear DBs SCADA en TIA Portal V17

## Objetivo

Crear los bloques de datos que usara Node-RED para comunicarse con el PLC Siemens S7-1200 CPU 1214C por S7 nativo.

PLC:

```text
CPU 1214C
Referencia: 214-1BG40-0XB0
IP: 192.168.1.110
TIA Portal V17
Modulo analogico: SM1231 8AI
```

## Bloques recomendados

Crear dos DB separados:

```text
DB10_SCADA_READ
DB11_SCADA_WRITE
```

Uso:

```text
DB10_SCADA_READ   PLC -> Node-RED / Dashboard
DB11_SCADA_WRITE  Node-RED / Dashboard -> PLC
```

Esto mantiene separadas las lecturas de los comandos y reduce errores.

## Paso A - Crear DB10_SCADA_READ

En TIA Portal:

1. Abrir el proyecto del PLC.
2. Ir a `Program blocks`.
3. Crear nuevo `Data block`.
4. Nombre sugerido: `DB10_SCADA_READ`.
5. Tipo: Global DB.
6. Desactivar acceso optimizado si Node-RED requiere direcciones absolutas.
7. Crear las variables segun la tabla de este documento.
8. Compilar y descargar al PLC.

## Variables DB10_SCADA_READ

| offset sugerido | nombre TIA | tipo | tag_id Node-RED | descripcion |
|---|---|---|---|---|
| DB10.DBX0.0 | bomba_principal_estado | Bool | bomba_principal_estado | Estado real B1 |
| DB10.DBX0.1 | dosificador_sulfato_estado | Bool | dosificador_sulfato_estado | Estado real B2 |
| DB10.DBX0.2 | dosificador_cloro_estado | Bool | dosificador_cloro_estado | Estado real B3 |
| DB10.DBX0.3 | nivel_tanque_agua_cruda | Bool | nivel_tanque_agua_cruda | Sensor digital nivel S1 |
| DB10.DBX0.4 | modo_remoto_habilitado | Bool | modo_remoto_habilitado | Permite comandos remotos |
| DB10.DBX0.5 | plc_en_falla | Bool | plc_en_falla | Falla general PLC/proceso |
| DB10.DBX0.6 | comando_rechazado | Bool | comando_rechazado | PLC rechazo ultimo comando |
| DB10.DBX0.7 | ultimo_comando_aceptado | Bool | ultimo_comando_aceptado | PLC acepto ultimo comando |
| DB10.DBD4 | ph_agua_cruda | Real | ph_agua_cruda | S2 escalado en pH |
| DB10.DBD8 | turbidez_agua_cruda | Real | turbidez_agua_cruda | S3 escalado en NTU |
| DB10.DBD12 | conductividad_agua_cruda | Real | conductividad_agua_cruda | S4 escalado en uS/cm |
| DB10.DBD16 | ph_agua_tratada | Real | ph_agua_tratada | S5 escalado en pH |
| DB10.DBD20 | turbidez_agua_tratada | Real | turbidez_agua_tratada | S6 escalado en NTU |
| DB10.DBD24 | conductividad_agua_tratada | Real | conductividad_agua_tratada | S7 escalado en uS/cm |
| DB10.DBD28 | caudal_bomba_principal | Real | caudal_bomba_principal | S8 escalado en L/min |
| DB10.DBD32 | caudal_distribucion | Real | caudal_distribucion | S9 escalado en L/min |
| DB10.DBD36 | presion_bomba_principal | Real | presion_bomba_principal | S10 escalado en bar, pendiente canal fisico |
| DB10.DBD40 | watchdog_plc | DInt | watchdog_plc | Contador vivo desde PLC |

## Paso B - Crear DB11_SCADA_WRITE

En TIA Portal:

1. Crear nuevo `Data block`.
2. Nombre sugerido: `DB11_SCADA_WRITE`.
3. Tipo: Global DB.
4. Desactivar acceso optimizado si Node-RED requiere direcciones absolutas.
5. Crear las variables segun la tabla.
6. El PLC debe consumir estos bits como solicitudes, no como salidas directas.

## Variables DB11_SCADA_WRITE

| offset sugerido | nombre TIA | tipo | tag_id Node-RED | descripcion |
|---|---|---|---|---|
| DB11.DBX0.0 | comando_bomba_principal_start | Bool | comando_bomba_principal_start | Solicitud prender B1 |
| DB11.DBX0.1 | comando_bomba_principal_stop | Bool | comando_bomba_principal_stop | Solicitud apagar B1 |
| DB11.DBX0.2 | comando_dosificador_sulfato_start | Bool | comando_dosificador_sulfato_start | Solicitud prender B2 |
| DB11.DBX0.3 | comando_dosificador_sulfato_stop | Bool | comando_dosificador_sulfato_stop | Solicitud apagar B2 |
| DB11.DBX0.4 | comando_dosificador_cloro_start | Bool | comando_dosificador_cloro_start | Solicitud prender B3 |
| DB11.DBX0.5 | comando_dosificador_cloro_stop | Bool | comando_dosificador_cloro_stop | Solicitud apagar B3 |
| DB11.DBX0.6 | reset_alarmas | Bool | reset_alarmas | Solicitud reset/ack alarmas |
| DB11.DBX0.7 | watchdog_scada | Bool | watchdog_scada | Pulso vivo desde Node-RED |
| DB11.DBD4 | command_sequence | DInt | command_sequence | Consecutivo de comando desde Node-RED |

## Logica recomendada en PLC

Los comandos desde SCADA deben tratarse como pulsos o solicitudes temporales.

Ejemplo conceptual:

```text
Si modo_remoto_habilitado = TRUE
Y plc_en_falla = FALSE
Y comando_bomba_principal_start = TRUE
Entonces solicitar arranque B1
```

Luego el PLC debe limpiar/ignorar el comando segun la logica que definas, y Node-RED debe escribir el bit en `FALSE` despues de un pulso corto.

## Reglas de seguridad

1. El PLC conserva la autoridad final.
2. Node-RED no debe accionar salidas directas, solo solicitudes.
3. Toda orden remota debe quedar registrada en SQL.
4. El dashboard debe pedir confirmacion antes de prender/apagar.
5. Si `modo_remoto_habilitado = FALSE`, Node-RED debe rechazar comandos.
6. Si `plc_en_falla = TRUE`, Node-RED debe rechazar comandos.

## Configuracion de comunicacion S7 para Node-RED

Datos base:

```text
PLC IP: 192.168.1.110
Rack: 0
Slot: 1
Protocolo: S7 nativo
```

Para S7-1200 normalmente se usa `Rack 0 / Slot 1`.

## Pendiente antes de Node-RED

Confirmar en TIA:

- DB10 y DB11 creados.
- Direcciones absolutas disponibles.
- Acceso PUT/GET habilitado si el nodo S7 lo requiere.
- PLC accesible por ping desde el PC local.
- Valores analogicos escalados como REAL.
