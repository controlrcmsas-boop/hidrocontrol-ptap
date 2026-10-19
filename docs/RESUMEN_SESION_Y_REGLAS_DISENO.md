# HIDROCONTROL PTAP — Resumen de Sesión y Guía de Reglas de Diseño

> **Fecha:** 18 de Octubre, 2026  
> **Estado:** Listo para retomar mañana  
> **Servidor Local:** `http://localhost:5088` (Blazor Server .NET 8)

---

## 1. Reglas Estrictas de Diseño y Estética Visual (UI/UX)

Estas directrices son **mandatorias** para todas las pantallas y componentes del sistema:

### A. Botones Estándar (Estilo Cápsula Píldora — Imagen 2)
* **Forma:** Cápsula píldora completa (`border-radius: 9999px`).
* **Tipografía:** Texto limpio, legible, peso mediano (`font-weight: 500` a `600`), sin mayúsculas sostenidas agresivas.
* **Cero Iconos / Emojis:** Los botones **NO DEBEN tener iconos, emojis 3D ni adornos** (prohibidos `🔑`, `⚡`, `☀️`, `🌧️`, `⚠️`, `🧪`, `🛑`, `📈`, etc.). Solo texto limpio.
* **Paleta de Colores:**
  * **Activo / Principal:** Azul corporativo de HIDROCONTROL (`#2585E8`).
  * **Inactivo / Secundario:** Fondo gris carbón neutro (`#2A2C32`), borde sutil (`rgba(255, 255, 255, 0.08)`), texto gris claro (`#9EABB8`).
  * **Hover:** Transición suave a `#353842` con texto blanco (`#FFFFFF`).

### B. Elementos Informativos y Lecturas (Si no es botón, no debe parecerlo)
* **Indicadores de estado (Status):** Si un elemento es puramente informativo (ej. *Modo Simulación Activo* o *Planta Real PLC*), debe presentarse como **texto plano con un punto discreto** (`●`), sin recuadro, sin borde de cápsula y sin aspecto clickeable.
* **Lecturas de Sensores:** Las métricas actuales de sensores (ej. `7.06 pH`, `0.40 NTU`, `451 µS/cm`) deben utilizar **tipografía numérica limpia** con su unidad en azul corporativo, nunca botones ni badges coloridos recargados.

### C. Minimalismo y Recuadros
* **Regla de los Recuadros:** Los **únicos recuadros permitidos** son las tarjetas contenedoras que separan secciones o temas principales en una ventana (siguiendo el esquema de la pantalla física).
* **Fondos:** Tonos oscuros profesionales y limpios (`#18191D` para fondos profundos, `#1E2025` para tarjetas de sección).
* **Cero ruido visual:** Cero badges multicolores, cero barras superfluas, cero animaciones distractoras.

---

## 2. Reglas Funcionales y de Proceso PTAP

### A. Comunicación con el PLC Siemens S7-1200 (Prioridad Vital)
* La conexión con el PLC **nunca debe desconectarse ni descartarse**, es la vida del sistema.
* **Mecanismo de Reconexión No Bloqueante (`NodeRedApiClient.cs`):**
  * Cuando la app está en modo PLC, realiza sondeos en segundo plano cada 3 segundos con timeout estricto de 600 ms.
  * Si la planta física/Node-RED está apagada (ej. trabajo remoto), la app **no se congela ni presenta lag**.
  * En cuanto se encienda el PLC en la oficina, la app se sincroniza en vivo de forma instantánea y automática.

### B. Sensores del Sistema
* La maqueta dispone de **un único sensor por variable de proceso**:
  1. **Sensor de pH** (una sola línea de medición).
  2. **Sensor de Turbidez** (una sola línea de medición).
  3. **Sensor de Conductividad** (una sola línea de medición).
* **Eliminado:** No se hace distinción de "Agua Cruda" vs. "Agua Tratada" (el sensor se ubicará donde convenga según la prueba).
* **Eliminado de la instrumentación del simulador:** Ni presión ni caudal existen como controles de proceso en la maqueta.

### C. Actuadores Físicos Reales
* Solo existen 3 actuadores en la maqueta física:
  * **B1:** Bomba Principal
  * **B2:** Dosificador de Sulfato
  * **B3:** Dosificador de Cloro
* **Eliminado:** "Nivel Tanque Agua Cruda" (no existe en la maqueta) y "Modo Remoto Habilitado".
* Se mantiene la simulación de falla: **Forzar Falla en PLC S7-1200**.

---

## 3. Registro de lo Realizado en la Sesión de Hoy

1. **Resolución de Lentitud e Inactividad de Clics:**
   * Solucionado el bloqueo del circuito de Blazor causado por peticiones HTTP síncronas que esperaban 5s cuando Node-RED estaba apagado. La app responde a 60 FPS sin retrasos.
2. **Corrección de Carga Automática en Login:**
   * Solucionado el autofill de credenciales para los roles de prueba (Operador, Gerente, Simulador, Admin).
   * Eliminado el icono `🔑` del botón principal.
3. **Restauración del Modo Operativo de Planta:**
   * El selector de modo (`Simulador` / `PLC`) quedó asignado exclusivamente al **Técnico / Operador** en `Home.razor`.
4. **Ventana Pantalla HMI:**
   * Eliminada por completo del sistema (`PantallaHmi.razor` y enlace en `NavMenu.razor`), ya que el diseño se integró directamente en el software de la pantalla física.
5. **Reingeniería de la Ventana HISTÓRICOS (`Historicos.razor`):**
   * Indicador de estado transformado en texto plano (`● Modo Simulación Activo`), sin parecer botón.
   * Gráficas individuales para **pH**, **Turbidez** y **Conductividad** (1 sola línea por sensor).
   * **Escala dinámica inteligente en eje Y:** Se eliminaron rangos desproporcionados (ej. 0-14 para pH o 0-10 para turbidez). Ahora la escala se ciñe a los datos reales mostrando ondas y variaciones vivas con área en gradiente azul (`#2585E8`).
   * **Rangos temporales:** Píldoras estandarizadas para **15 min** (por defecto), **30 min** y **1 Hora**.
   * **Tooltip interactivo Hover / Touch:** Al mover el mouse o tocar en celular, se despliega una línea guía, un punto marcador y un tooltip con valor exacto y hora de la muestra.
6. **Depuración de la Ventana SIMULADOR PLC (`Simulador.razor`):**
   * Eliminados todos los emojis y colores de los botones de escenarios rápidos.
   * Variables de proceso reducidas estrictamente a **pH**, **Turbidez** y **Conductividad** (eliminadas presión y caudal).
   * Actuadores reducidos a los 3 equipos reales + falla de PLC (eliminados nivel de tanque y modo remoto).
   * Eliminado el bloque de "Respuesta Calculada de Agua Tratada".
   * **Eliminado el letrero flotante de confirmación** que aparecía bajo el título para evitar el salto de botones y garantizar una pantalla 100% estática y fluida.

---

## 4. Estado para Reanudar Mañana

* **Código compilando con 0 errores y 0 advertencias críticas.**
* **Ruta de continuidad:** Seguir revisando rol por rol y ventana por ventana según la lista de prioridades del usuario.
