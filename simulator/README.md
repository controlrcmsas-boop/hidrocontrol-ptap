# Motor de Simulación de Procesos PTAP (Modo Offline)

Este módulo provee simulación hidrodinámica y fisicoquímica para pruebas, demostraciones y desarrollo offline del sistema **HIDROCONTROL PTAP V2.2** sin requerir conexión física al PLC Siemens S7-1200 (`192.168.1.110`).

---

## Archivo Principal
- **Script:** [ptap_simulator.py](file:///d:/PROGRAMAS/CONTROL/HIDROCONTROL%20PTAP%20V2.2/simulator/ptap_simulator.py)
- **Requisitos:** Python 3.8+ (Utiliza únicamente la biblioteca estándar, **sin pip install** necesario).

---

## Modos de Uso

### 1. Servidor Mock Independiente (Para Dashboard SCADA)
El simulador expone una API REST idéntica a la de Node-RED en el puerto `1881`:

```bash
python ptap_simulator.py --port 1881
```

Para que el Dashboard Blazor consuma directamente al simulador, en [appsettings.json](file:///d:/PROGRAMAS/CONTROL/HIDROCONTROL%20PTAP%20V2.2/dashboard/PTAPControl/appsettings.json) o mediante variable de entorno:
```json
"NodeRed": {
  "BaseUrl": "http://localhost:1881/"
}
```
O ejecutando el dashboard con:
```bash
set NODERED_BASE_URL=http://localhost:1881/
dotnet run
```

---

### 2. Inyección de Escenarios Críticos en Tiempo Real

Puedes conmutar la física de la planta en caliente vía HTTP sin reiniciar el simulador:

| Escenario | Comando de Activación | Comportamiento Simulado |
| :--- | :--- | :--- |
| **Normal** | `curl http://localhost:1881/api/scenario/normal` | Operación estable, turbidez cruda 18 NTU, tratada 0.48 NTU, pH 7.1, bombas ON. |
| **Lluvia Torrencial** | `curl http://localhost:1881/api/scenario/rain` | Turbidez cruda se dispara a ~78 NTU, el supervisor IA alerta y sugiere elevar dosis de coagulante. |
| **Falla de Bomba B1** | `curl http://localhost:1881/api/scenario/pump_fault` | Bomba B1 se apaga, presión y caudal caen a 0, activa bit de falla en el PLC. |
| **Falla Cloración** | `curl http://localhost:1881/api/scenario/chlorine_fault` | Dosificador B3 se apaga, conductividad decae, alarma de desinfección. |

---

### 3. Modo Push hacia Node-RED (Opcional)
Si deseas alimentar la base de datos PostgreSQL a través de Node-RED usando telemetría simulada:
```bash
python ptap_simulator.py --push-nodered --nodered-url http://localhost:1880 --interval 2.0
```

---

## Variables Emuladas en Bloque DB10
- `bomba_principal_estado`, `dosificador_sulfato_estado`, `dosificador_cloro_estado`
- `modo_remoto_habilitado`, `plc_en_falla`, `nivel_tanque_agua_cruda`
- `ph_agua_cruda`, `turbidez_agua_cruda`, `conductividad_agua_cruda`
- `ph_agua_tratada`, `turbidez_agua_tratada`, `conductividad_agua_tratada`
- `caudal_bomba_principal`, `caudal_distribucion`, `presion_bomba_principal`
- `watchdog_plc` (contador incremental de latido)
