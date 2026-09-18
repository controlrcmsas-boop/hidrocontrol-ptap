# Arquitectura HIDROCONTROL PTAP

## Datos reales iniciales

| Campo | Valor |
|---|---|
| PLC | Siemens S7-1200 CPU 1214C |
| Referencia | 214-1BG40-0XB0 |
| TIA Portal | V17 |
| IP PLC | 192.168.1.110 |
| Modulo adicional | SM1231 8AI |
| Protocolo PLC elegido | S7 nativo |
| Dashboard | PTAP Control |
| Proyecto | HIDROCONTROL PTAP |

## Arquitectura general

```text
Planta fisica PTAP
  |
PLC Siemens S7-1200 CPU 1214C + SM1231 8AI
IP: 192.168.1.110
  |
Red industrial local
  |
PC local de control
  |-- Node-RED
  |-- Base de datos SQL/PostgreSQL
  |-- Dashboard web PTAP Control
  |
Acceso remoto seguro para operador/cliente
```

## Flujo de datos

```text
PLC S7-1200
  -> Node-RED lee tags por S7 nativo
  -> Node-RED normaliza nombres de tags
  -> Node-RED publica API REST
  -> Node-RED guarda historicos/eventos en SQL
  -> Dashboard Blazor consume API REST
```

## Flujo de comandos

```text
Dashboard PTAP Control
  -> POST /api/control/command
  -> Node-RED valida comando
  -> Node-RED registra comando en SQL
  -> Node-RED escribe bit/valor en PLC por S7 nativo
  -> PLC ejecuta solo si tiene permisos y condiciones seguras
  -> Node-RED confirma estado real leyendo nuevamente el PLC
```

## Principios

1. El PLC no se expone directamente a internet.
2. Node-RED es el unico servicio que lee/escribe al PLC.
3. El dashboard consume API REST de Node-RED.
4. Todo comando queda registrado en base de datos.
5. Toda alarma queda registrada con fecha, tag, valor y estado.
6. Primero se prueba con datos simulados, luego con PLC real.
7. El PLC conserva la autoridad final de seguridad: enclavamientos, fallas, permisos y protecciones.

## Componentes

### PLC Siemens S7-1200

Responsable del control real de la planta: entradas, salidas, motores, sensores y protecciones locales.

Para S7 nativo se recomienda crear un DB de comunicacion SCADA con tags ordenados para lectura/escritura desde Node-RED.

### SM1231 8AI

Modulo de entradas analogicas para sensores de calidad y proceso. Se propone usarlo para pH, turbidez, conductividad y caudales. La presion B1 queda pendiente de canal adicional o reubicacion.

### Node-RED

Responsable de:

- Leer variables del PLC.
- Escribir comandos permitidos.
- Normalizar tags.
- Exponer API REST.
- Guardar historicos y eventos.
- Evaluar alarmas simples si aplica.

### Base de datos

Responsable de:

- Historicos de telemetria.
- Eventos de alarma.
- Registro de comandos.
- Estado del sistema.

### Dashboard PTAP Control

Aplicacion web desarrollada en Visual Studio con ASP.NET Core Blazor Server.

Responsable de:

- Mostrar pantalla SCADA con imagen de fondo HIDROCONTROL.
- Mostrar variables en tiempo real.
- Mostrar estados de bombas/equipos.
- Enviar comandos autorizados.
- Consultar historicos y alarmas.

## Acceso remoto

Recomendado:

- VPN privada: Tailscale, WireGuard o ZeroTier.
- HTTPS para dashboard.
- Usuarios con roles.

No recomendado:

- Abrir puerto del PLC a internet.
- Abrir Node-RED sin autenticacion.
- Permitir comandos sin registro ni confirmacion.
