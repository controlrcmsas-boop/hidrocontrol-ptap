# Tags PLC - HIDROCONTROL PTAP

## PLC definido

| Campo | Valor |
|---|---|
| PLC | Siemens S7-1200 |
| CPU | CPU 1214C |
| Referencia | 214-1BG40-0XB0 |
| Software | TIA Portal V17 |
| IP PLC | 192.168.1.110 |
| Modulo adicional | SM1231 8AI |
| Protocolo elegido | S7 nativo |

## Criterio de nombres

Para el demo usaremos tres nombres por senal:

- Nombre visible: texto amigable para pantalla SCADA.
- Abreviado: codigo corto de planta, por ejemplo B1, S1, S2.
- tag_id: nombre tecnico usado por Node-RED, SQL y dashboard.

Regla recomendada para tag_id:

```text
minusculas_sin_espacios
```

## Matriz inicial de senales

| item | nombre_visible | abreviado | tag_id | clase | tipo_senal | entrada_fisica | direccion_plc | tipo_dato_s7 | unidad | lectura_escritura | historizar | alarma | descripcion |
|---:|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Bomba principal | B1 | bomba_principal_estado | motor | digital | CPU digital | Por definir | BOOL | - | lectura | si | si | Estado de marcha/falla de la bomba principal |
| 2 | Dosificador sulfato | B2 | dosificador_sulfato_estado | motor | digital | CPU digital | Por definir | BOOL | - | lectura | si | si | Estado del dosificador de sulfato |
| 3 | Dosificador cloro | B3 | dosificador_cloro_estado | motor | digital | CPU digital | Por definir | BOOL | - | lectura | si | si | Estado del dosificador de cloro |
| 4 | Sensor nivel tanque agua cruda | S1 | nivel_tanque_agua_cruda | sensor | digital | CPU digital | Por definir | BOOL | - | lectura | si | si | Nivel/estado de tanque de agua cruda |
| 5 | Sensor pH agua cruda | S2 | ph_agua_cruda | sensor | analogica | SM1231 AI0 | Por definir | REAL | pH | lectura | si | si | Medicion de pH en agua cruda |
| 6 | Sensor turbidez agua cruda | S3 | turbidez_agua_cruda | sensor | analogica | SM1231 AI1 | Por definir | REAL | NTU | lectura | si | si | Medicion de turbidez en agua cruda |
| 7 | Sensor conductividad agua cruda | S4 | conductividad_agua_cruda | sensor | analogica | SM1231 AI2 | Por definir | REAL | uS/cm | lectura | si | si | Medicion de conductividad en agua cruda |
| 8 | Sensor pH agua tratada | S5 | ph_agua_tratada | sensor | analogica | SM1231 AI3 | Por definir | REAL | pH | lectura | si | si | Medicion de pH en agua tratada |
| 9 | Sensor turbidez agua tratada | S6 | turbidez_agua_tratada | sensor | analogica | SM1231 AI4 | Por definir | REAL | NTU | lectura | si | si | Medicion de turbidez en agua tratada |
| 10 | Sensor conductividad agua tratada | S7 | conductividad_agua_tratada | sensor | analogica | SM1231 AI5 | Por definir | REAL | uS/cm | lectura | si | si | Medicion de conductividad en agua tratada |
| 11 | Sensor caudal B1 | S8 | caudal_bomba_principal | sensor | analogica | SM1231 AI6 | Por definir | REAL | L/min | lectura | si | si | Caudal asociado a bomba principal |
| 12 | Sensor caudal distribucion | S9 | caudal_distribucion | sensor | analogica | SM1231 AI7 | Por definir | REAL | L/min | lectura | si | si | Caudal hacia distribucion |
| 13 | Presion B1 | S10 | presion_bomba_principal | sensor | analogica | Pendiente canal | Por definir | REAL | bar | lectura | si | si | Requiere canal adicional o reubicacion |

## Comandos confirmados

| comando | equipo | tag_id_comando | descripcion |
|---|---|---|---|
| start | B1 | comando_bomba_principal_start | Prender bomba principal |
| stop | B1 | comando_bomba_principal_stop | Apagar bomba principal |
| start | B2 | comando_dosificador_sulfato_start | Prender dosificador sulfato |
| stop | B2 | comando_dosificador_sulfato_stop | Apagar dosificador sulfato |
| start | B3 | comando_dosificador_cloro_start | Prender dosificador cloro |
| stop | B3 | comando_dosificador_cloro_stop | Apagar dosificador cloro |

## Pendiente para TIA Portal

- Crear o confirmar DB de comunicacion para SCADA.
- Definir si el DB sera no optimizado para lectura S7 desde Node-RED.
- Asignar direcciones reales, por ejemplo DB10.DBX0.0, DB10.DBD4, etc.
- Confirmar tipo electrico de sensores: 0-5 V inicial, configurable en TIA.
- Definir escalas analogicas desde PLC: pH, NTU, uS/cm, L/min, bar.
- Resolver canal para presion B1.
- Definir limites de alarma baja/alta.
- Definir estados de falla, manual/auto y permiso remoto para motores.
