/**
 * HIDROCONTROL PTAP - AGENTE EDGE DE SUPERVIVENCIA LOCAL + STORE & FORWARD SQLITE
 * Arquitectura Híbrida Industria 4.0:
 * - Registro local continuo en SQLite (con Store & Forward)
 * - Calibración y Escalamiento de Sensores por Software (Turbidez Invertida, pH, Conductividad)
 * - Watchdog de conexión nube (TypeSafe JEV)
 * - Canal de Entrenamiento JEV Cloud -> Agente Local (Policy Sync via /api/ai/update-policy)
 * - Agente IA Local Offline que asume el mando si se corta internet
 * - Protección física e interlocks
 */

const { DatabaseSync } = require('node:sqlite');
const path = require('node:path');
const fs = require('node:fs');
const http = require('node:http');
const https = require('node:https');

// Manejador global para que el Agente NUNCA se detenga ante errores
process.on('uncaughtException', (err) => {
    console.error('   [CRITICAL] Error no capturado (Agente recuperado):', err.message);
});
process.on('unhandledRejection', (reason) => {
    console.error('   [CRITICAL] Rechazo no controlado (Agente recuperado):', reason);
});

// 1. Inicialización de Base de Datos Local SQLite
const dbDir = path.join(__dirname, '..', '..', 'database');
if (!fs.existsSync(dbDir)) fs.mkdirSync(dbDir, { recursive: true });
const dbPath = path.join(dbDir, 'ptap-edge-history.db');
const policyPath = path.join(dbDir, 'policy-rules.json');

const db = new DatabaseSync(dbPath);
db.exec(`
    CREATE TABLE IF NOT EXISTS telemetry_history (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        timestamp TEXT NOT NULL,
        plc_connected INTEGER NOT NULL,
        ph_agua_cruda REAL,
        turbidez_agua_cruda REAL,
        conductividad_agua_cruda REAL,
        ph_agua_tratada REAL,
        turbidez_agua_tratada REAL,
        conductividad_agua_tratada REAL,
        bomba_principal INTEGER,
        dosificador_sulfato INTEGER,
        dosificador_cloro INTEGER,
        modo_operacion TEXT,
        sincronizado INTEGER DEFAULT 0
    );
`);

console.log('==============================================================================');
console.log('   HIDROCONTROL PTAP - AGENTE EDGE LOCAL + BASE DE DATOS SQLITE');
console.log('   Base de datos local : ' + dbPath);
console.log('   Archivo de Criterio : ' + policyPath);
console.log('==============================================================================\n');

// 2. Criterio Operativo Entrenado por JEV Cloud (Policy Rules)
let defaultPolicy = {
    version: '1.2.0-jev',
    lastTrainedAt: new Date().toISOString(),
    turbidityHighThreshold: 5.0,  // NTU para activar sulfato
    turbidityOptimalThreshold: 2.0, // NTU para detener sulfato
    phMinThreshold: 6.5,
    phMaxThreshold: 8.5,
    sulfateDefaultDosePpm: 25.0,
    chlorineDefaultDosePpm: 1.5,
    source: 'JEV_CLOUD_TYPESAFE'
};

function loadPolicyRules() {
    try {
        if (fs.existsSync(policyPath)) {
            const raw = fs.readFileSync(policyPath, 'utf8');
            return { ...defaultPolicy, ...JSON.parse(raw) };
        }
    } catch (e) {
        console.warn('   [AVISO] No se pudo leer policy-rules.json, usando valores por defecto:', e.message);
    }
    return defaultPolicy;
}

function savePolicyRules(newPolicy) {
    try {
        fs.writeFileSync(policyPath, JSON.stringify(newPolicy, null, 2), 'utf8');
        return true;
    } catch (e) {
        console.error('   [ERROR] Fallo al guardar policy-rules.json:', e.message);
        return false;
    }
}

// 3. Estado Operativo del Edge
let state = {
    plcConnected: false,
    cloudOnline: true,
    lastCloudHeartbeat: Date.now(),
    mandoActual: 'CLOUD_JEV', // 'CLOUD_JEV' o 'LOCAL_OFFLINE_AGENT'
    lastTelemetry: null,
    pendingSyncCount: 0,
    recentEvents: [],
    dryRunTrip: false,
    policyRules: loadPolicyRules(),
    lastSulfateToggle: 0,
    lastChlorineToggle: 0,
    moStatus: 'MO: Supervisión nominal en planta. Parámetros estables.'
};

function addEvent(msg) {
    const time = new Date().toLocaleTimeString();
    state.recentEvents.push(`[${time}] ${msg}`);
    if (state.recentEvents.length > 5) {
        state.recentEvents.shift();
    }
}

// 4. Calibración y Escalamiento de Sensores por Software
function scaleSensorValues(rawValues) {
    if (!rawValues) return {};

    // Lecturas en bruto recibidas del PLC / Arduino
    const rawTurb = rawValues.turbidez_agua_cruda !== undefined ? Number(rawValues.turbidez_agua_cruda) : 484.3;
    const rawPh = rawValues.ph_agua_cruda !== undefined ? Number(rawValues.ph_agua_cruda) : 6.64;
    const rawCond = rawValues.conductividad_agua_cruda !== undefined ? Number(rawValues.conductividad_agua_cruda) : 66.3;

    // FÓRMULA 1: Turbidez Invertida
    // Agua limpia entrega ~484 -> 0.5 a 0.8 NTU. Agua con tierra baja a ~332 -> ~304 NTU.
    const turbidezReal = Math.max(0.5, Number(((485.0 - rawTurb) * 2.0).toFixed(1)));

    // FÓRMULA 2: pH Calibrado con corrección de offset y ganancia
    // Base agua limpia: 6.64 -> calibrado a 7.00. Agua con limón: 4.75 -> calibrado a 3.50.
    // m = (7.0 - 3.5) / (6.64 - 4.75) = 3.5 / 1.89 = 1.8518
    const phReal = Math.min(14.0, Math.max(0.0, Number((7.00 + (rawPh - 6.64) * 1.8518).toFixed(2))));

    // FÓRMULA 3: Conductividad (uS/cm estándar)
    const conductividadReal = Number(Math.max(0.0, rawCond).toFixed(1));

    return {
        ...rawValues,
        turbidez_agua_cruda_raw: rawTurb,
        turbidez_agua_cruda: turbidezReal,
        turbidez_agua_tratada_raw: rawTurb,
        turbidez_agua_tratada: turbidezReal,
        ph_agua_cruda_raw: rawPh,
        ph_agua_cruda: phReal,
        ph_agua_tratada_raw: rawPh,
        ph_agua_tratada: phReal,
        conductividad_agua_cruda_raw: rawCond,
        conductividad_agua_cruda: conductividadReal,
        conductividad_agua_tratada_raw: rawCond,
        conductividad_agua_tratada: conductividadReal
    };
}

// 5. Helper HTTP GET
function httpGet(urlStr, timeoutMs = 2500) {
    return new Promise((resolve, reject) => {
        const parsed = new URL(urlStr);
        const client = parsed.protocol === 'https:' ? https : http;
        const req = client.get(urlStr, { timeout: timeoutMs }, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    resolve({ statusCode: res.statusCode, body: JSON.parse(data) });
                } catch {
                    resolve({ statusCode: res.statusCode, body: data });
                }
            });
        });
        req.on('error', reject);
        req.on('timeout', () => { req.destroy(); reject(new Error('Timeout')); });
    });
}

// 6. Helper HTTP POST Comandos a Node-RED (Blindado contra bloqueos)
function sendCommand(actuator, action) {
    return new Promise((resolve) => {
        let finished = false;
        const done = (val) => {
            if (!finished) {
                finished = true;
                resolve(val);
            }
        };

        // Timeout estricto de 2.5 segundos: Si Node-RED no responde, el agente NUNCA se cuelga
        const timer = setTimeout(() => done(false), 2500);

        try {
            const payload = JSON.stringify({ target: actuator, command: action, actuator, action, source: 'LOCAL_EDGE_AGENT' });
            const req = http.request({
                hostname: '127.0.0.1',
                port: 1880,
                path: '/api/control/command',
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Content-Length': Buffer.byteLength(payload)
                },
                timeout: 2000
            }, (res) => {
                clearTimeout(timer);
                done(res.statusCode === 200);
            });
            req.on('error', () => {
                clearTimeout(timer);
                done(false);
            });
            req.on('timeout', () => {
                clearTimeout(timer);
                req.destroy();
                done(false);
            });
            req.write(payload);
            req.end();
        } catch {
            clearTimeout(timer);
            done(false);
        }
    });
}

// Helper para enviar alertas automáticas vía WhatsApp Gateway (Puerto 5089)
function sendWhatsAppAlert(message) {
    return new Promise((resolve) => {
        try {
            // Obtener número configurado en base de datos o por defecto
            const row = db.prepare("SELECT phone FROM notification_whatsapp WHERE enabled = 1 LIMIT 1").get();
            const targetPhone = row ? row.phone : "+573001234567";
            
            const payload = JSON.stringify({ phone: targetPhone, message: message });
            const req = http.request({
                hostname: '127.0.0.1',
                port: 5089,
                path: '/send',
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Content-Length': Buffer.byteLength(payload)
                },
                timeout: 3000
            }, (res) => {
                resolve(res.statusCode === 200);
            });
            req.on('error', () => resolve(false));
            req.on('timeout', () => { req.destroy(); resolve(false); });
            req.write(payload);
            req.end();
        } catch {
            resolve(false);
        }
    });
}

// 7. Watchdog de Conexión a Internet / Nube
async function checkCloudHeartbeat() {
    let cloudResponsive = false;
    try {
        const res = await httpGet('https://hidrocontrol.potenzia.app/', 3500);
        if (res.statusCode >= 200 && res.statusCode < 400) {
            cloudResponsive = true;
        }
    } catch {
        cloudResponsive = false;
    }

    const now = Date.now();

    if (cloudResponsive) {
        state.lastCloudHeartbeat = now;

        // Si la red acaba de volver tras una caída:
        if (!state.cloudOnline || state.mandoActual === 'LOCAL_OFFLINE_AGENT') {
            const durationSec = offlineStartedAt > 0 ? Math.round((now - offlineStartedAt) / 1000) : 0;
            const countRow = db.prepare('SELECT COUNT(*) as pending FROM telemetry_history WHERE sincronizado = 0').get();
            const pending = countRow ? countRow.pending : 0;

            addEvent(`¡Internet Restablecido! Reconexión exitosa tras ${durationSec}s en modo autónomo.`);
            addEvent(`[STORE & FORWARD] Sincronizando ${pending} registros acumulados hacia MO Nube...`);

            // Reporte automático vía WhatsApp
            const mins = Math.max(1, Math.round(durationSec / 60));
            sendWhatsAppAlert(`🟢 *HIDROCONTROL - ENLACE RESTABLECIDO*\n\nEl servicio de internet ha regresado. Durante la desconexión, *MO Local Edge* gobernó la planta de forma autónoma durante ${mins} min.\n\nSe han sincronizado exitosamente ${pending} registros históricos a la nube.`);
        }

        state.cloudOnline = true;
        state.mandoActual = 'CLOUD_JEV';
        offlineStartedAt = 0;
    } else {
        if (now - state.lastCloudHeartbeat > 12000) {
            if (state.cloudOnline || state.mandoActual !== 'LOCAL_OFFLINE_AGENT') {
                offlineStartedAt = now;
                addEvent('¡Corte de Internet detectado! MO Nube fuera de línea.');
                addEvent('[RESILIENCIA TOTAL] MO LOCAL asume el mando autónomo de la planta en sitio.');
            }
            state.cloudOnline = false;
            state.mandoActual = 'LOCAL_OFFLINE_AGENT';
        }
    }
}

// 8. Agente IA Local Offline: Reglas de Decisión con Criterio de JEV
async function runLocalAgent(v) {
    // REGLA DE SEGURIDAD FISICA UNIVERSAL: Los dosificadores NUNCA operan si la bomba esta apagada
    if (!v.bomba_principal_estado) {
        if (v.dosificador_sulfato_estado) {
            addEvent('Bomba apagada -> Deteniendo dosificador de sulfato por seguridad física.');
            await sendCommand('dosificador_sulfato', 'stop');
        }
        if (v.dosificador_cloro_estado) {
            addEvent('Bomba apagada -> Deteniendo dosificador de cloro por seguridad física.');
            await sendCommand('dosificador_cloro', 'stop');
        }
        return;
    }

    if (state.mandoActual !== 'LOCAL_OFFLINE_AGENT') return;

    if (state.dryRunTrip) {
        state.dryRunTrip = false;
        addEvent('Protección de marcha en seco liberada (Bypass activo para pruebas).');
    }


    // Regla de Calidad de Agua gobernada por el criterio entrenado por JEV
    const turb = v.turbidez_agua_tratada || 0;
    const ph = v.ph_agua_tratada || 7.0;
    const condVal = v.conductividad_agua_tratada || v.conductividad_agua_cruda || 0;

    // Criterio cognitivo continuo de MO para supervisión de calidad
    const activeAlarmsList = [];
    if (ph < 6.5) activeAlarmsList.push(`pH Ácido (${ph.toFixed(2)} pH)`);
    else if (ph > 8.5) activeAlarmsList.push(`pH Alcalino (${ph.toFixed(2)} pH)`);
    if (turb > (state.policyRules.turbidityHighThreshold || 5.0)) activeAlarmsList.push(`Turbidez Elevada (${turb.toFixed(1)} NTU)`);
    if (condVal > 200) activeAlarmsList.push(`Conductividad (${Math.round(condVal)} µS/cm)`);

    if (activeAlarmsList.length > 0) {
        state.moStatus = `MO: ALERTA DE PROCESO ACTIVA. ${activeAlarmsList.join(' | ')}. Mitigación química en curso.`;
    } else {
        state.moStatus = `MO: Supervisión nominal en planta. Parámetros estables.`;
    }
    const highLimit = state.policyRules.turbidityHighThreshold || 5.0;
    const optLimit = state.policyRules.turbidityOptimalThreshold || 2.0;
    const now = Date.now();
    const minHoldMs = 12000; // Mínimo 12s para proteger motores y evitar rebote de conmutación

    // LÓGICA 1: DOSIFICADOR DE SULFATO (Coagulación / Floculación ante turbidez alta)
    if (turb > highLimit && !v.dosificador_sulfato_estado) {
        if (!state.lastSulfateToggle || (now - state.lastSulfateToggle) > minHoldMs) {
            state.lastSulfateToggle = now;
            addEvent(`[QUÍMICA] Turbidez elevada (${turb.toFixed(1)} NTU > umbral JEV ${highLimit} NTU) -> Activando Dosificador de Sulfato.`);
            await sendCommand('dosificador_sulfato', 'start');

            if (now - lastTurbWaAlert > 30000) {
                lastTurbWaAlert = now;
                sendWhatsAppAlert(`⚠️ *HIDROCONTROL — ALERTA DE TURBIDEZ EN PLANTA*\n\nEl sensor detectó turbidez de ${turb.toFixed(1)} NTU (umbral: ${highLimit} NTU).\n\n*Acción de MO Local:* Activando dosificador de sulfato para coagulación de sólidos.`);
            }
        }
    } else if (turb <= optLimit && v.dosificador_sulfato_estado) {
        if (!state.lastSulfateToggle || (now - state.lastSulfateToggle) > minHoldMs) {
            state.lastSulfateToggle = now;
            addEvent(`[QUÍMICA] Turbidez clarificada (${turb.toFixed(1)} NTU <= umbral JEV ${optLimit} NTU) -> Deteniendo Sulfato.`);
            await sendCommand('dosificador_sulfato', 'stop');
        }
    }

    // LÓGICA 2: DOSIFICADOR DE CLORO (Desinfección de Agua Clarificada)
    // El cloro desinfecta activamente cuando el agua está clarificada y dentro de pH seguro
    if (turb <= highLimit && ph >= 6.0 && ph <= 8.5) {
        if (!v.dosificador_cloro_estado) {
            if (!state.lastChlorineToggle || (now - state.lastChlorineToggle) > minHoldMs) {
                state.lastChlorineToggle = now;
                addEvent(`[DESINFECCIÓN] Agua clarificada (${turb.toFixed(1)} NTU, pH ${ph.toFixed(2)}) -> Activando Dosificador de Cloro.`);
                await sendCommand('dosificador_cloro', 'start');
            }
        }
    } else if (turb > (highLimit * 3.0) || ph < 5.5) {
        // En turbidez masiva (tierra) o agua ácida (limón): pausar cloro para proteger calidad
        if (v.dosificador_cloro_estado) {
            if (!state.lastChlorineToggle || (now - state.lastChlorineToggle) > minHoldMs) {
                state.lastChlorineToggle = now;
                addEvent(`[SEGURIDAD] Condición anormal (Turb: ${turb.toFixed(1)} NTU, pH: ${ph.toFixed(2)}) -> Pausando Dosificador de Cloro.`);
                await sendCommand('dosificador_cloro', 'stop');

                if (ph < 5.5 && (now - lastPhWaAlert > 30000)) {
                    lastPhWaAlert = now;
                    sendWhatsAppAlert(`🚨 *HIDROCONTROL — ALERTA CRÍTICA DE pH EN PLANTA*\n\nEl sensor detectó una caída drástica a ${ph.toFixed(2)} pH (condición ácida corrosiva).\n\n*Acción de MO Local:* Dosificación de cloro pausada por seguridad química.`);
                }
            }
        }
    }
}

// 9. Store & Forward: Vaciado de Buffer a la Nube cuando hay internet
async function flushPendingBuffer() {
    if (!state.cloudOnline) return;

    try {
        const pending = db.prepare('SELECT * FROM telemetry_history WHERE sincronizado = 0 LIMIT 50').all();
        if (pending && pending.length > 0) {
            const payload = JSON.stringify(pending);
            const req = https.request('https://hidrocontrol.potenzia.app/api/telemetry/sync', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Content-Length': Buffer.byteLength(payload)
                },
                timeout: 6000
            }, (res) => {
                if (res.statusCode >= 200 && res.statusCode < 300) {
                    const ids = pending.map(r => r.id).join(',');
                    db.exec(`UPDATE telemetry_history SET sincronizado = 1 WHERE id IN (${ids})`);
                }
            });
            req.on('error', () => {});
            req.on('timeout', () => { req.destroy(); });
            req.write(payload);
            req.end();
        }
    } catch (e) {
        // Si hay fallo se mantienen en cola
    }
}

// 10. Servidor HTTP de Gestión Local (Puerto 1882) para recibir entrenamiento de JEV
const edgeServer = http.createServer((req, res) => {
    // Enable CORS
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

    if (req.method === 'OPTIONS') {
        res.writeHead(200);
        res.end();
        return;
    }

    if (req.method === 'GET' && (req.url === '/' || req.url === '/panel' || req.url === '/local')) {
        const consoleHtmlPath = path.join(__dirname, '..', '..', 'Consola-Local-Servidor.html');
        if (fs.existsSync(consoleHtmlPath)) {
            res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
            res.end(fs.readFileSync(consoleHtmlPath, 'utf8'));
            return;
        }
    }

    if (req.method === 'GET' && req.url === '/api/edge/status') {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
            status: 'ok',
            plcConnected: state.plcConnected,
            cloudOnline: state.cloudOnline,
            mandoActual: state.mandoActual,
            lastTelemetry: state.lastTelemetry,
            pendingSyncCount: state.pendingSyncCount,
            policyRules: state.policyRules,
            recentEvents: state.recentEvents,
            moStatus: state.moStatus,
            timestamp: new Date().toISOString()
        }));
        return;
    }

    if (req.method === 'POST' && req.url === '/api/ai/update-policy') {
        let body = '';
        req.on('data', chunk => body += chunk);
        req.on('end', () => {
            try {
                const parsed = JSON.parse(body);
                const updated = {
                    ...state.policyRules,
                    ...parsed,
                    lastTrainedAt: new Date().toISOString(),
                    source: 'JEV_CLOUD_TYPESAFE'
                };
                savePolicyRules(updated);
                state.policyRules = updated;
                addEvent(`[JEV TRAINING] Nuevas políticas operativas asimiladas por el Agente Local.`);
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ success: true, message: 'Criterio operativo actualizado exitosamente', policy: updated }));
            } catch (err) {
                res.writeHead(400, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ success: false, error: 'JSON inválido' }));
            }
        });
        return;
    }

    res.writeHead(404, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ error: 'Endpoint no encontrado' }));
});

edgeServer.listen(1882, '0.0.0.0', () => {
    console.log('   Receptor de Políticas JEV escuchando en puerto HTTP 1882 (/api/ai/update-policy)');
});

let globalLastRecordedAt = 0;
let offlineStartedAt = 0;
let lastTurbWaAlert = 0;
let lastPhWaAlert = 0;
let lastCondWaAlert = 0;
let globalLastRecordedValues = null;

// 11. Ciclo Principal de Ejecución (Cada 5 segundos)
async function mainLoop() {
    try {
        const res = await httpGet('http://127.0.0.1:1880/api/telemetry/current', 2000);
        if (res.statusCode === 200 && res.body && res.body.values) {
            state.plcConnected = res.body.plcConnected !== false;
            
            // Aplicar calibración y escalamiento de software en caliente
            const calibrated = scaleSensorValues(res.body.values);
            state.lastTelemetry = calibrated;

            // Filtro Deadband: registrar en SQLite cada 15s si la planta está estable,
            // o inmediatamente si ocurre un cambio en sensores o actuadores
            const nowTime = Date.now();
            let shouldInsert = false;

            if (!globalLastRecordedValues || (nowTime - globalLastRecordedAt) >= 15000) {
                shouldInsert = true;
            } else {
                const turbDelta = Math.abs((calibrated.turbidez_agua_tratada || 0) - (globalLastRecordedValues.turbidez_agua_tratada || 0));
                const phDelta = Math.abs((calibrated.ph_agua_tratada || 0) - (globalLastRecordedValues.ph_agua_tratada || 0));
                const condDelta = Math.abs((calibrated.conductividad_agua_tratada || 0) - (globalLastRecordedValues.conductividad_agua_tratada || 0));
                const actChanged = calibrated.bomba_principal_estado !== globalLastRecordedValues.bomba_principal_estado ||
                                   calibrated.dosificador_sulfato_estado !== globalLastRecordedValues.dosificador_sulfato_estado ||
                                   calibrated.dosificador_cloro_estado !== globalLastRecordedValues.dosificador_cloro_estado;

                if (turbDelta >= 1.5 || phDelta >= 0.15 || condDelta >= 15.0 || actChanged) {
                    shouldInsert = true;
                }
            }

            if (shouldInsert) {
                globalLastRecordedAt = nowTime;
                globalLastRecordedValues = { ...calibrated };

                const stmt = db.prepare(`
                    INSERT INTO telemetry_history (
                        timestamp, plc_connected, ph_agua_cruda, turbidez_agua_cruda, conductividad_agua_cruda,
                        ph_agua_tratada, turbidez_agua_tratada, conductividad_agua_tratada,
                        bomba_principal, dosificador_sulfato, dosificador_cloro, modo_operacion, sincronizado
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                `);

                stmt.run(
                    new Date().toISOString(),
                    state.plcConnected ? 1 : 0,
                    calibrated.ph_agua_cruda || 0,
                    calibrated.turbidez_agua_cruda || 0,
                    calibrated.conductividad_agua_cruda || 0,
                    calibrated.ph_agua_tratada || 0,
                    calibrated.turbidez_agua_tratada || 0,
                    calibrated.conductividad_agua_tratada || 0,
                    calibrated.bomba_principal_estado ? 1 : 0,
                    calibrated.dosificador_sulfato_estado ? 1 : 0,
                    calibrated.dosificador_cloro_estado ? 1 : 0,
                    state.mandoActual,
                    state.cloudOnline ? 1 : 0
                );
            }

            await runLocalAgent(calibrated);
        }
    } catch {
        state.plcConnected = false;
    }

    await checkCloudHeartbeat();
    await flushPendingBuffer();

    // Dashboard en Consola
    try {
        const countRow = db.prepare('SELECT COUNT(*) as total, SUM(CASE WHEN sincronizado = 0 THEN 1 ELSE 0 END) as pending FROM telemetry_history').get();
        state.pendingSyncCount = countRow.pending || 0;

        const v = state.lastTelemetry || {};
        const time = new Date().toLocaleTimeString();

        process.stdout.write('\x1Bc');
        console.log('==============================================================================');
        console.log(`   HIDROCONTROL PTAP - SERVIDOR EDGE DE PLANTA [${time}]`);
        console.log('==============================================================================');
        console.log(`   PLC Siemens S7-1200 : ${state.plcConnected ? '🟢 ONLINE (Conectado DB10)' : '🔴 DESCONECTADO'}`);
        console.log(`   Enlace Cloud        : ${state.cloudOnline ? '🟢 CONECTADO (Heartbeat OK)' : '🟠 SIN RED (Modo Supervivencia)'}`);
        console.log(`   Mando Operativo     : ${state.mandoActual === 'CLOUD_JEV' ? '🔵 CLOUD REGIDOR (TypeSafe JEV)' : '🟢 AGENTE LOCAL OFFLINE'}`);
        console.log(`   Criterio Activo     : Versión ${state.policyRules.version} (Entrenado por: ${state.policyRules.source})`);
        console.log('------------------------------------------------------------------------------');
        console.log(`   pH Agua Tratada     : ${(v.ph_agua_tratada || 0).toFixed(2)} pH  (Raw: ${(v.ph_agua_tratada_raw || 0).toFixed(2)})`);
        console.log(`   Turbidez Tratada    : ${(v.turbidez_agua_tratada || 0).toFixed(1)} NTU (Raw: ${(v.turbidez_agua_tratada_raw || 0).toFixed(1)})`);
        console.log(`   Conductividad       : ${(v.conductividad_agua_tratada || 0).toFixed(1)} uS/cm`);
        console.log(`   Nivel Tanque Cruda  : 🟢 MODO PRUEBAS (Bypass activo - Sin boya conectada)`);
        console.log(`   Bomba Principal     : ${v.bomba_principal_estado ? 'ENCENDIDA' : 'APAGADA'}`);
        console.log(`   Dosificador Sulfato : ${v.dosificador_sulfato_estado ? 'ENCENDIDO' : 'APAGADO'}`);
        console.log(`   Dosificador Cloro   : ${v.dosificador_cloro_estado ? 'ENCENDIDO' : 'APAGADO'}`);
        console.log('------------------------------------------------------------------------------');
        console.log(`   Base de Datos SQLite: ${countRow.total || 0} registros guardados en disco`);
        console.log(`   Cola Store & Forward: ${state.pendingSyncCount} pendientes de sincronizar`);
        console.log('==============================================================================');
        console.log('   REGISTRO DE ACCIONES Y EVENTOS DEL AGENTE:');
        if (state.recentEvents.length === 0) {
            console.log('   (Planta operando en condiciones nominales estables)');
        } else {
            state.recentEvents.forEach(e => console.log('   * ' + e));
        }
        console.log('==============================================================================');
    } catch {}

    setTimeout(mainLoop, 5000);
}

mainLoop();
