# Dashboard PTAP Control

Proyecto Blazor Server creado para HIDROCONTROL PTAP.

## Solucion Visual Studio

Abrir en Visual Studio:

```text
PTAPControl/PTAPControl.sln
```

## URL local

Con el servidor iniciado:

```text
http://localhost:5088
```

## Endpoints Node-RED usados

```http
GET  http://localhost:1880/api/telemetry/current
GET  http://localhost:1880/api/system/status
POST http://localhost:1880/api/control/command
GET  http://localhost:1880/api/telemetry/quality-history
```

## Pantallas

```text
/             PTAP Control SCADA
/historicos   Historicos de calidad
```

## Configuracion

La URL base de Node-RED esta en:

```text
PTAPControl/appsettings.json
```

Clave:

```json
"NodeRed": {
  "BaseUrl": "http://localhost:1880/"
}
```
