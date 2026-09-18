# Guía de Despliegue en Línea y Configuración de Proxy Inverso

**Dominio Asignado:** `hidrocontrol.potenzia.com`  
**Aplicaciones Internas:**
- **Dashboard SCADA (Blazor Server):** Kestrel en puerto `5088` (o `5000`).
- **Middleware Node-RED:** Puerto `1880`.
- **Servidor Mock / Simulador:** Puerto `1881` (para pruebas offline).
- **WhatsApp Gateway:** Puerto `5089`.

---

## 1. Configuración de Nginx (Proxy Inverso Recomendado)

Crea o edita el archivo de sitio en `/etc/nginx/sites-available/hidrocontrol.potenzia.com.conf`:

```nginx
# ==============================================================================
# HIDROCONTROL PTAP - NGINX REVERSE PROXY CONFIGURATION
# Dominio: hidrocontrol.potenzia.com
# ==============================================================================

# 1. Redirección HTTP a HTTPS
server {
    listen 80;
    listen [::]:80;
    server_name hidrocontrol.potenzia.com;
    return 301 https://$host$request_uri;
}

# 2. Servidor Seguro HTTPS
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name hidrocontrol.potenzia.com;

    # Certificados SSL (Let's Encrypt / Certbot)
    ssl_certificate /etc/letsencrypt/live/hidrocontrol.potenzia.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/hidrocontrol.potenzia.com/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    # Encabezados de Seguridad y Frame-Ancestors para permitir embebido seguro (IFrame)
    add_header X-Content-Type-Options "nosniff" always;
    add_header Content-Security-Policy "frame-ancestors 'self' https://*.potenzia.com http://localhost:*" always;

    # Aumentar tamaño máximo de subida para reportes
    client_max_body_size 50M;

    # --------------------------------------------------------------------------
    # Frontend SCADA Blazor Server (.NET 8)
    # --------------------------------------------------------------------------
    location / {
        proxy_pass http://127.0.0.1:5088;
        proxy_http_version 1.1;

        # Soporte obligatorio para WebSockets de Blazor SignalR (/_blazor)
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";

        # Encabezados de Proxy para Kestrel
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Timeouts extendidos para conexiones persistentes de SCADA
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
        proxy_cache_bypass $http_upgrade;
    }

    # --------------------------------------------------------------------------
    # API y WebSockets de Node-RED (Opcional si se expone externamente)
    # --------------------------------------------------------------------------
    location /nodered/ {
        proxy_pass http://127.0.0.1:1880/;
        proxy_http_version 1.1;

        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        proxy_read_timeout 3600s;
    }

    # --------------------------------------------------------------------------
    # Pasarela WhatsApp (Para vinculación QR de operadores)
    # --------------------------------------------------------------------------
    location /whatsapp/ {
        proxy_pass http://127.0.0.1:5089/;
        proxy_http_version 1.1;

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 2. Configuración de CORS en Node-RED (`settings.js`)

Para que Node-RED acepte consultas directas desde el dominio temporal sin bloqueos de origen cruzado, abre el archivo de configuración de Node-RED (típicamente en `%USERPROFILE%\.node-red\settings.js` en Windows o `~/.node-red/settings.js` en Linux):

Busca la sección `httpNodeCors` y configúrala de la siguiente forma:

```javascript
// Habilitar CORS para endpoints HTTP de Node-RED (/api/...)
httpNodeCors: {
    origin: [
        "https://hidrocontrol.potenzia.com",
        "http://hidrocontrol.potenzia.com",
        "http://localhost:5088",
        "https://localhost:5088"
    ],
    methods: "GET,PUT,POST,DELETE,OPTIONS",
    credentials: true
},
```

---

## 3. Variables de Entorno para el Despliegue de Blazor

Al arrancar el servicio .NET en el servidor de producción o en contenedor Docker, define las siguientes variables:

```bash
# Entorno de Producción (activa appsettings.Production.json)
ASPNETCORE_ENVIRONMENT=Production

# Puertos locales en los que escucha Kestrel internamente tras Nginx
ASPNETCORE_URLS=http://127.0.0.1:5088

# URL donde Blazor encuentra a Node-RED (red interna o localhost)
NODERED_BASE_URL=http://127.0.0.1:1880/
```

O si se desea correr en **Modo Simulación Offline**:
```bash
# Apuntar Blazor al simulador local de telemetría
NODERED_BASE_URL=http://127.0.0.1:1881/
```

---

## 4. Script de Inicio Rápido de Producción / Prueba Online

Puedes ejecutar el servidor en producción con:
```bash
dotnet run --configuration Release --launch-profile http
```
Kestrel aplicará automáticamente los `ForwardedHeaders` configurados en [Program.cs](file:///d:/PROGRAMAS/CONTROL/HIDROCONTROL%20PTAP%20V2.2/dashboard/PTAPControl/Program.cs), interpretando las peticiones HTTPS que envía Nginx y evitando bucles de redirección.
