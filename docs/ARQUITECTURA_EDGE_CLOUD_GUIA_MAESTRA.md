# GUÍA MAESTRA DE ARQUITECTURA: EDGE-CLOUD HÍBRIDO CON INTELIGENCIA DISTRIBUIDA
## SCADA HIDROCONTROL PTAP (Plataforma SaaS + Servidor Local Resiliente)

**Fecha:** 2026-10-05  
**Documento Oficial de Referencia para el Equipo de Desarrollo e Ingeniería**

---

## 1. Visión y Principios del Sistema

El sistema SCADA HIDROCONTROL PTAP implementa una arquitectura híbrida de **Industria 4.0** basada en el principio de **Supervivencia Local Total (*Local Survivability*) con Inteligencia Cloud Centralizada**.

### Principios Fundamentales:
1. **Cero Dependencia para Operar:** Si la planta se queda sin internet (por horas o días), la potabilizadora sigue operando, monitoreando, decidiendo y guardando datos en disco local sin interrupción.
2. **SaaS Autónomo 24/7:** La plataforma web en la nube (`https://hidrocontrol.potenzia.app`) permanece siempre accesible, mostrando el estado actual, el histórico sincronizado y actuando como centro de comando para supervisores y directivos.
3. **Jerarquía Inteligente con Transmisión de Criterio:** El agente de la nube (**TypeSafe JEV**) posee visión global y entrena periódicamente al **Agente Local**, transfiriéndole criterio operativo actualizado para que el agente local tome decisiones óptimas incluso desconectado.
4. **Seguridad Física Inviolable:** Ninguna IA ni comando remoto puede saltarse las protecciones y enclavamientos cableados en el PLC Siemens S7-1200.

---

## 2. Esquema Integral de Arquitectura

```text
+-----------------------------------------------------------------------------------------+
|                                  PLC SIEMENS S7-1200                                    |
|   DB10 (Lectura de Sensores)             |   DB11 (Comandos / Salidas a Actuadores)     |
|   [Lógica de Seguridad e Interlocks]     |   [Bombas, Válvulas, Cloro, Coagulante]      |
+-----------------------------------------------------------------------------------------+
                  ▲                                                      |
                  | S7 Protocol (ISO-on-TCP)                             | Ciclo PLC
                  | (node-red-contrib-s7)                                ▼
+-----------------------------------------------------------------------------------------+
|                        SERVIDOR LOCAL DE PLANTA (EDGE ON-PREMISE)                       |
|                                                                                         |
|  [NODE-RED - PUERTO 1880]                  [BASE DE DATOS SQL LOCAL]                   |
|  • GET  /api/telemetry/current             • Registro histórico en disco local          |
|  • POST /api/control/command               • Cola Store & Forward (guarda sin red)      |
|  • POST /api/sync/flush-pending            • Flag: 'sincronizado = false'               |
|                                                                                         |
|  [AGENTE IA LOCAL (OFFLINE / MODELFILE)]   [WATCHDOG DE CONEXIÓN]                       |
|  • Inferencia local sin internet           • Monitorea Heartbeat de la nube             |
|  • Asume el mando si cae la red            • Conmutación automática a modo local        |
|  • Recibe actualizaciones de JEV           • Transición suave al volver internet        |
+-----------------------------------------------------------------------------------------+
                  ▲                                                      |
                  | Telemetría / Store & Forward                         | Heartbeat / Comandos
                  | Actualizaciones de Modelo de JEV                     | Políticas de Control
                  ▼                                                      ▼
+-----------------------------------------------------------------------------------------+
|                        ENLACE SEGURO TAILSCALE (RED ZERO-TRUST)                         |
|                 https://desktop-u8frjng.tail82f1e5.ts.net:1880                          |
|             (Permite acceso seguro al servidor sin abrir puertos ni IP fija)            |
+-----------------------------------------------------------------------------------------+
                  ▲                                                      |
                  | Streaming Telemetría / WebSockets                    | HTTPS POST API /
                  ▼                                                      ▼ Sincronización
+-----------------------------------------------------------------------------------------+
|                       PLATAFORMA SAAS EN LA NUBE (24/7 AUTÓNOMA)                        |
|                                                                                         |
|  [DASHBOARD WEB SCADA - BLAZOR]            [BASE DE DATOS SQL CLOUD (SUPABASE)]         |
|  • URL: https://hidrocontrol.potenzia.app  • Histórico general consolidado              |
|  • Pantalla SCADA en tiempo real           • Recibe lotes sincronizados de planta       |
|  • Control de actuadores y setpoints       • Usuarios, permisos y auditoría             |
|                                                                                         |
|  [AGENTE CLOUD TYPESAFE / JEV (MANDO 1 - REGIDOR PRINCIPAL)]                            |
|  • Modelo superior con visión global, analítica profunda y correlación química          |
|  • Envía 'Heartbeat' continuo para mantener la autoridad de mando                       |
|  • Rige sobre el sistema mientras exista conexión a internet activa                     |
|  • ENTRENADOR CONTINUO: Destila conocimiento y actualiza el criterio del Agente Local   |
+-----------------------------------------------------------------------------------------+
```

---

## 3. Mecanismo de Aprendizaje: JEV Cloud Entrena al Agente Local

Para evitar que el agente local quede estancado con reglas fijas u obsoletas, se implementa el patrón **Knowledge Distillation & Policy Sync (Destilación de Conocimiento y Sincronización de Criterio)**:

```text
┌──────────────────────────────────────────────────────────────────┐
│                   AGENTE CLOUD (TypeSafe JEV)                    │
│  1. Analiza semanas de datos históricos en Supabase              │
│  2. Descubre correlaciones complejas:                            │
│     (ej: Turbidez cruda vs Clima vs Dosificación óptima de PAC)  │
│  3. Optimiza consumos energéticos y curvas de dosificación       │
│  4. Genera un "Paquete de Criterio" (Policy Matrix + Modelfile)  │
└─────────────────────────────────┬────────────────────────────────┘
                                  │
                   Sincronización Periódica vía Tailscale
                   (JSON de Políticas / Pesos de Decisión)
                                  ▼
┌──────────────────────────────────────────────────────────────────┐
│                    AGENTE LOCAL (Servidor Edge)                  │
│  1. Ingesta el nuevo Paquete de Criterio en caliente             │
│  2. Actualiza sus matrices de límites, umbrales y setpoints      │
│  3. SI SE CORTA INTERNET:                                        │
│     Aplica de forma autónoma el criterio aprendido de JEV        │
└──────────────────────────────────────────────────────────────────┘
```

### Componentes del "Paquete de Criterio":
1. **Matriz de Reglas Dinámicas (`policy-rules.json`):** Tablas de correlación generadas por JEV (ej. rangos de dosificación de sulfato de aluminio y cloro según pH y turbidez de entrada).
2. **Contexto del Sistema para Inferencia Local (`Modelfile.ptap`):** System prompt actualizado y ejemplos de toma de decisiones (*few-shot*) para el motor de inferencia local (Ollama / SLM local).
3. **Límites de Seguridad Acotados:** Restricciones de variación máxima por hora para evitar oscilaciones en los lazos de control de la planta.

---

## 4. Pilares de Resiliencia y Seguridad Operativa

### Pilar 1: Sincronización de Base de Datos (*Store & Forward*)
* **En planta:** Toda lectura de telemetría y cambio de estado se registra de inmediato en la base de datos SQL local (PostgreSQL o SQLite) con una columna de control: `sincronizado: false`.
* **Conexión normal:** Un worker en segundo plano sincroniza en tiempo real hacia Supabase y actualiza `sincronizado = true`.
* **Corte de red:** El servidor acumula las lecturas localmente en disco.
* **Restablecimiento:** Al reaparecer la conexión, Node-RED ejecuta un vaciado por lotes (*batch flush* de 100 en 100 registros) a Supabase. **Cero pérdida de datos históricos.**

### Pilar 2: Conmutación de Mando sin Conflictos (*Watchdog Heartbeat*)
* El Agente Cloud emite un mensaje de **Keep-Alive (Heartbeat)** cada 5 a 10 segundos a través de Tailscale.
* El Servidor Local cuenta con un **Watchdog**:
  * **Si Heartbeat recibido < 15 segundos:** El Agente Cloud es la **Autoridad Activa (Mando 1)**. El Agente Local permanece en modo observador pasivo.
  * **Si Heartbeat timeout > 15 segundos:** El Servidor Local declara fallo de red y conmuta al Agente Local como **Autoridad Activa (Mando 2)**.
  * **Al regresar la red:** El Agente Cloud realiza un *Handshake Suave* (lee el estado actual del PLC antes de enviar cualquier comando) para evitar cambios bruscos en actuadores.

### Pilar 3: Enclavamientos de Seguridad en el PLC (*Safety Interlocks*)
* **Regla estricta:** La inteligencia artificial propone y optimiza; **el hardware dispone y protege**.
* El PLC Siemens S7-1200 tiene programados bloqueos inviolables a nivel de bits en el programa Ladder/SCL:
  * Prohibido encender bombas de impulsión si el nivel del tanque de succión está en nivel muy bajo (LAL - *Low Alarm Limit*).
  * Prohibido abrir dosificadores si no hay confirmación de flujo en el sensor de entrada.
  * Botonera física de Parada de Emergencia (E-Stop) desenergiza salidas de potencia independientemente de cualquier comando de software.

### Pilar 4: Red Privada Cifrada con Tailscale
* Toda la comunicación entre la nube (Fly.io) y la planta corre dentro de una VPN WireGuard mallada (*Zero-Trust*).
* No se abren puertos en el router físico de la planta (sin port-forwarding).
* Cero exposición a escaneos o ataques de denegación de servicio desde la internet pública.

---

## 5. Matriz de Comportamiento Operativo

| Evento | Control de Actuadores | Inteligencia Operativa | Destino de Telemetría | Acceso de Usuarios |
| :--- | :--- | :--- | :--- | :--- |
| **Operación Normal (Internet OK)** | Comandos desde Cloud SaaS o decisiones de JEV | **Agente Cloud JEV** activo con autoridad máxima | SQL Local + Supabase Cloud simultáneo | Web oficial `hidrocontrol.potenzia.app` en tiempo real |
| **Corte de Internet en Planta** | Comandos autónomos del Agente Local | **Agente Local** toma el mando automático con criterio de JEV | SQL Local en disco (Cola *Store & Forward*) | Web en nube muestra datos simulados o último estado conocido; personal local opera vía panel/Node-RED |
| **Retorno de Conexión** | Handshake suave de JEV tras validar estado | **Agente Cloud JEV** reasume la autoridad principal | Node-RED vacía el buffer acumulado a Supabase | Web oficial se actualiza con el histórico completo sin vacíos |

---

## 6. Hoja de Ruta de Implementación para el Equipo

```text
[FASE 1: PERSISTENCIA LOCAL Y STORE & FORWARD]
├── Crear tabla local 'telemetria_historico' con columna 'sincronizado BOOLEAN DEFAULT false'
├── Diseñar flujo Node-RED para vaciado por lotes hacia Supabase API
└── Probar simulación de desconexión física de cable de red (validar cero pérdidas)

[FASE 2: CORAZÓN DEL MANDO (WATCHDOG Y HEARTBEAT)]
├── Implementar endpoint en Node-RED: /api/heartbeat/cloud
├── Crear temporizador Watchdog (15s timeout) para conmutación de mando
└── Registrar logs de auditoría en cada cambio de autoridad (Cloud -> Local -> Cloud)

[FASE 3: CANAL DE ENTRENAMIENTO JEV -> AGENTE LOCAL]
├── Diseñar estructura del JSON de criterios: /database/policy-rules.json
├── Crear servicio en SaaS para calcular coeficientes óptimos basados en datos históricos
├── Implementar endpoint en Servidor Local: /api/ai/update-policy para recibir actualizaciones
└── Adaptar inferencia local (Ollama / Modelfile.ptap) para consumir el criterio recibido

[FASE 4: BLINDAJE INDUSTRIAL Y PRUEBAS EN CAMPO]
├── Validar interlocks en TIA Portal (PLC S7-1200)
├── Verificar estabilidad de Tailscale Funnel / VPN como servicio de Windows (auto-rearranque)
└── Prueba de corte real de suministro de red con operación de planta activa
```
