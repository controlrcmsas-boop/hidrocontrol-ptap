# HIDROCONTROL PTAP V2.2 - ESTADO ACTUAL DE PLANTA Y HOJA DE RUTA MAÑANA

> **Fecha de congelamiento:** 15 de Octubre de 2026 - 17:50  
> **Propósito:** Reanudar de inmediato mañana sin necesidad de recordar ni explicar nada. Solo di: *"Lee el archivo docs/ESTADO_ACTUAL_PLANTA_Y_PASOS_MANANA.md y comencemos"* y sabré exactamente en qué punto quedamos.


---

## 0. GUÍA PASO A PASO: CÓMO ARRANCAR TODO DESDE CERO (SI EL PORTÁTIL SE REINICIÓ)

Si el computador portátil se reinicia o se apaga, esta es la secuencia exacta para dejarlo todo operando en 2 minutos:

### Paso A: Conexiones Físicas
1. **Cable de Red Ethernet:** Conectado del portátil al switch o directamente al PLC Siemens S7-1200.
   * *Verificación IP del portátil:* Debe estar configurado en IP estática `192.168.1.50`, máscara `255.255.255.0`.
2. **Cable USB de la Pantalla SEEKU:** Conectado al puerto USB del portátil (debe ser `COM4`).
3. **Cargador de corriente:** Conectado a la toma eléctrica (para evitar suspensión por batería).

### Paso B: Arrancar los 3 Servicios (En sus 3 Ventanas)
Toda la aplicación de planta vive en la carpeta local: **`C:\SERVIDOR_HIDROCONTROL`**.

* **Opción Rápida (1 solo clic):**
  Doble clic al archivo: **`C:\SERVIDOR_HIDROCONTROL\Arrancar-Servidor-Planta.bat`** (él arranca automáticamente Tailscale, Node-RED y el Agente Edge).

* **Opción Manual (Paso a paso en CMD):**
  * **Ventana 1 (Node-RED):**
    Abre una ventana de CMD en cualquier ubicación y escribe:
    ```cmd
    node-red
    ```
    *(Verás el mensaje `Welcome to Node-RED` y que se conecta exitosamente a DB10).*
  * **Ventana 2 (Túnel Tailscale para la Nube):**
    Abre otra ventana de CMD y escribe:
    ```cmd
    tailscale funnel --bg 1880
    ```
  * **Ventana 3 (Servidor Edge & Base de Datos SQLite):**
    Abre el explorador de Windows en la carpeta `C:\SERVIDOR_HIDROCONTROL`. En la barra de direcciones de la carpeta escribe `cmd` y dale Enter. Luego escribe:
    ```cmd
    node services\edge-agent\edge-agent.js
    ```
    *(Verás el registro continuo guardando telemetría cada 5 segundos en SQLite).*

### Paso C: ¿Y si hacemos cambios de código? (Sin descargar zips)
Si modificamos algún archivo (por ejemplo `edge-agent.js` o flujos de Node-RED):
* **NO hay que volver a descargar todo el zip.**
* Se actualiza únicamente el archivo individual mediante un comando rápido de PowerShell de 1 línea pegado en la ventana de CMD, o editándolo directamente.

---

## 1. ESTADO ACTUAL DEL HARDWARE EN OFICINA

### A. Pantalla Táctil Industrial SEEKU WSB7048R (HMI & PLC All-in-One)
* **Estado:** ✅ **COMUNICACIÓN ESTABLECIDA Y CONFIRMADA.**
* **Conexión física:** Adaptador USB a RS485 con cables conectados a la regleta verde derecha de la pantalla:
  * **Cable A** del USB $\rightarrow$ Bornera **`A`** (primer tornillo superior derecho).
  * **Cable B** del USB $\rightarrow$ Bornera **`B`** (segundo tornillo superior derecho).
* **Parámetros Seriales Confirmados:**
  * **Puerto:** `COM4` (Driver CH340).
  * **Baudios:** `38,400` baud.
  * **Paridad:** `None`.
  * **Bits de datos:** `8`.
  * **Bits de parada:** `1`.
  * **Protocolo:** Mitsubishi FX (Responde con código `ACK 0x06` al saludo `0x05`).

### B. PLC Siemens S7-1200 (Control Maestro de Potencia)
* **IP PLC:** `192.168.1.110` (Puerto 102 ISO-on-TCP).
* **IP Laptop (Servidor Edge):** `192.168.1.50/24`.
* **Bloque de Datos Maestro:** `DB10`.
* **Estado:** Totalmente operativo en vivo vía Node-RED.

### C. Sensores de Instrumentación (Arduino en Entradas Analógicas)
Caracterización física real realizada en oficina el 15/10/2026:

| Sensor | Agua Limpia (Base) | Muestra de Prueba | Lectura en Prueba | Diagnóstico Físico |
| :--- | :---: | :--- | :---: | :--- |
| **pH** | `6.64 pH` | Agua con limón | `4.75 pH` | Funciona perfecto, sensible al ácido. |
| **Turbidez (Óptico)** | `484.3 NTU` | Agua con tierra | `332 NTU` / negativos | **Sensor Invertido:** En agua limpia entrega voltaje alto (~484); al ensuciarse baja el voltaje. |
| **Conductividad** | `66.3 µS/cm` | Agua con sal | `478.0 µS/cm` | Funciona excelente, subió 7 veces con iones de sal. |

### D. Sensor de Nivel de Tanque
* No hay boya física conectada al PLC en este montaje.
* **Bypass de seguridad:** Activo en `edge-agent.js` para permitir el funcionamiento de la bomba sin disparos falsos de marcha en seco.

---

## 2. ESTADO DEL SOFTWARE Y SERVICIOS EN EL PORTÁTIL

1. **Node-RED (`http://localhost:1880`):**
   * Corre en segundo plano leyendo el Siemens S7-1200 DB10 cada segundo.
   * Endpoints de telemetría y comandos operativos:
     * Telemetría: `GET /api/telemetry/current`
     * Comandos: `POST /api/control/command` con `{ target, command, source }`.
2. **Servidor Edge de Planta (`edge-agent.js`):**
   * Ubicación en el portátil: `C:\SERVIDOR_HIDROCONTROL\services\edge-agent\edge-agent.js`.
   * Base de datos local: SQLite `database/ptap-edge-history.db` grabando cada 5 segundos.
   * Agente Local Offline: Detecta turbidez y envía órdenes de control de dosificación.
3. **Tailscale Funnel:**
   * Puerto 1880 expuesto para la nube.
4. **App Web Cloud:**
   * URL: `https://hidrocontrol.potenzia.app` (Máquina Fly.io `78121eda0e19e8`). Permite mando remoto de bomba y dosificadores.

---

## 3. PLAN DE TRABAJO EXACTO PARA MAÑANA (3 PASOS)

### Paso 1: Enlazar la Pantalla SEEKU en Node-RED con el Siemens S7-1200
* Agregar un nodo Serial a Node-RED escuchando `COM4` a 38400 baud.
* Mapear los botones de la pantalla táctil (`M0`, `M1`, `M2`) para que al tocarlos envíen la orden a las salidas del Siemens S7-1200 (`Q0.0`, `Q0.1`, `Q0.2`).
* Comprobar que al tocar el botón en la pantalla física prenda la bomba real.

### Paso 2: Escalamiento / Calibración de Sensores por Software
* Aplicar la fórmula de inversión de turbidez:  
  $$\text{Turbidez Real} = \text{Max}\left(0.5, \; (485 - \text{Raw}) \times 2.0\right)$$
  *(Para que agua limpia marque ~0.8 NTU y con tierra marque ~300 NTU).*
* Calibrar el pH para centrar agua limpia en 7.0 pH y limón en 3.5 pH.
* Escalar la conductividad para mostrar microSiemens/cm estándar.

### Paso 3: Ajuste de Gráficas en la App Web
* Conectar las gráficas históricas de la web para leer los datos de SQLite (`ptap-edge-history.db`).
* Ajustar la escala vertical del gráfico de turbidez (0 a 500 NTU) y el eje temporal.

---

*¡Documento listo y verificado! Puedes desconectarte con total tranquilidad.*
