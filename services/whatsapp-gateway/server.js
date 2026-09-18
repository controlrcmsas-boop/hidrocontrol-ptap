const { default: makeWASocket, useMultiFileAuthState, DisconnectReason, fetchLatestBaileysVersion } = require("@whiskeysockets/baileys");
const pino = require("pino");
const express = require("express");
const cors = require("cors");
const QRCode = require("qrcode");
const path = require("path");
const fs = require("fs");

const app = express();
app.use(cors());
app.use(express.json());

const PORT = process.env.PORT || 5089;
const AUTH_DIR = path.join(__dirname, "auth_info");

let sock = null;
let currentQR = null;
let isConnected = false;
let connectedUser = null;
let qrImageBase64 = null;

async function startWhatsApp() {
    const { state, saveCreds } = await useMultiFileAuthState(AUTH_DIR);
    const { version } = await fetchLatestBaileysVersion();

    sock = makeWASocket({
        version,
        logger: pino({ level: "silent" }),
        printQRInTerminal: true,
        auth: state,
        browser: ["HIDROCONTROL PTAP", "Chrome", "1.0.0"]
    });

    sock.ev.on("creds.update", saveCreds);

    sock.ev.on("connection.update", async (update) => {
        const { connection, lastDisconnect, qr } = update;

        if (qr) {
            currentQR = qr;
            qrImageBase64 = await QRCode.toDataURL(qr, { width: 300, margin: 2 });
            console.log("[WhatsApp Gateway] Nuevo codigo QR generado.");
        }

        if (connection === "close") {
            const shouldReconnect = (lastDisconnect?.error)?.output?.statusCode !== DisconnectReason.loggedOut;
            isConnected = false;
            connectedUser = null;
            currentQR = null;
            qrImageBase64 = null;
            console.log("[WhatsApp Gateway] Conexion cerrada. Reconectar:", shouldReconnect);

            if (shouldReconnect) {
                setTimeout(startWhatsApp, 3000);
            }
        } else if (connection === "open") {
            isConnected = true;
            currentQR = null;
            qrImageBase64 = null;
            const jid = sock.user?.id || "";
            connectedUser = jid.split(":")[0] || jid.split("@")[0] || "Conectado";
            console.log(`[WhatsApp Gateway] Conectado exitosamente con el numero: +${connectedUser}`);
        }
    });
}

// Iniciar conexion WhatsApp
startWhatsApp();

// API: Estado de la conexion
app.get("/status", (req, res) => {
    res.json({
        connected: isConnected,
        user: connectedUser ? `+${connectedUser}` : null,
        hasQR: !!qrImageBase64
    });
});

// API: Enviar mensaje
app.post("/send", async (req, res) => {
    try {
        const { phone, message } = req.body || {};

        if (!isConnected || !sock) {
            return res.status(503).json({ success: false, error: "WhatsApp no esta conectado. Escanee el codigo QR primero." });
        }

        if (!phone || !message) {
            return res.status(400).json({ success: false, error: "Parametros 'phone' y 'message' son obligatorios." });
        }

        // Limpiar numero: solo digitos
        let cleanPhone = String(phone).replace(/[^0-9]/g, "");
        if (!cleanPhone.endsWith("@s.whatsapp.net")) {
            cleanPhone = `${cleanPhone}@s.whatsapp.net`;
        }

        const sent = await sock.sendMessage(cleanPhone, { text: String(message) });
        console.log(`[WhatsApp Gateway] Mensaje enviado a ${cleanPhone}`);
        return res.json({ success: true, id: sent?.key?.id });
    } catch (err) {
        console.error("[WhatsApp Gateway] Error enviando mensaje:", err);
        return res.status(500).json({ success: false, error: err.message });
    }
});

// API / UI: Pagina web para escanear QR
app.get(["/", "/qr"], (req, res) => {
    const html = `
    <!DOCTYPE html>
    <html lang="es">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Vinculacion WhatsApp - HIDROCONTROL PTAP</title>
        <style>
            body {
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                background: #0d151b;
                color: #e8f4f6;
                display: flex;
                flex-direction: column;
                align-items: center;
                justify-content: center;
                min-height: 100vh;
                margin: 0;
                padding: 20px;
                box-sizing: border-box;
            }
            .card {
                background: #111f27;
                border: 1px solid rgba(163, 199, 209, 0.22);
                border-radius: 12px;
                padding: 28px;
                max-width: 440px;
                width: 100%;
                text-align: center;
                box-shadow: 0 16px 36px rgba(0,0,0,0.5);
            }
            h1 { font-size: 1.3rem; margin-top: 0; color: #4ec8d8; }
            .status {
                display: inline-flex;
                align-items: center;
                gap: 8px;
                padding: 6px 14px;
                border-radius: 20px;
                font-size: 0.9rem;
                font-weight: 600;
                margin-bottom: 20px;
            }
            .status.ok { background: rgba(72, 213, 151, 0.15); color: #48d597; border: 1px solid rgba(72, 213, 151, 0.4); }
            .status.wait { background: rgba(231, 199, 101, 0.15); color: #e7c765; border: 1px solid rgba(231, 199, 101, 0.4); }
            .qr-box {
                background: #fff;
                padding: 14px;
                border-radius: 8px;
                display: inline-block;
                margin: 10px 0;
            }
            .qr-box img { display: block; max-width: 100%; height: auto; }
            ol { text-align: left; font-size: 0.88rem; color: #93a9af; line-height: 1.5; padding-left: 20px; }
            .btn {
                background: #ff6b6b;
                color: #fff;
                border: none;
                padding: 8px 16px;
                border-radius: 6px;
                cursor: pointer;
                font-size: 0.88rem;
                margin-top: 14px;
            }
        </style>
    </head>
    <body>
        <div class="card">
            <h1>HIDROCONTROL PTAP</h1>
            <h3>Emisor Oficial de WhatsApp</h3>
            
            <div id="status-area">
                ${
                    isConnected
                        ? `<div class="status ok">🟢 Conectado como: +${connectedUser}</div>
                           <p style="color: #93a9af; font-size: 0.9rem;">El sistema esta listo para enviar alarmas a los operadores.</p>
                           <form method="POST" action="/disconnect"><button class="btn" type="submit">Desvincular numero</button></form>`
                        : `<div class="status wait">🟡 Esperando escaneo QR...</div>
                           ${qrImageBase64 ? `<div class="qr-box"><img src="${qrImageBase64}" alt="Codigo QR" /></div>` : `<p>Generando QR...</p>`}
                           <ol>
                               <li>Abre WhatsApp en el celular que enviara las alertas.</li>
                               <li>Toca <b>Dispositivos vinculados</b> > <b>Vincular un dispositivo</b>.</li>
                               <li>Apunta tu camara hacia este codigo QR.</li>
                           </ol>`
                }
            </div>
        </div>
        <script>
            // Auto recargar si no esta conectado para actualizar QR o confirmar conexion
            ${!isConnected ? 'setTimeout(() => window.location.reload(), 4000);' : ''}
        </script>
    </body>
    </html>
    `;
    res.send(html);
});

// API: Desvincular
app.post("/disconnect", (req, res) => {
    try {
        if (sock) {
            sock.logout();
        }
        if (fs.existsSync(AUTH_DIR)) {
            fs.rmSync(AUTH_DIR, { recursive: true, force: true });
        }
        isConnected = false;
        connectedUser = null;
        currentQR = null;
        qrImageBase64 = null;
        console.log("[WhatsApp Gateway] Sesion desvinculada.");
        setTimeout(startWhatsApp, 2000);
        res.redirect("/qr");
    } catch (e) {
        res.status(500).send("Error desvinculando: " + e.message);
    }
});

app.listen(PORT, "0.0.0.0", () => {
    console.log(`====================================================`);
    console.log(`  WhatsApp Gateway HIDROCONTROL PTAP`);
    console.log(`  Servidor activo en: http://0.0.0.0:${PORT}`);
    console.log(`  Escanear QR en:     http://0.0.0.0:${PORT}/qr`);
    console.log(`====================================================`);
});
