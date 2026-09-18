#!/usr/bin/env node
/**
 * ===============================================================================
 * HIDROCONTROL PTAP V2.2 - MOTOR DE SIMULACIÓN ESTOCÁSTICA Y FÍSICA (NODE.JS)
 * ===============================================================================
 * Emula la respuesta dinámica hidrodinámica y química de la planta potabilizadora
 * cuando el PLC Siemens S7-1200 físico (192.168.1.110) no está conectado.
 * 
 * Sin dependencias externas (utiliza únicamente módulos nativos 'http', 'url').
 */

const http = require("http");
const url = require("url");

const PORT = parseInt(process.env.SIM_PORT || "1881", 10);

// =============================================================================
// ESTADO DE LA PLANTA SIMULADA
// =============================================================================
class PlantState {
    constructor() {
        this.bomba_principal_estado = true;
        this.dosificador_sulfato_estado = true;
        this.dosificador_cloro_estado = true;
        this.nivel_tanque_agua_cruda = true;
        this.modo_remoto_habilitado = true;
        this.plc_en_falla = false;
        this.comando_rechazado = false;
        this.ultimo_comando_aceptado = true;

        this.watchdog_plc = 1000;
        this.command_sequence = 1;
        this.last_command_result = "Simulador iniciado";

        this.ph_cruda_base = 7.25;
        this.turbidez_cruda_base = 18.5;
        this.conductividad_cruda_base = 425.0;

        this.caudal_b1_base = 15.0;
        this.caudal_dist_base = 14.6;
        this.presion_b1_base = 2.45;

        this.scenario = "normal";
        this.step_counter = 0;
    }

    getTunableState() {
        return {
            ph_cruda_base: parseFloat(this.ph_cruda_base.toFixed(2)),
            turbidez_cruda_base: parseFloat(this.turbidez_cruda_base.toFixed(1)),
            conductividad_cruda_base: parseFloat(this.conductividad_cruda_base.toFixed(0)),
            presion_b1_base: parseFloat(this.presion_b1_base.toFixed(2)),
            caudal_b1_base: parseFloat(this.caudal_b1_base.toFixed(1)),
            bomba_principal_estado: this.bomba_principal_estado,
            dosificador_sulfato_estado: this.dosificador_sulfato_estado,
            dosificador_cloro_estado: this.dosificador_cloro_estado,
            nivel_tanque_agua_cruda: this.nivel_tanque_agua_cruda,
            modo_remoto_habilitado: this.modo_remoto_habilitado,
            plc_en_falla: this.plc_en_falla,
            scenario: this.scenario
        };
    }

    setTunableState(params) {
        if (params.ph_cruda_base !== undefined) this.ph_cruda_base = Math.max(0, Math.min(14, parseFloat(params.ph_cruda_base)));
        if (params.turbidez_cruda_base !== undefined) this.turbidez_cruda_base = Math.max(0, Math.min(500, parseFloat(params.turbidez_cruda_base)));
        if (params.conductividad_cruda_base !== undefined) this.conductividad_cruda_base = Math.max(0, Math.min(3000, parseFloat(params.conductividad_cruda_base)));
        if (params.presion_b1_base !== undefined) this.presion_b1_base = Math.max(0, Math.min(10, parseFloat(params.presion_b1_base)));
        if (params.caudal_b1_base !== undefined) this.caudal_b1_base = Math.max(0, Math.min(60, parseFloat(params.caudal_b1_base)));
        if (params.bomba_principal_estado !== undefined) this.bomba_principal_estado = Boolean(params.bomba_principal_estado);
        if (params.dosificador_sulfato_estado !== undefined) this.dosificador_sulfato_estado = Boolean(params.dosificador_sulfato_estado);
        if (params.dosificador_cloro_estado !== undefined) this.dosificador_cloro_estado = Boolean(params.dosificador_cloro_estado);
        if (params.nivel_tanque_agua_cruda !== undefined) this.nivel_tanque_agua_cruda = Boolean(params.nivel_tanque_agua_cruda);
        if (params.modo_remoto_habilitado !== undefined) this.modo_remoto_habilitado = Boolean(params.modo_remoto_habilitado);
        if (params.plc_en_falla !== undefined) this.plc_en_falla = Boolean(params.plc_en_falla);
        if (params.scenario !== undefined) this.scenario = String(params.scenario);
        return this.getTunableState();
    }

    setScenario(scenarioName) {
        this.scenario = scenarioName.toLowerCase();
        if (this.scenario === "rain") {
            this.turbidez_cruda_base = 78.0;
            this.ph_cruda_base = 6.85;
        } else if (this.scenario === "pump_fault") {
            this.bomba_principal_estado = false;
            this.plc_en_falla = true;
        } else if (this.scenario === "chlorine_fault") {
            this.dosificador_cloro_estado = false;
        } else {
            // Normal
            this.turbidez_cruda_base = 18.5;
            this.ph_cruda_base = 7.25;
            this.bomba_principal_estado = true;
            this.dosificador_sulfato_estado = true;
            this.dosificador_cloro_estado = true;
            this.plc_en_falla = false;
        }
    }

    executeCommand(target, command) {
        this.command_sequence++;
        const cmdKey = `${target}_${command}`;

        if (!this.modo_remoto_habilitado) {
            this.comando_rechazado = true;
            this.ultimo_comando_aceptado = false;
            return { accepted: false, message: "Comando rechazado: Modo remoto deshabilitado" };
        }

        if (target === "bomba_principal") {
            this.bomba_principal_estado = (command === "start");
        } else if (target === "dosificador_sulfato") {
            this.dosificador_sulfato_estado = (command === "start");
        } else if (target === "dosificador_cloro") {
            this.dosificador_cloro_estado = (command === "start");
        } else if (target === "ALL" || target === "planta") {
            this.bomba_principal_estado = false;
            this.dosificador_sulfato_estado = false;
            this.dosificador_cloro_estado = false;
        } else {
            this.comando_rechazado = true;
            return { accepted: false, message: `Equipo desconocido: ${target}` };
        }

        this.comando_rechazado = false;
        this.ultimo_comando_aceptado = true;
        this.last_command_result = `Ejecutado ${command} en ${target}`;
        return { accepted: true, message: `Comando ${cmdKey} procesado con éxito por simulador` };
    }

    // Ruido gaussiano Box-Muller
    static randomGaussian(mean = 0, stdev = 1) {
        const u = 1 - Math.random();
        const v = Math.random();
        const z = Math.sqrt(-2.0 * Math.log(u)) * Math.cos(2.0 * Math.PI * v);
        return mean + z * stdev;
    }

    computeStep() {
        this.step_counter++;
        this.watchdog_plc = (this.watchdog_plc + 1) % 65535;

        // Dinámica lenta del afluente
        const t = this.step_counter * 0.05;
        const slowWave = Math.sin(t * 0.2) * 2.0;

        // 1. Agua Cruda
        const ph_cruda = Math.max(4.0, Math.min(10.0, this.ph_cruda_base + PlantState.randomGaussian(0, 0.03)));
        const turb_cruda = Math.max(2.0, this.turbidez_cruda_base + slowWave + PlantState.randomGaussian(0, 0.4));
        const cond_cruda = Math.max(100.0, this.conductividad_cruda_base + PlantState.randomGaussian(0, 2.5));

        // 2. Hidráulica
        let caudal_b1 = 0.0;
        let presion_b1 = 0.0;
        let caudal_dist = 0.0;

        if (this.bomba_principal_estado) {
            caudal_b1 = Math.max(0.0, this.caudal_b1_base + PlantState.randomGaussian(0, 0.15));
            presion_b1 = Math.max(0.0, this.presion_b1_base + PlantState.randomGaussian(0, 0.04));
            caudal_dist = Math.max(0.0, caudal_b1 * 0.97 + PlantState.randomGaussian(0, 0.1));
        }

        // 3. Calidad de Agua Tratada
        let turb_tratada = 0.5;
        let ph_tratada = ph_cruda;

        if (this.dosificador_sulfato_estado && this.bomba_principal_estado) {
            turb_tratada = Math.max(0.25, (turb_cruda * 0.025) + PlantState.randomGaussian(0, 0.03));
            ph_tratada = Math.max(6.0, ph_cruda - 0.20 + PlantState.randomGaussian(0, 0.02));
        } else {
            turb_tratada = Math.max(1.5, (turb_cruda * 0.45) + PlantState.randomGaussian(0, 0.1));
        }

        const cond_tratada = cond_cruda + (this.dosificador_cloro_estado ? 20.0 : 0.0) + PlantState.randomGaussian(0, 1.5);

        return {
            timestamp: new Date().toISOString(),
            plcConnected: true,
            source: "SIMULADOR_OFFLINE_HIDROCONTROL",
            values: {
                bomba_principal_estado: this.bomba_principal_estado,
                dosificador_sulfato_estado: this.dosificador_sulfato_estado,
                dosificador_cloro_estado: this.dosificador_cloro_estado,
                nivel_tanque_agua_cruda: this.nivel_tanque_agua_cruda,
                modo_remoto_habilitado: this.modo_remoto_habilitado,
                plc_en_falla: this.plc_en_falla,
                comando_rechazado: this.comando_rechazado,
                ultimo_comando_aceptado: this.ultimo_comando_aceptado,
                ph_agua_cruda: parseFloat(ph_cruda.toFixed(2)),
                turbidez_agua_cruda: parseFloat(turb_cruda.toFixed(1)),
                conductividad_agua_cruda: parseFloat(cond_cruda.toFixed(0)),
                ph_agua_tratada: parseFloat(ph_tratada.toFixed(2)),
                turbidez_agua_tratada: parseFloat(turb_tratada.toFixed(2)),
                conductividad_agua_tratada: parseFloat(cond_tratada.toFixed(0)),
                caudal_bomba_principal: parseFloat(caudal_b1.toFixed(1)),
                caudal_distribucion: parseFloat(caudal_dist.toFixed(1)),
                presion_bomba_principal: parseFloat(presion_b1.toFixed(2)),
                watchdog_plc: this.watchdog_plc
            }
        };
    }
}

const plant = new PlantState();

// =============================================================================
// SERVIDOR HTTP REST
// =============================================================================
function setCorsHeaders(res, status = 200, contentType = "application/json") {
    res.writeHead(status, {
        "Content-Type": contentType,
        "Access-Control-Allow-Origin": "*",
        "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
        "Access-Control-Allow-Headers": "Content-Type, Authorization"
    });
}

const server = http.createServer((req, res) => {
    const parsedUrl = url.parse(req.url, true);
    const pathname = parsedUrl.pathname;

    if (req.method === "OPTIONS") {
        setCorsHeaders(res, 204);
        res.end();
        return;
    }

    if (req.method === "GET") {
        if (pathname === "/api/telemetry/current") {
            const data = plant.computeStep();
            setCorsHeaders(res, 200);
            res.end(JSON.stringify(data));
        } else if (pathname === "/api/system/status") {
            const status = {
                service: "HIDROCONTROL PTAP - Simulador de Proceso Offline",
                timestamp: new Date().toISOString(),
                plc: {
                    ip: "127.0.0.1 (SIMULADO)",
                    connected: true,
                    lastSeen: new Date().toISOString(),
                    watchdog: plant.watchdog_plc,
                    scenario: plant.scenario
                }
            };
            setCorsHeaders(res, 200);
            res.end(JSON.stringify(status));
        } else if (pathname === "/api/simulator/state") {
            setCorsHeaders(res, 200);
            res.end(JSON.stringify(plant.getTunableState()));
        } else if (pathname.startsWith("/api/scenario/")) {
            const scenarioName = pathname.replace("/api/scenario/", "").trim();
            plant.setScenario(scenarioName);
            setCorsHeaders(res, 200);
            res.end(JSON.stringify({ status: "ok", active_scenario: scenarioName, state: plant.getTunableState() }));
        } else {
            setCorsHeaders(res, 404);
            res.end(JSON.stringify({ error: "Endpoint no encontrado en simulador" }));
        }
    } else if (req.method === "POST") {
        let body = "";
        req.on("data", chunk => { body += chunk; });
        req.on("end", () => {
            if (pathname === "/api/simulator/state") {
                try {
                    const payload = body ? JSON.parse(body) : {};
                    const updated = plant.setTunableState(payload);
                    setCorsHeaders(res, 200);
                    res.end(JSON.stringify({ success: true, state: updated }));
                } catch (err) {
                    setCorsHeaders(res, 400);
                    res.end(JSON.stringify({ error: "JSON inválido: " + err.message }));
                }
            } else if (pathname === "/api/control/command") {
                try {
                    const payload = body ? JSON.parse(body) : {};
                    const target = payload.target || "";
                    const command = payload.command || "";
                    const result = plant.executeCommand(target, command);

                    const resPayload = {
                        accepted: result.accepted,
                        sequence: plant.command_sequence,
                        target,
                        command,
                        message: result.message,
                        timestamp: new Date().toISOString()
                    };

                    setCorsHeaders(res, result.accepted ? 200 : 400);
                    res.end(JSON.stringify(resPayload));
                } catch (err) {
                    setCorsHeaders(res, 500);
                    res.end(JSON.stringify({ error: err.message }));
                }
            } else {
                setCorsHeaders(res, 404);
                res.end(JSON.stringify({ error: "Endpoint POST no encontrado" }));
            }
        });
    }
});

server.listen(PORT, "0.0.0.0", () => {
    console.log("===============================================================================");
    console.log("        HIDROCONTROL PTAP V2.2 - MOTOR DE SIMULACIÓN OFFLINE (NODE.JS)         ");
    console.log("===============================================================================");
    console.log(` [+] Servidor Mock HTTP activo en:     http://localhost:${PORT}`);
    console.log(` [+] Endpoint telemetría:              http://localhost:${PORT}/api/telemetry/current`);
    console.log(` [+] Endpoint estado sistema:          http://localhost:${PORT}/api/system/status`);
    console.log(` [+] Escenario actual:                 ${plant.scenario.toUpperCase()}`);
    console.log("-------------------------------------------------------------------------------");
    console.log(" Comandos rápidos para probar escenarios en caliente:");
    console.log(`  * Lluvia Torrencial (alta turbidez): curl http://localhost:${PORT}/api/scenario/rain`);
    console.log(`  * Falla de Bomba B1:                 curl http://localhost:${PORT}/api/scenario/pump_fault`);
    console.log(`  * Falla Dosificación Cloro:          curl http://localhost:${PORT}/api/scenario/chlorine_fault`);
    console.log(`  * Operación Normal:                  curl http://localhost:${PORT}/api/scenario/normal`);
    console.log("===============================================================================");
    console.log(" Presiona Ctrl+C para detener el simulador.\n");
});
