# HIDROCONTROL PTAP

Proyecto demo nuevo para sistema SCADA PTAP con PLC Siemens S7-1200 real, Node-RED, base de datos SQL/PostgreSQL y dashboard desarrollado en Visual Studio.

## Objetivo

Crear una demo limpia, separada de proyectos anteriores, con arquitectura industrial ordenada:

```text
PLC Siemens S7-1200 real
  <-> Node-RED
  <-> PostgreSQL o SQL Server
  <-> Dashboard Visual Studio "PTAP Control"
  <-> Acceso remoto seguro
```

## Carpetas

```text
docs/                 Documentacion de arquitectura y decisiones
nodered/              Flujos, configuracion y notas de Node-RED
database/             Scripts SQL, esquema y migraciones
dashboard/            Proyecto Visual Studio del dashboard
assets/               Imagenes, iconos y referencias visuales
```

## Decision inicial recomendada

- Dashboard: ASP.NET Core Blazor Server en Visual Studio.
- Middleware: Node-RED.
- Base de datos: PostgreSQL o SQL Server Express.
- Comunicacion PLC: definir entre S7 nativo o Modbus TCP.
- Acceso remoto: VPN tipo Tailscale, WireGuard o ZeroTier. No exponer el PLC a internet.

## Siguiente paso

Definir modelo exacto del PLC, IP, protocolo de comunicacion y las primeras 8 variables/senales.
