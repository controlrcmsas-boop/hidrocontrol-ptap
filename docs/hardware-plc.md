# Hardware PLC - HIDROCONTROL PTAP

## CPU

| Campo | Valor |
|---|---|
| Familia | Siemens S7-1200 |
| CPU | CPU 1214C |
| Referencia | 214-1BG40-0XB0 |
| Software | TIA Portal V17 |
| IP PLC | 192.168.1.110 |
| Comunicacion SCADA | S7 nativo |

## Modulos adicionales

| modulo | descripcion | uso previsto |
|---|---|---|
| SM1231 8AI | Modulo de 8 entradas analogicas | Sensores de calidad/proceso |

## Tipo de senal analogica

Condicion inicial definida:

```text
0-5 V
```

El diseno queda abierto para modificar el tipo/rango desde TIA Portal si luego se cambia a 0-10 V, 4-20 mA u otro acondicionamiento.

## Criterio recomendado de escalamiento

Node-RED y el dashboard no deberian recibir valores crudos de entrada analogica. El PLC debe entregar valores escalados en unidades de ingenieria dentro del DB de comunicacion SCADA.

Ejemplo:

```text
Entrada analogica cruda -> Escalamiento en TIA -> DB10.ph_agua_cruda = 7.15 REAL
```

Asi Node-RED lee valores ya listos:

```text
pH
NTU
uS/cm
L/min
bar
```

## Observacion importante

La lista inicial contiene estas senales analogicas/proceso:

1. pH agua cruda.
2. Turbidez agua cruda.
3. Conductividad agua cruda.
4. pH agua tratada.
5. Turbidez agua tratada.
6. Conductividad agua tratada.
7. Caudal B1.
8. Caudal distribucion.
9. Presion B1.

El modulo SM1231 8AI tiene 8 entradas analogicas, por lo tanto hay 9 senales para 8 canales.

Opciones:

1. Usar 8 senales en el SM1231 8AI y dejar una en entrada analogica integrada de la CPU si esta disponible.
2. Usar un segundo modulo analogico.
3. Dejar una senal como calculada/simulada para la demo inicial.
4. Reducir temporalmente una variable analogica en fase 1.

## Propuesta inicial de canales SM1231 8AI

| canal | abreviado | tag_id | senal | tipo inicial | configurable en TIA | unidad | observacion |
|---|---|---|---|---|---|---|---|
| AI0 | S2 | ph_agua_cruda | pH agua cruda | 0-5 V | si | pH | Confirmar escala de transmisor |
| AI1 | S3 | turbidez_agua_cruda | Turbidez agua cruda | 0-5 V | si | NTU | Confirmar escala |
| AI2 | S4 | conductividad_agua_cruda | Conductividad agua cruda | 0-5 V | si | uS/cm | Confirmar escala |
| AI3 | S5 | ph_agua_tratada | pH agua tratada | 0-5 V | si | pH | Confirmar escala de transmisor |
| AI4 | S6 | turbidez_agua_tratada | Turbidez agua tratada | 0-5 V | si | NTU | Confirmar escala |
| AI5 | S7 | conductividad_agua_tratada | Conductividad agua tratada | 0-5 V | si | uS/cm | Confirmar escala |
| AI6 | S8 | caudal_bomba_principal | Caudal B1 | 0-5 V | si | L/min | Confirmar escala |
| AI7 | S9 | caudal_distribucion | Caudal distribucion | 0-5 V | si | L/min | Confirmar escala |
| Pendiente | S10 | presion_bomba_principal | Presion B1 | 0-5 V | si | bar | Requiere canal adicional o reubicar |

## Senales digitales CPU

| abreviado | tag_id | senal | tipo |
|---|---|---|---|
| B1 | bomba_principal_estado | Bomba principal | Digital |
| B2 | dosificador_sulfato_estado | Dosificador sulfato | Digital |
| B3 | dosificador_cloro_estado | Dosificador cloro | Digital |
| S1 | nivel_tanque_agua_cruda | Nivel tanque agua cruda | Digital |

## Pendiente por confirmar

- Rango real de ingenieria por sensor.
- Si la presion B1 ira en entrada integrada de CPU, segundo modulo o queda para fase 2.
- Configuracion exacta del modulo SM1231 en TIA Portal.
