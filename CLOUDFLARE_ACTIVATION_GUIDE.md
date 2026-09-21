# GUÍA DE ACTIVACIÓN CLOUDFLARE Y RESOLUCIÓN DEFINITIVA DE ERROR 525
## SCADA HIDROCONTROL PTAP — Despliegue Cloud 24/7 en `https://hidrocontrol.potenzia.app`

Esta guía detalla el procedimiento exacto y definitivo para resolver el **Error 525 (SSL Handshake Failed)** en Cloudflare y dejar operativo al 100% con candado verde HTTPS el dominio oficial:
👉 **`https://hidrocontrol.potenzia.app`**

---

### 1. Diagnóstico Técnico del Error 525

Actualmente, el sistema se encuentra en el siguiente estado operacional:
- **Origen Cloud Directo (`https://hidrocontrol-ptap.fly.dev`):** **OPERATIVO (HTTP 200 OK)** con certificado TLS válido de Let's Encrypt (`*.fly.dev`). El SCADA Blazor Server y el motor de simulación física estocástica Box-Muller operan de forma autónoma 24/7 en Fly.io sin depender de la PC local.
- **Acceso HTTP (`http://hidrocontrol.potenzia.app`):** **OPERATIVO (HTTP 200 OK)** a través del proxy Anycast de Cloudflare. El Error 1016 (túnel Argo local obsoleto) fue resuelto apuntando el CNAME directamente a Fly.io.
- **Acceso HTTPS (`https://hidrocontrol.potenzia.app`):** Retorna **Error 525**.
  - **Causa raíz:** La zona `potenzia.app` en Cloudflare tiene configurado el modo SSL/TLS global como **Full** o **Full (Strict)**. Al recibir una petición HTTPS del navegador, Cloudflare intenta establecer un túnel TLS en el puerto 443 contra Fly.io con SNI `hidrocontrol.potenzia.app`. Dado que Fly.io aún no ha validado la titularidad del dominio personalizado (`Not verified`), rechaza el handshake TLS en puerto 443.

---

### 2. Soluciones Definitivas (Elige Opción A u Opción B)

Se presentan dos alternativas verificadas:
- **Opción A (Recomendada - Activación Inmediata en 30 Segundos):** Crear una Regla de Configuración (*Configuration Rule*) en Cloudflare para activar SSL Flexible únicamente en el subdominio `hidrocontrol.potenzia.app`.
- **Opción B (Validación CNAME/TXT de Certificado de Origen):** Añadir los registros DNS de validación ACME para que Fly.io emita su propio certificado Let's Encrypt y funcione con Full/Strict.

---

### OPCIÓN A: Regla de Configuración SSL Flexible (Recomendada - 30 Segundos)

Esta opción es la más rápida y estándar para aplicaciones alojadas en Fly.io detrás del proxy de Cloudflare. No altera la seguridad de los demás subdominios de `potenzia.app`.

#### Paso a paso:
1. Inicia sesión en el panel de control de Cloudflare: [https://dash.cloudflare.com](https://dash.cloudflare.com)
2. Selecciona la zona o dominio: **`potenzia.app`**
3. En el menú lateral izquierdo, despliega **Rules** (Reglas) y haz clic en **Configuration Rules** (Reglas de configuración).
4. Haz clic en el botón azul **Create rule** (Crear regla).
5. Configura los siguientes campos:
   - **Rule name (Nombre de regla):** `Flexible SSL for Hidrocontrol`
   - **If incoming requests match... (Regla de coincidencia):**
     - Selecciona **Custom filter expression**
     - **Field (Campo):** `Hostname`
     - **Operator (Operador):** `equals`
     - **Value (Valor):** `hidrocontrol.potenzia.app`
   - **Then... (Parámetros a aplicar):**
     - Busca la sección **SSL** y marca la casilla.
     - En el menú desplegable de SSL, selecciona: **Flexible**.
6. Haz clic en **Deploy** (Desplegar).

#### Resultado:
- El navegador del cliente se conecta a Cloudflare mediante **HTTPS seguro (candado verde)** con el certificado universal de Cloudflare.
- Cloudflare se conecta a Fly.io por HTTP en el puerto 80 (que mapea internamente al puerto 8080 del contenedor).
- **El Error 525 desaparece instantáneamente** y `https://hidrocontrol.potenzia.app` carga con éxito.

---

### OPCIÓN B: Emisión de Certificado de Origen Fly.io (Full/Strict)

Si prefieres mantener el cifrado de extremo a extremo (Full/Strict) entre Cloudflare y Fly.io:

#### Paso a paso:
1. En Cloudflare Dashboard, ingresa a **`potenzia.app`** -> **DNS** -> **Records** (Registros).
2. Agrega los dos registros de validación que requiere Fly.io:

   **Registro 1 (Propiedad del dominio):**
   - **Type (Tipo):** `TXT`
   - **Name (Nombre):** `_fly-ownership.hidrocontrol`
   - **Content (Contenido):** `app-md50kpn`
   - **TTL:** Auto

   **Registro 2 (Desafío ACME para Let's Encrypt):**
   - **Type (Tipo):** `CNAME`
   - **Name (Nombre):** `_acme-challenge.hidrocontrol`
   - **Target (Destino):** `hidrocontrol.potenzia.app.md50kpn.flydns.net`
   - **Proxy status:** **DNS Only** (Nube gris, sin proxy)
   - **TTL:** Auto

3. Una vez creados los registros, Fly.io detectará la validación en 1 a 3 minutos y emitirá automáticamente el certificado SSL para `hidrocontrol.potenzia.app`.
4. El handshake en puerto 443 funcionará correctamente en modo Full/Strict.

---

### 3. Sincronización de Código: Git Commit & Push

Todos los cambios del sistema SCADA V2.2 ya están listos y preparados en el índice de Git (`staged`):
- `.github/workflows/fly-deploy.yml`: Flujo automatizado CI/CD para despliegues continuos.
- `dashboard/PTAPControl/Program.cs`: Configuración de proxy inverso, Forwarded Headers y CORS.
- `dashboard/PTAPControl/Services/PtapSimulationEngine.cs`: Simulación estocástica en memoria y límites de alarma.
- `dashboard/PTAPControl/Services/NodeRedApiClient.cs`: Desacoplamiento total del PLC físico.
- `dashboard/PTAPControl/Components/Pages/Configuracion.razor`: Desacoplamiento de enlaces locales.
- `fly.toml`: Configuración de persistencia y máquina continua (`min_machines_running = 1`).

Para enviar estos cambios al repositorio remoto en GitHub, ejecuta en PowerShell:
```powershell
cd "D:\PROGRAMAS\CONTROL\HIDROCONTROL PTAP V2.2"
git commit -m "feat: CI/CD workflow, in-memory alarm limits, and cloud UI decoupling"
git push origin main
```

---

### 4. Configuración del Token CI/CD en GitHub (Opcional para auto-despliegues)

Para que cada `git push origin main` actualice automáticamente el contenedor en Fly.io sin intervención manual:
1. En GitHub, ve al repositorio: `controlrcmsas-boop/hidrocontrol-ptap`
2. Ve a **Settings** -> **Secrets and variables** -> **Actions**.
3. Haz clic en **New repository secret**.
4. Nombre: `FLY_API_TOKEN`
5. Valor: Pega tu token de despliegue de Fly.io (generado con `fly tokens create deploy`).
6. Guarda el secreto.

---

### 5. Verificación Final de Disponibilidad

Una vez aplicada la Opción A o la Opción B en Cloudflare, verifica el funcionamiento con:
1. **Navegador web o dispositivo móvil:**
   Accede a [https://hidrocontrol.potenzia.app](https://hidrocontrol.potenzia.app)
2. **Resultados esperados:**
   - Carga instantánea de la interfaz SCADA Blazor en HTTPS con candado de seguridad.
   - Sin error 525 ni error 1016.
   - Monitoreo en tiempo real de pH, turbidez, cloro, bombas y niveles de tanques con variación estocástica.
   - Funcionamiento ininterrumpido 24 horas al día, 7 días a la semana, incluso con la PC de desarrollo apagada.
