#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
===============================================================================
HIDROCONTROL PTAP V2.2 - MOTOR DE SIMULACIÓN ESTOCÁSTICA Y FÍSICA (OFFLINE)
===============================================================================
Este script emula la respuesta dinámica hidrodinámica y química de la planta
potabilizadora cuando el PLC Siemens S7-1200 físico (192.168.1.110) no está conectado.

Capacidades:
1. Simulación física coherente:
   - Reducción de turbidez en sedimentador lamelar y filtro de carbón según coagulante.
   - Variación de pH por dosificación ácida de Sulfato de Aluminio.
   - Generación de presión y caudal dependiente del estado ON/OFF de la Bomba B1.
   - Fluctuaciones estocásticas gaussianas para emular ruido real de instrumentación 4-20mA.
2. Servidor Mock HTTP integrado:
   - Expone endpoints compatibles con el Dashboard SCADA Blazor:
     * GET /api/telemetry/current
     * GET /api/system/status
     * POST /api/control/command
3. Modo Push opcional hacia Node-RED (puerto 1880).
4. Inyección de escenarios críticos para pruebas y demostraciones:
   - normal: Operación estable dentro de norma.
   - rain: Lluvia torrencial con aumento drástico de turbidez cruda (> 75 NTU).
   - pump_fault: Parada o sobrepresión en Bomba Principal B1.
   - chlorine_fault: Agotamiento o falla en dosificador de desinfectante B3.

Sin dependencias externas requeridas (utiliza únicamente la librería estándar de Python).
"""

import argparse
import http.server
import json
import math
import random
import socketserver
import sys
import threading
import time
from datetime import datetime, timezone
import urllib.request
import urllib.error

# =============================================================================
# ESTADO GLOBAL DE LA PLANTA SIMULADA
# =============================================================================
class PlantState:
    def __init__(self):
        self.lock = threading.Lock()
        
        # Estados booleanos de actuadores
        self.bomba_principal_estado = True
        self.dosificador_sulfato_estado = True
        self.dosificador_cloro_estado = True
        self.nivel_tanque_agua_cruda = True
        self.modo_remoto_habilitado = True
        self.plc_en_falla = False
        self.comando_rechazado = False
        self.ultimo_comando_aceptado = True
        
        # Contadores y supervisión
        self.watchdog_plc = 1000
        self.command_sequence = 1
        self.last_command_result = "Simulador iniciado"
        
        # Variables continuas base (Condición de operación nominal)
        self.ph_cruda_base = 7.25
        self.turbidez_cruda_base = 18.5
        self.conductividad_cruda_base = 425.0
        
        self.ph_tratada_base = 7.05
        self.turbidez_tratada_base = 0.48
        self.conductividad_tratada_base = 445.0
        
        self.caudal_b1_base = 15.0
        self.caudal_dist_base = 14.6
        self.presion_b1_base = 2.45
        
        # Escenario activo
        self.scenario = "normal"
        self.step_counter = 0

    def set_scenario(self, scenario_name: str):
        with self.lock:
            self.scenario = scenario_name
            if scenario_name == "rain":
                self.turbidez_cruda_base = 78.0
                self.ph_cruda_base = 6.85
            elif scenario_name == "pump_fault":
                self.bomba_principal_estado = False
                self.plc_en_falla = True
            elif scenario_name == "chlorine_fault":
                self.dosificador_cloro_estado = False
            else: # normal
                self.turbidez_cruda_base = 18.5
                self.ph_cruda_base = 7.25
                self.bomba_principal_estado = True
                self.dosificador_sulfato_estado = True
                self.dosificador_cloro_estado = True
                self.plc_en_falla = False

    def execute_command(self, target: str, command: str, operator: str = "operador_sim"):
        with self.lock:
            self.command_sequence += 1
            cmd_key = f"{target}_{command}"
            
            if not self.modo_remoto_habilitado:
                self.comando_rechazado = True
                self.ultimo_comando_aceptado = False
                return False, "Comando rechazado: Modo remoto no habilitado"

            if target == "bomba_principal":
                self.bomba_principal_estado = (command == "start")
            elif target == "dosificador_sulfato":
                self.dosificador_sulfato_estado = (command == "start")
            elif target == "dosificador_cloro":
                self.dosificador_cloro_estado = (command == "start")
            elif target in ("ALL", "planta"):
                self.bomba_principal_estado = False
                self.dosificador_sulfato_estado = False
                self.dosificador_cloro_estado = False
            else:
                self.comando_rechazado = True
                return False, f"Equipo desconocido: {target}"

            self.comando_rechazado = False
            self.ultimo_comando_aceptado = True
            self.last_command_result = f"Ejecutado {command} en {target}"
            return True, f"Comando {cmd_key} procesado exitosamente por simulador"

    def compute_step(self):
        with self.lock:
            self.step_counter += 1
            self.watchdog_plc = (self.watchdog_plc + 1) % 65535
            
            # Dinámica oscilatoria lenta del afluente (río / pozo)
            t = self.step_counter * 0.05
            slow_wave = math.sin(t * 0.2) * 2.0
            
            # 1. Agua Cruda (con ruido gaussiano estocástico)
            ph_cruda = max(4.0, min(10.0, self.ph_cruda_base + random.gauss(0, 0.03)))
            turb_cruda = max(2.0, self.turbidez_cruda_base + slow_wave + random.gauss(0, 0.4))
            cond_cruda = max(100.0, self.conductividad_cruda_base + random.gauss(0, 2.5))
            
            # 2. Hidráulica de Bombeo
            if self.bomba_principal_estado:
                caudal_b1 = max(0.0, self.caudal_b1_base + random.gauss(0, 0.15))
                presion_b1 = max(0.0, self.presion_b1_base + random.gauss(0, 0.04))
                caudal_dist = max(0.0, caudal_b1 * 0.97 + random.gauss(0, 0.1))
            else:
                caudal_b1 = 0.0
                presion_b1 = 0.0
                caudal_dist = 0.0

            # 3. Respuesta química y tratamiento
            # Coagulación con Sulfato de Aluminio:
            if self.dosificador_sulfato_estado and self.bomba_principal_estado:
                # La remoción típica de turbidez alcanza 96% - 98% con buena dosificación
                turb_tratada = max(0.25, (turb_cruda * 0.025) + random.gauss(0, 0.03))
                # Ligera reducción de pH por hidrólisis del aluminio
                ph_tratada = max(6.0, ph_cruda - 0.20 + random.gauss(0, 0.02))
            else:
                # Sin coagulante la sedimentación y filtración pierden hasta 70% de eficiencia
                turb_tratada = max(1.5, (turb_cruda * 0.45) + random.gauss(0, 0.1))
                ph_tratada = ph_cruda

            # Desinfección con Hipoclorito de Sodio:
            cond_tratada = cond_cruda + (20.0 if self.dosificador_cloro_estado else 0.0) + random.gauss(0, 1.5)

            # Empaque de valores conforme a la especificación de DB10
            return {
                "timestamp": datetime.now(timezone.utc).isoformat(),
                "plcConnected": True,
                "source": "SIMULADOR_OFFLINE_HIDROCONTROL",
                "values": {
                    "bomba_principal_estado": self.bomba_principal_estado,
                    "dosificador_sulfato_estado": self.dosificador_sulfato_estado,
                    "dosificador_cloro_estado": self.dosificador_cloro_estado,
                    "nivel_tanque_agua_cruda": self.nivel_tanque_agua_cruda,
                    "modo_remoto_habilitado": self.modo_remoto_habilitado,
                    "plc_en_falla": self.plc_en_falla,
                    "comando_rechazado": self.comando_rechazado,
                    "ultimo_comando_aceptado": self.ultimo_comando_aceptado,
                    "ph_agua_cruda": round(ph_cruda, 2),
                    "turbidez_agua_cruda": round(turb_cruda, 1),
                    "conductividad_agua_cruda": round(cond_cruda, 0),
                    "ph_agua_tratada": round(ph_tratada, 2),
                    "turbidez_agua_tratada": round(turb_tratada, 2),
                    "conductividad_agua_tratada": round(cond_tratada, 0),
                    "caudal_bomba_principal": round(caudal_b1, 1),
                    "caudal_distribucion": round(caudal_dist, 1),
                    "presion_bomba_principal": round(presion_b1, 2),
                    "watchdog_plc": self.watchdog_plc
                }
            }

# =============================================================================
# MANEJADOR HTTP (MOCK REST API PARA EL SCADA)
# =============================================================================
class SimulatorHttpHandler(http.server.BaseHTTPRequestHandler):
    plant: PlantState = None # Inyectado en arranque

    def _set_cors_headers(self, status=200, content_type="application/json"):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.end_headers()

    def do_OPTIONS(self):
        self._set_cors_headers(204)

    def do_GET(self):
        if self.path.startswith("/api/telemetry/current"):
            data = self.plant.compute_step()
            self._set_cors_headers(200)
            self.wfile.write(json.dumps(data).encode("utf-8"))
            
        elif self.path.startswith("/api/system/status"):
            status = {
                "service": "HIDROCONTROL PTAP - Simulador de Proceso Offline",
                "timestamp": datetime.now(timezone.utc).isoformat(),
                "plc": {
                    "ip": "127.0.0.1 (SIMULADO)",
                    "connected": True,
                    "lastSeen": datetime.now(timezone.utc).isoformat(),
                    "watchdog": self.plant.watchdog_plc,
                    "scenario": self.plant.scenario
                }
            }
            self._set_cors_headers(200)
            self.wfile.write(json.dumps(status).encode("utf-8"))
            
        elif self.path.startswith("/api/scenario/"):
            # Permite cambiar escenario vía URL rápida (ej: /api/scenario/rain)
            scenario_name = self.path.split("/api/scenario/")[-1].strip()
            self.plant.set_scenario(scenario_name)
            self._set_cors_headers(200)
            self.wfile.write(json.dumps({"status": "ok", "active_scenario": scenario_name}).encode("utf-8"))
            
        else:
            self._set_cors_headers(404)
            self.wfile.write(b'{"error": "Endpoint no encontrado en simulador"}')

    def do_POST(self):
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length).decode("utf-8")
        
        if self.path.startswith("/api/control/command"):
            try:
                payload = json.loads(body) if body else {}
                target = payload.get("target", "")
                command = payload.get("command", "")
                operator = payload.get("operator", "operador_sim")
                
                success, msg = self.plant.execute_command(target, command, operator)
                status_code = 200 if success else 400
                response_data = {
                    "accepted": success,
                    "sequence": self.plant.command_sequence,
                    "target": target,
                    "command": command,
                    "message": msg,
                    "timestamp": datetime.now(timezone.utc).isoformat()
                }
                self._set_cors_headers(status_code)
                self.wfile.write(json.dumps(response_data).encode("utf-8"))
            except Exception as ex:
                self._set_cors_headers(500)
                self.wfile.write(json.dumps({"error": str(ex)}).encode("utf-8"))
        else:
            self._set_cors_headers(404)
            self.wfile.write(b'{"error": "Endpoint POST no encontrado"}')

    def log_message(self, format, *args):
        # Silenciar logs verbosos de polling
        pass

# =============================================================================
# LOOP DE SIMULACIÓN Y PUSH OPCIONAL
# =============================================================================
def background_push_loop(plant: PlantState, nodered_url: str, interval: float, stop_event: threading.Event):
    push_endpoint = f"{nodered_url.rstrip('/')}/api/telemetry/current"
    print(f"[*] Modo Push hacia Node-RED activo en: {push_endpoint} (cada {interval}s)")
    
    while not stop_event.is_set():
        data = plant.compute_step()
        try:
            req = urllib.request.Request(
                push_endpoint,
                data=json.dumps(data).encode("utf-8"),
                headers={"Content-Type": "application/json"},
                method="POST"
            )
            with urllib.request.urlopen(req, timeout=2.0) as resp:
                pass
        except Exception:
            # Si Node-RED no tiene endpoint de recepción POST, el servidor HTTP local se mantiene como fallback
            pass
        time.sleep(interval)

# =============================================================================
# ENTRADA PRINCIPAL
# =============================================================================
def main():
    parser = argparse.ArgumentParser(description="Simulador de variables y proceso físico para HIDROCONTROL PTAP V2.2")
    parser.add_argument("--port", type=int, default=1881, help="Puerto HTTP del servidor Mock (default: 1881)")
    parser.add_argument("--scenario", type=str, default="normal", choices=["normal", "rain", "pump_fault", "chlorine_fault"], help="Escenario inicial")
    parser.add_argument("--push-nodered", action="store_true", help="Enviar telemetría simulada a Node-RED periódicamente")
    parser.add_argument("--nodered-url", type=str, default="http://127.0.0.1:1880", help="URL base de Node-RED")
    parser.add_argument("--interval", type=float, default=2.0, help="Intervalo de actualización en segundos (default: 2.0)")

    args = parser.parse_args()

    plant = PlantState()
    plant.set_scenario(args.scenario)

    SimulatorHttpHandler.plant = plant

    stop_event = threading.Event()
    push_thread = None

    if args.push_nodered:
        push_thread = threading.Thread(
            target=background_push_loop,
            args=(plant, args.nodered_url, args.interval, stop_event),
            daemon=True
        )
        push_thread.start()

    print("===============================================================================")
    print("        HIDROCONTROL PTAP V2.2 - MOTOR DE SIMULACIÓN OFFLINE ACTIVO            ")
    print("===============================================================================")
    print(f" [+] Servidor Mock HTTP activo en:     http://localhost:{args.port}")
    print(f" [+] Endpoint telemetría:              http://localhost:{args.port}/api/telemetry/current")
    print(f" [+] Endpoint estado sistema:          http://localhost:{args.port}/api/system/status")
    print(f" [+] Escenario actual:                 {args.scenario.upper()}")
    print("-------------------------------------------------------------------------------")
    print(" Comandos rápidos para probar escenarios:")
    print(f"  * Lluvia Torrencial (alta turbidez): curl http://localhost:{args.port}/api/scenario/rain")
    print(f"  * Falla de Bomba B1:                 curl http://localhost:{args.port}/api/scenario/pump_fault")
    print(f"  * Normalizar Planta:                 curl http://localhost:{args.port}/api/scenario/normal")
    print("===============================================================================")
    print(" Presiona Ctrl+C para detener el simulador.\n")

    # Iniciar servidor HTTP
    class ThreadedHTTPServer(socketserver.ThreadingMixIn, http.server.HTTPServer):
        daemon_threads = True

    server = ThreadedHTTPServer(("0.0.0.0", args.port), SimulatorHttpHandler)

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n[*] Deteniendo simulador...")
        stop_event.set()
        server.server_close()
        sys.exit(0)

if __name__ == "__main__":
    main()
