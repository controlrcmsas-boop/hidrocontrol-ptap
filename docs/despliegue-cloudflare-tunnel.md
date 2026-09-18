# Despliegue Online con Cloudflare Tunnel

**Dominio Oficial:** `hidrocontrol.potenzia.com`  
**Servicio Local:** Dashboard SCADA Blazor Server en `http://localhost:5088`

---

## ¿Por qué Cloudflare Tunnel para HIDROCONTROL PTAP?

1. **Soporte Nativo de WebSockets Blazor:**
   A diferencia de plataformas serverless como Vercel, Cloudflare Tunnel mantiene túneles dúplex persistentes hacia el servidor Kestrel en el puerto `5088`, garantizando que la telemetría SCADA y los circuitos de SignalR (`/_blazor`) nunca se desconecten.

2. **Seguridad Industrial de Grado Cero Confianza (Zero Trust):**
   - **Cero puertos abiertos en el router:** La máquina local/industrial solo establece una conexión saliente TLS cifrada hacia la red Anycast de Cloudflare.
   - El PLC Siemens S7-1200 y Node-RED quedan 100% aislados de internet.
   - Se puede restringir el acceso con autenticación corporativa (Google, correo corporativo o PIN) desde el panel de Cloudflare Zero Trust.

3. **Certificado SSL Automático:**
   Cloudflare provee y renueva automáticamente el certificado HTTPS para `hidrocontrol.potenzia.com`.

---

## Modalidades de Uso

### Modalidad 1: Prueba Rápida Inmediata (Sin configuración de cuenta previa)
1. Ejecuta el archivo:
   ```cmd
   Iniciar-Tunel-Cloudflare-Pruebas.bat
   ```
2. La consola generará una URL pública y segura temporal (ejemplo: `https://palabra-aleatoria.trycloudflare.com`).
3. Abre esa URL en cualquier navegador o teléfono para ver el SCADA funcionando en vivo.

---

### Modalidad 2: Dominio Fijo de Producción (`hidrocontrol.potenzia.com`)

Sigue estos 4 sencillos pasos en Cloudflare:

1. **Iniciar Sesión en Cloudflare Zero Trust:**
   - Entra a [https://one.dash.cloudflare.com](https://one.dash.cloudflare.com).
   - Ve a **Networks** > **Tunnels**.
   - Haz clic en **Add a tunnel** (o "Create a tunnel").

2. **Crear el Túnel:**
   - Selecciona **Cloudflared**.
   - Dale como nombre `hidrocontrol-ptap` y presiona **Next**.

3. **Copiar el Token:**
   - En el paso "Install and run a connector", selecciona **Windows**.
   - Verás un bloque de texto que termina con un token largo, por ejemplo:
     `eyJhIjoiYTY...`
   - Copia ese token.

4. **Vincular el Dominio y el Puerto Local:**
   - En la pestaña **Public Hostname** del túnel:
     - **Subdomain:** `hidrocontrol`
     - **Domain:** `potenzia.com`
     - **Type:** `HTTP`
     - **URL:** `localhost:5088` (o `127.0.0.1:5088`)
   - Guarda los cambios.

5. **Iniciar el Servicio Localmente:**
   - Ejecuta:
     ```cmd
     Iniciar-Tunel-Cloudflare-Produccion.bat
     ```
   - Pega el token cuando el script te lo solicite (o guárdalo en `tools/tunnel-token.txt`).
   - El túnel quedará conectado y `https://hidrocontrol.potenzia.com` estará disponible en vivo.

---

## Ejecución Conjunta con el Sistema Completo

Para levantar todo el entorno productivo:
1. Ejecuta `Iniciar-TODO.bat` (levanta Node-RED, WhatsApp Gateway y Kestrel en puerto 5088).
2. Ejecuta `Iniciar-Tunel-Cloudflare-Produccion.bat`.
