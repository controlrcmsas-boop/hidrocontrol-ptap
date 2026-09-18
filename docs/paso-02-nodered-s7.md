# Paso 02 - Node-RED S7 nativo

Este paso se realiza despues de crear y descargar los DB en TIA Portal.

## Objetivo

Configurar Node-RED para leer `DB10_SCADA_READ` y escribir comandos en `DB11_SCADA_WRITE` usando S7 nativo.

## Datos de conexion PLC

```text
IP: 192.168.1.110
Rack: 0
Slot: 1
```

## Nodo recomendado

Para S7 nativo en Node-RED se puede usar un nodo Siemens S7 compatible, por ejemplo:

```text
node-red-contrib-s7
```

## Lecturas iniciales

Leer periodicamente:

```text
DB10.DBX0.0 a DB10.DBX0.7
DB10.DBD4 a DB10.DBD40
```

Frecuencia inicial recomendada:

```text
1 segundo para estados actuales
10 a 60 segundos para historicos SQL, segun necesidad
```

## Escritura de comandos

Los comandos deben escribirse como pulso:

```text
1. Escribir bit de comando en TRUE.
2. Esperar 300 a 1000 ms.
3. Escribir bit de comando en FALSE.
4. Leer estado real en DB10 para confirmar.
5. Registrar resultado en SQL.
```

## Primer endpoint a crear

```http
GET /api/telemetry/current
```

Debe devolver los valores actuales de DB10.

## Segundo endpoint a crear

```http
POST /api/control/command
```

Debe validar target y comando antes de escribir DB11.
