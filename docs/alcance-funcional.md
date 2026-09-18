# Alcance funcional - HIDROCONTROL PTAP

## Alcance inicial confirmado

El sistema demo debe permitir:

1. Leer variables desde PLC Siemens S7-1200 por S7 nativo.
2. Encender y apagar equipos desde el dashboard, pasando siempre por Node-RED.
3. Guardar historicos de calidad de agua.
4. Mostrar pantalla SCADA "PTAP Control" con imagen HIDROCONTROL.
5. Mostrar alarmas y eventos basicos.
6. Permitir acceso remoto seguro al dashboard.

## Equipos con mando remoto

| equipo | abreviado | comandos permitidos | condicion recomendada |
|---|---|---|---|
| Bomba principal | B1 | prender, apagar | Solo si modo remoto habilitado y sin falla |
| Dosificador sulfato | B2 | prender, apagar | Solo si modo remoto habilitado y sin falla |
| Dosificador cloro | B3 | prender, apagar | Solo si modo remoto habilitado y sin falla |

## Variables a leer

| abreviado | variable | tipo | uso principal |
|---|---|---|---|
| B1 | Bomba principal | Digital | Estado equipo |
| B2 | Dosificador sulfato | Digital | Estado equipo |
| B3 | Dosificador cloro | Digital | Estado equipo |
| S1 | Nivel tanque agua cruda | Digital | Estado/proteccion |
| S2 | pH agua cruda | Analogica | Calidad agua |
| S3 | Turbidez agua cruda | Analogica | Calidad agua |
| S4 | Conductividad agua cruda | Analogica | Calidad agua |
| S5 | pH agua tratada | Analogica | Calidad agua |
| S6 | Turbidez agua tratada | Analogica | Calidad agua |
| S7 | Conductividad agua tratada | Analogica | Calidad agua |
| S8 | Caudal B1 | Analogica | Proceso/operacion |
| S9 | Caudal distribucion | Analogica | Proceso/operacion |
| S10 | Presion B1 | Analogica | Proceso/proteccion |

## Historicos de calidad

Historizar principalmente:

| tag_id | unidad | historico |
|---|---|---|
| ph_agua_cruda | pH | si |
| turbidez_agua_cruda | NTU | si |
| conductividad_agua_cruda | uS/cm | si |
| ph_agua_tratada | pH | si |
| turbidez_agua_tratada | NTU | si |
| conductividad_agua_tratada | uS/cm | si |
| caudal_bomba_principal | L/min | si |
| caudal_distribucion | L/min | si |
| presion_bomba_principal | bar | si |

## Historicos de equipos

Guardar eventos de cambio de estado:

- B1 encendida/apagada/falla.
- B2 encendida/apagada/falla.
- B3 encendida/apagada/falla.
- Comando solicitado desde dashboard.
- Comando aceptado/rechazado por Node-RED/PLC.

## Regla de mando

El dashboard no escribe directo al PLC. El flujo correcto es:

```text
Dashboard
  -> Node-RED API
  -> Validacion de comando
  -> Registro en SQL
  -> Escritura S7 nativa al PLC
  -> PLC valida permisos/enclavamientos
  -> PLC actualiza estado real
  -> Node-RED lee confirmacion
  -> Dashboard muestra resultado
```
