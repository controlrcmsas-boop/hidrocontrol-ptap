# Acceso remoto seguro - HIDROCONTROL PTAP

## Objetivo

Permitir acceso remoto al dashboard sin exponer directamente el PLC.

## Recomendacion

Usar VPN privada:

- Tailscale para demo rapida y sencilla.
- WireGuard para configuracion mas industrial/controlada.
- ZeroTier como alternativa.

## Topologia recomendada

```text
Operador remoto
  -> VPN segura
  -> PC local de planta
  -> Dashboard PTAP Control
  -> Node-RED
  -> PLC S7-1200
```

## Reglas minimas

1. PLC sin puertos abiertos a internet.
2. Node-RED con autenticacion si queda accesible en red.
3. Dashboard con login.
4. Comandos con confirmacion.
5. Registro de operador, fecha y resultado.
6. Roles: solo lectura, operador, supervisor.
