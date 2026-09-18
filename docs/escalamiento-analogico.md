# Escalamiento analogico - HIDROCONTROL PTAP

## Entrada electrica inicial

Las senales analogicas se consideran inicialmente como:

```text
0-5 V
```

Esta condicion queda abierta a modificacion en TIA Portal.

## Filosofia de diseno

El PLC debe hacer el escalamiento principal y entregar a SCADA valores en unidades de ingenieria.

```text
Valor electrico 0-5 V
  -> Entrada analogica S7-1200 / SM1231
  -> Normalizacion en TIA
  -> Escalamiento a unidades reales
  -> DB10_SCADA_READ como REAL
  -> Node-RED
  -> SQL / Dashboard
```

## Ventajas

- Node-RED queda mas simple.
- Dashboard no depende de formulas de sensores.
- El PLC mantiene criterio industrial de validacion.
- Si cambia el sensor, se ajusta en TIA sin reescribir dashboard.

## Rangos iniciales sugeridos

Estos rangos son base de demo y deben ajustarse a los transmisores reales.

| tag_id | entrada inicial | rango ingenieria sugerido | unidad | observacion |
|---|---|---|---|---|
| ph_agua_cruda | 0-5 V | 0 a 14 | pH | Confirmar transmisor |
| turbidez_agua_cruda | 0-5 V | 0 a 100 | NTU | Ajustable |
| conductividad_agua_cruda | 0-5 V | 0 a 2000 | uS/cm | Ajustable |
| ph_agua_tratada | 0-5 V | 0 a 14 | pH | Confirmar transmisor |
| turbidez_agua_tratada | 0-5 V | 0 a 100 | NTU | Ajustable |
| conductividad_agua_tratada | 0-5 V | 0 a 2000 | uS/cm | Ajustable |
| caudal_bomba_principal | 0-5 V | 0 a 100 | L/min | Ajustar a caudalimetro |
| caudal_distribucion | 0-5 V | 0 a 100 | L/min | Ajustar a caudalimetro |
| presion_bomba_principal | 0-5 V | 0 a 10 | bar | Pendiente canal |

## Formula conceptual

```text
valor_ingenieria = ((valor_voltaje - 0) / (5 - 0)) * (rango_max - rango_min) + rango_min
```

Ejemplo pH:

```text
0 V = 0 pH
5 V = 14 pH
```

## Recomendacion para TIA

Crear variables internas escaladas tipo REAL y publicar solo esas variables al DB de SCADA.

Ejemplo:

```text
AI0_RAW -> ph_agua_cruda_REAL -> DB10_SCADA_READ.ph_agua_cruda
```

## Validacion recomendada

Agregar banderas de calidad por sensor en una fase siguiente:

```text
ph_agua_cruda_quality_ok
ph_agua_cruda_out_of_range
ph_agua_cruda_sensor_fault
```
