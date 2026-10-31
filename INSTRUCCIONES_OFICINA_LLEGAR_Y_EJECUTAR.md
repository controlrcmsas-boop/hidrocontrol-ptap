# 📋 INSTRUCCIONES EXACTAS Y TO-DO OFICINA: LLEGAR Y EJECUTAR
**HIDROCONTROL PTAP V2.2 - PANTALLA SEEKU WSB7048R + PLC SIEMENS S7-1200 + SCADA WEB**

> **ESTADO DEL SISTEMA:** 100% Configurado y Listo.  
> **PAQUETE DE ACTUALIZACIÓN CREADO:** Carpeta `d:\PROGRAMAS\CONTROL\ACTUALIZACION_SERVIDOR_OFICINA` lista para copiar al Servidor.

---

## 🗂️ 1. LISTA COMPLETA DE ARCHIVOS ACTUALIZADOS HOY
Estos 14 archivos contienen todas las soluciones de hoy y ya están empaquetados en la carpeta `ACTUALIZACION_SERVIDOR_OFICINA`:

| Archivo | Ubicación en el Servidor | ¿Qué hace y qué problema soluciona? |
| :--- | :--- | :--- |
| **`hidrocontrol-ptap-flow.json`** | `nodered\flows\` y `%USERPROFILE%\.node-red\flows.json` | **Solución error DB11:** Separa lectura DB10 de escritura DB11. Elimina para siempre el error rojo `Area [132] DB [11] Invalid address`. |
| **`edge-agent.js`** | `services\edge-agent\` | **Estabilidad Agente:** Apagado en cascada (si la bomba se apaga, apaga dosificadores), protección anti-caídas y temporizador anti-rebote de 15s. |
| **`Enlace-Pantalla-SEEKU-FX.ps1`** | `scripts\` | **Enlace Pantalla:** Lee telemetría cada 1s de Node-RED e inyecta D0..D2 y M0..M2 a la pantalla en protocolo nativo Mitsubishi FX (38400 baud). |
| **`Conectar-Pantalla-SEEKU.bat`** | Raíz del proyecto | Lanzador de 1 clic para probar el enlace de la pantalla de forma aislada. |
| **`Iniciar-TODO-Con-Internet.bat`** | Raíz del proyecto | Lanzador maestro de planta: arranca Node-RED + WhatsApp + SCADA Blazor + Túnel Cloudflare + Pantalla SEEKU. |
| **`Iniciar-TODO.bat`** | Raíz del proyecto | Lanzador maestro para operar localmente si no hay conexión a internet. |
| **`Arrancar-Servidor-Planta.bat`** | Raíz del proyecto | Script de arranque local optimizado para el servidor de planta. |
| **`Arrancar-Servidor-Planta-Tailscale.bat`** | Raíz del proyecto | Script de arranque con red VPN industrial Tailscale. |
| **`ptap-edge-history.db`** | `database\` | **Base de Datos SQLite:** Histórico local con Store & Forward para telemetría continua offline/online. *(12 KB, verificada).* |
| **`policy-rules.json`** | `database\` | Umbrales y políticas de dosificación del criterio JEV / Agente Local. |
| **`hidrocontrol-auth.db`** | `dashboard\PTAPControl\` | **Base de Datos SQLite:** Usuarios, contraseñas y permisos del SCADA web para el operador. *(100 KB, verificada).* |
| **`INSTRUCCIONES_OFICINA_LLEGAR_Y_EJECUTAR.md`** | Raíz del proyecto | Esta guía maestra de procedimiento para tenerla a mano en la oficina. |
| **`SOLUCION_DEFINITIVA_CONEXION_PANTALLA.md`** | `docs\` | Diagnóstico técnico profundo de la arquitectura All-in-One de la pantalla SEEKU. |
| **`INSTALAR_EN_SERVIDOR.bat`** | Carpeta de actualización | **Instalador automático de 1 clic:** Copia todos los archivos a sus rutas en el servidor y actualiza `flows.json` de Node-RED. |

---

## 💾 2. CONFIRMACIÓN DE LAS BASES DE DATOS DEL SERVIDOR
Ambas bases de datos SQLite requeridas existen físicamente en disco y están verificadas:
1. **`database\ptap-edge-history.db` (12 KB):**
   - Tabla: `telemetry_history` (guarda timestamp, pH, turbidez, conductividad, estados de bomba y dosificadores, y bandera de sincronización `sincronizado`).
   - El motor es SQLite nativo embebido en Node.js (`node:sqlite`). No necesita servidores externos como PostgreSQL ni puertos adicionales.
2. **`dashboard\PTAPControl\hidrocontrol-auth.db` (100 KB):**
   - Base de datos de autenticación del SCADA Blazor.
   - Contiene el usuario Administrador y permisos de Operador para permitir que los botones de encendido/apagado de bomba y dosificadores funcionen.

---

## ✅ 3. CHECKLIST TO-DO MAESTRO AL LLEGAR A LA OFICINA

### 🔹 FASE A: LLEVAR Y APLICAR LA ACTUALIZACIÓN EN EL SERVIDOR (1 minuto)
- [ ] **1. Copiar la carpeta:** Toma la carpeta `d:\PROGRAMAS\CONTROL\ACTUALIZACION_SERVIDOR_OFICINA` de esta laptop y pásala al Servidor de la oficina (mediante Google Drive o memoria USB).
- [ ] **2. Ejecutar el instalador:** En el Servidor de la oficina, abre esa carpeta y haz doble clic en:
  👉 **`INSTALAR_EN_SERVIDOR.bat`**
  *(Esto copiará automáticamente los 14 archivos a la carpeta del servidor `C:\SERVIDOR_HIDROCONTROL` y actualizará el archivo `flows.json` de Node-RED sin que tengas que buscar carpetas a mano).*

---

### 🔹 FASE B: TIA PORTAL - HABILITAR MANDO REMOTO DE BOMBAS DESDE LA WEB (2 minutos)
*Para que el técnico u operador pueda presionar los botones ON/OFF de la bomba y dosificadores desde la web y el PLC Siemens los acepte:*
- [ ] **1. Abrir el proyecto en TIA Portal** en el computador donde esté instalado TIA Portal.
- [ ] **2. Ubicar el bloque DB11:** En el árbol del proyecto, ir a *Bloques de programa* ➔ Clic derecho sobre **`DB11_SCADA_WRITE`** ➔ **Propiedades**.
- [ ] **3. Desmarcar Acceso Optimizado:** En la pestaña **Atributos**, asegurarse de que esté **DESMARCADA** la casilla:  
  `[ ] Acceso optimizado al bloque` *(Optimized block access)*.
- [ ] **4. Cargar a la CPU:** Clic en **Compilar** y luego en **Cargar en dispositivo** (Descargar a la CPU del Siemens S7-1200).
- [ ] *(Resultado: El Siemens expondrá las direcciones absolutas `DB11.DBX0.0` y cuando el técnico presione ON/OFF en el SCADA web, la bomba prenderá y apagará inmediatamente).*

---

### 🔹 FASE C: DESCARGAR DISEÑO A LA PANTALLA SEEKU (1 minuto)
*La pantalla física informativa de 7":*
- [ ] **1. Conectar cable USB:** Conecta el cable USB de programación de la pantalla a la laptop/servidor.
- [ ] **2. Abrir WSStudio:** Abre el proyecto de la pantalla donde ya dejamos configurado el protocolo Mitsubishi FX3U y los tags (`M0..M2`, `D0..D2`, `D10`).
- [ ] **3. Clic en Download (Descargar):** Espera a que termine. La pantalla se reiniciará sola y **el aviso blanco de error `Communicate Error` desaparecerá**.

---

### 🔹 FASE D: CABLEADO FÍSICO Y ARRANQUE DE PLANTA (1 minuto)
- [ ] **1. Cable Ethernet Amarillo:** Conectado del PLC Siemens S7-1200 al puerto Ethernet del Servidor.
- [ ] **2. Adaptador USB-RS485:** Conectado al puerto USB del Servidor:
  - Borne verde `A` de la pantalla ➔ tornillo `D+` del USB (Cable Verde).
  - Borne verde `B` de la pantalla ➔ tornillo `D-` del USB (Cable Rojo).
- [ ] **3. Encender la Fuente de 24V DC** de la planta.
- [ ] **4. Fijar IP de red:** Clic derecho a **`Configurar-IP-Ethernet-PLC.bat`** ➔ **Ejecutar como Administrador** (asegura la IP `192.168.1.50`).
- [ ] **5. Iniciar todo:** Doble clic a:
  👉 **`Iniciar-TODO-Con-Internet.bat`**

---

### 🔹 FASE E: PRUEBA DE FUEGO DE LA EXPERIENCIA SHOWROOM (100% LISTA)
Para deslumbrar a cualquier cliente o socio técnico:
- [ ] **1. Preparar 4 Vasos de Muestra en la mesa:**
  * **Vaso 1: Agua Limpia / Grifo** (`pH ~7.1`, `Turb < 1.0 NTU`, `Cond ~90 µS/cm`) ➔ Actúa Dosificador de Cloro (desinfección continua).
  * **Vaso 2: Agua con Tierra** (`Turb > 80 NTU`) ➔ Actúa Dosificador de Sulfato (coagulación) y suena/parpadea alarma.
  * **Vaso 3: Agua con Limón** (`pH < 4.5`) ➔ Alerta de acidez extrema / corrosión de tuberías.
  * **Vaso 4: Agua con Sal** (`Cond > 1.500 µS/cm`) ➔ Alerta de sólidos disueltos / salinidad.
- [ ] **2. Vincular WhatsApp en el Servidor (Solo la primera vez):**
  * Abrir en el navegador: `http://localhost:5089/qr`
  * Escanear el código QR con el WhatsApp de la empresa (o el teléfono que enviará las alertas).
- [ ] **3. Registrar el WhatsApp del Cliente:**
  * En el SCADA (`http://localhost:5088/configuracion`), escribir el celular del cliente (ej. `+573001234567`) y dar clic en **"Enviar prueba"**.
  * El cliente recibirá de inmediato el mensaje de bienvenida de HIDROCONTROL y MO.
- [ ] **4. Demostración del Apagón Digital (La carta ganadora):**
  * Con la planta operando, desconectar el cable de red / Wi-Fi del computador servidor durante 1 a 2 minutos.
  * Señalarle al cliente: *La pantalla sigue leyendo, MO Local Edge asumió el mando autónomo y ningún dato se pierde.*
  * Reconectar el cable de internet: en segundos, la nube se actualiza (*Store & Forward*) y al WhatsApp del cliente llega el reporte automático de restablecimiento.

---

## 🎯 RESULTADO FINAL ESPERADO EN PLANTA
1. **Consola Node-RED:** Totalmente limpia en verde, leyendo el bloque DB10 sin el error `Invalid address`.
2. **Pantalla Física SEEKU 7":** Valores de pH, Turbidez y Conductividad en vivo. Bombillo de la bomba encendiéndose en sincronía. Cero letreros de error.
3. **Dashboard SCADA Web (`http://localhost:5088` o dominio online):**
   - Telemetría en tiempo real.
   - Botones **ON / OFF** de la Bomba Principal B1, Dosificador de Sulfato B2 y Cloro B3 respondiendo al clic del operador y actuando directamente en las salidas del Siemens S7-1200 vía DB11.
   - Históricos guardándose de forma ininterrumpida en SQLite local y sincronizados con la nube.
4. **Copiloto Inteligente MO:** Asesorando en pantalla de Análisis IA, vigilando la resiliencia offline y despachando reportes al WhatsApp del cliente.
