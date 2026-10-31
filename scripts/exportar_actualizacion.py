import os
import shutil

src_root = r"d:\PROGRAMAS\CONTROL\HIDROCONTROL PTAP V2.2"
dst_root = r"d:\PROGRAMAS\CONTROL\ACTUALIZACION_SERVIDOR_OFICINA"

print("==============================================================================")
print("   CREANDO PAQUETE DE ACTUALIZACION PARA EL SERVIDOR DE LA OFICINA")
print("==============================================================================")
print(f"Origen : {src_root}")
print(f"Destino: {dst_root}\n")

if os.path.exists(dst_root):
    shutil.rmtree(dst_root)

# Files to copy (relative to src_root)
files_to_copy = [
    # 1. Node-RED fixed flow (DB10 read / DB11 write separated)
    r"nodered\flows\hidrocontrol-ptap-flow.json",
    
    # 2. Edge Agent (with intelligent chemical dosing, deadband SQLite, and /panel web console)
    r"services\edge-agent\edge-agent.js",

    # 3. Consola Local de Respaldo Offline
    r"Consola-Local-Servidor.html",
    r"Abrir-Consola-Local-Servidor.bat",

    # 4. WhatsApp Gateway service
    r"services\whatsapp-gateway\server.js",
    r"services\whatsapp-gateway\package.json",
    
    # 5. Screen bridge (auto COM detection, 100-character strict formatting)
    r"scripts\Enlace-Pantalla-SEEKU-FX.ps1",
    r"scripts\Monitor-Botones-Pantalla.ps1",
    r"Conectar-Pantalla-SEEKU.bat",
    r"Probar-Botones-Pantalla-En-Vivo.bat",
    
    # 6. Master startup and network scripts
    r"AUTOSTART-PLANTA.bat",
    r"Configurar-IP-Ethernet-PLC.bat",
    r"Iniciar-TODO-Con-Internet.bat",
    r"Iniciar-TODO.bat",
    r"Arrancar-Servidor-Planta.bat",
    r"Arrancar-Servidor-Planta-Tailscale.bat",
    
    # 7. Databases
    r"database\ptap-edge-history.db",
    r"database\policy-rules.json",
    r"dashboard\PTAPControl\hidrocontrol-auth.db",
    
    # 8. Dashboard SCADA .NET 8 (Source files & compiled DLL)
    r"dashboard\PTAPControl\Components\Pages\Home.razor",
    r"dashboard\PTAPControl\Components\Pages\Simulador.razor",
    r"dashboard\PTAPControl\Components\Pages\AnalisisIA.razor",
    r"dashboard\PTAPControl\Services\PtapSimulationEngine.cs",
    r"dashboard\PTAPControl\Services\NodeRedApiClient.cs",
    r"dashboard\PTAPControl\Services\PtapKnowledgeService.cs",
    r"dashboard\PTAPControl\bin\Debug\net8.0\PTAPControl.dll",

    # 9. Documentation & Guides
    r"INSTRUCCIONES_OFICINA_LLEGAR_Y_EJECUTAR.md",
    r"docs\SOLUCION_DEFINITIVA_CONEXION_PANTALLA.md",
    r"docs\ESTADO_ACTUAL_PLANTA_Y_PASOS_MANANA.md",
    r"docs\PLAN_PRODUCCION_VIDEOS_OFICINA.md"
]

copied_count = 0
for rel_path in files_to_copy:
    src_file = os.path.join(src_root, rel_path)
    dst_file = os.path.join(dst_root, rel_path)
    
    if os.path.exists(src_file):
        os.makedirs(os.path.dirname(dst_file), exist_ok=True)
        shutil.copy2(src_file, dst_file)
        print(f" [OK] {rel_path} ({os.path.getsize(src_file)} bytes)")
        copied_count += 1
    else:
        print(f" [ALERTA] Archivo no encontrado: {rel_path}")

print(f"\n[+] Total archivos empaquetados: {copied_count}")
print(f"[+] Paquete listo en: {dst_root}")
print("==============================================================================")
