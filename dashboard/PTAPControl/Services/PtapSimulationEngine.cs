using PTAPControl.Models;

namespace PTAPControl.Services;

public enum PtapDataSourceMode
{
    SimulatedAuto,       // Piloto automático: física estocástica en la nube
    SimulatedManual,     // Showroom comercial: pruebas y escenarios manuales
    PhysicalPlc          // Planta real: comunicación con Siemens S7-1200
}

public sealed class PtapSimulationEngine
{
    private readonly object _sync = new();

    // Estado físico y operativo
    public bool BombaPrincipalEstado { get; set; } = true;
    public bool DosificadorSulfatoEstado { get; set; } = true;
    public bool DosificadorCloroEstado { get; set; } = true;
    public bool NivelTanqueAguaCruda { get; set; } = true;
    public bool ModoRemotoHabilitado { get; set; } = true;
    public bool PlcEnFalla { get; set; } = false;
    public bool ComandoRechazado { get; set; } = false;
    public bool UltimoComandoAceptado { get; set; } = true;
    public bool ModoRecirculacion { get; set; } = false;

    public int WatchdogPlc { get; private set; } = 1000;
    public int CommandSequence { get; private set; } = 1;
    public string LastCommandResult { get; private set; } = "Simulador nativo listo";

    // Parámetros base
    public double PhCrudaBase { get; set; } = 7.25;
    public double TurbidezCrudaBase { get; set; } = 18.5;
    public double ConductividadCrudaBase { get; set; } = 425.0;
    public double PresionB1Base { get; set; } = 2.45;
    public double CaudalB1Base { get; set; } = 15.0;

    // Modo de operación y escenario (por defecto Simulador para que la interfaz responda instantáneamente)
    public PtapDataSourceMode CurrentMode { get; set; } = PtapDataSourceMode.PhysicalPlc;
    public string ActiveScenario { get; private set; } = "normal";
    public bool SimulatePlcDisconnected { get; set; } = false;

    private long _stepCounter = 0;
    private readonly Random _random = new();

    // Buffer circular en memoria para series históricas sincronizadas con el simulador y PLC
    private readonly Dictionary<string, List<QualityHistoryPoint>> _historyStore = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ph_agua_cruda"] = new(),
        ["turbidez_agua_cruda"] = new(),
        ["conductividad_agua_cruda"] = new(),
        ["ph_agua_tratada"] = new(),
        ["turbidez_agua_tratada"] = new(),
        ["conductividad_agua_tratada"] = new(),
        ["caudal_bomba_principal"] = new(),
        ["presion_bomba_principal"] = new(),
        ["caudal_distribucion"] = new()
    };
    private const int MaxHistoryPointsPerTag = 3600;
    private readonly Timer? _backgroundTimer;

    
    private static string ResolveStoragePath(string filename)
    {
        var dataDir = Directory.Exists("/data") ? "/data" : AppContext.BaseDirectory;
        return Path.Combine(dataDir, filename);
    }

    private static readonly string AlarmFilePath = ResolveStoragePath("ptap-alarm-history.json");
    private static readonly string QualityHistoryFilePath = ResolveStoragePath("ptap-quality-history.json");
    private readonly List<AlarmEvent> _activeAlarms = [];
    private readonly List<AlarmEvent> _alarmHistory = [];
    private long _nextAlarmId = 1;

    public PtapSimulationEngine()
    {
        LoadAlarmsFromDisk();
        LoadHistoryFromDisk();

        if (_alarmHistory.Count == 0)
        {
            _alarmHistory.Add(new AlarmEvent
            {
                Id = _nextAlarmId++,
                TagId = "sistema_scada",
                NombreVisible = "Sistema SCADA",
                AlarmType = "SYSTEM_BOOT",
                Severity = "info",
                Message = "Supervisión continua activa en Cloud y Planta",
                State = "RESUELTA",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                ClearedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            });
            SaveAlarmsToDisk();
        }

        // Muestreador continuo en segundo plano cada 2 segundos
        _backgroundTimer = new Timer(OnBackgroundTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private void LoadHistoryFromDisk()
    {
        try
        {
            if (File.Exists(QualityHistoryFilePath))
            {
                var json = File.ReadAllText(QualityHistoryFilePath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<QualityHistoryPoint>>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    lock (_sync)
                    {
                        foreach (var kvp in loaded)
                        {
                            _historyStore[kvp.Key] = kvp.Value;
                        }
                    }
                    return;
                }
            }
        }
        catch { }

        // Si no hay archivo previo guardado, inicializar curva base
        InitializeFullHistory();
    }

    private DateTimeOffset _lastHistoryDiskSave = DateTimeOffset.MinValue;
    private void SaveHistoryToDisk(bool force = false)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (!force && (now - _lastHistoryDiskSave).TotalSeconds < 15) return;
            _lastHistoryDiskSave = now;

            Dictionary<string, List<QualityHistoryPoint>> snapshot;
            lock (_sync)
            {
                snapshot = _historyStore.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.TakeLast(360).ToList()
                );
            }
            var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
            File.WriteAllText(QualityHistoryFilePath, json);
        }
        catch { }
    }

    private void LoadAlarmsFromDisk()
    {
        try
        {
            if (File.Exists(AlarmFilePath))
            {
                var json = File.ReadAllText(AlarmFilePath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<List<AlarmEvent>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    _alarmHistory.Clear();
                    _alarmHistory.AddRange(loaded);
                    _activeAlarms.Clear();
                    _activeAlarms.AddRange(loaded.Where(a => a.State == "ACTIVA"));
                    _nextAlarmId = loaded.Max(a => a.Id) + 1;
                }
            }
        }
        catch { }
    }

    private void SaveAlarmsToDisk()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_alarmHistory, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AlarmFilePath, json);
        }
        catch { }
    }

    private void OnBackgroundTick(object? state)
    {
        if (CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            var telemetry = GenerateTelemetryStep();
            RecordTelemetrySample(telemetry);
        }
    }

    public void NotifyParametersChanged()
    {
        lock (_sync)
        {
            if (CurrentMode != PtapDataSourceMode.PhysicalPlc)
            {
                // Inyectar muestras inmediatas para reflejar de forma reactiva el cambio en las gráficas
                var telemetry = GenerateTelemetryStep();
                RecordTelemetrySample(telemetry);
            }
        }
    }

    public double GetCurrentEstimatedPhTratada()
    {
        if (DosificadorSulfatoEstado && BombaPrincipalEstado)
        {
            return Math.Max(6.0, PhCrudaBase - 0.20);
        }
        return PhCrudaBase;
    }

    public double GetCurrentEstimatedTurbTratada()
    {
        if (DosificadorSulfatoEstado && BombaPrincipalEstado)
        {
            return Math.Max(0.25, TurbidezCrudaBase * 0.025);
        }
        return Math.Max(1.5, TurbidezCrudaBase * 0.45);
    }

    public double GetCurrentEstimatedCondTratada()
    {
        return ConductividadCrudaBase + (DosificadorCloroEstado ? 20.0 : 0.0);
    }

    public double GetEstimatedTurbTratada(double turbCruda)
    {
        if (DosificadorSulfatoEstado && BombaPrincipalEstado)
        {
            return Math.Max(0.25, (turbCruda * 0.025) + NextGaussian(0, 0.03));
        }
        return Math.Max(1.5, (turbCruda * 0.45) + NextGaussian(0, 0.1));
    }

    public double GetEstimatedPhTratada(double phCruda)
    {
        if (DosificadorSulfatoEstado && BombaPrincipalEstado)
        {
            return Math.Max(6.0, phCruda - 0.20 + NextGaussian(0, 0.02));
        }
        return phCruda;
    }

    public void SetMode(PtapDataSourceMode mode)
    {
        lock (_sync)
        {
            CurrentMode = mode;
        }
    }

    public void SetScenario(string scenarioName)
    {
        lock (_sync)
        {
            ActiveScenario = scenarioName.ToLowerInvariant();
            switch (ActiveScenario)
            {
                case "tierra":
                case "turbia":
                case "low_confidence":
                case "rain":
                    ActiveScenario = "tierra";
                    TurbidezCrudaBase = 85.0;
                    PhCrudaBase = 6.85;
                    ConductividadCrudaBase = 450.0;
                    PresionB1Base = 3.10;
                    BombaPrincipalEstado = true;
                    DosificadorSulfatoEstado = true;
                    DosificadorCloroEstado = false;
                    PlcEnFalla = false;
                    break;
                case "limon":
                case "acida":
                    ActiveScenario = "limon";
                    TurbidezCrudaBase = 1.2;
                    PhCrudaBase = 3.50;
                    ConductividadCrudaBase = 380.0;
                    PresionB1Base = 2.45;
                    BombaPrincipalEstado = true;
                    DosificadorSulfatoEstado = false;
                    DosificadorCloroEstado = false;
                    PlcEnFalla = false;
                    break;
                case "sal":
                case "salina":
                    ActiveScenario = "sal";
                    TurbidezCrudaBase = 1.0;
                    PhCrudaBase = 7.10;
                    ConductividadCrudaBase = 1850.0;
                    PresionB1Base = 2.45;
                    BombaPrincipalEstado = true;
                    DosificadorSulfatoEstado = false;
                    DosificadorCloroEstado = false;
                    PlcEnFalla = false;
                    break;
                case "pump_fault":
                    BombaPrincipalEstado = false;
                    DosificadorSulfatoEstado = false;
                    DosificadorCloroEstado = false;
                    PlcEnFalla = true;
                    break;
                case "chlorine_fault":
                    DosificadorCloroEstado = false;
                    break;
                case "tank_empty":
                    NivelTanqueAguaCruda = false;
                    BombaPrincipalEstado = false;
                    DosificadorSulfatoEstado = false;
                    DosificadorCloroEstado = false;
                    break;
                default: // normal / agua limpia
                    ActiveScenario = "normal";
                    TurbidezCrudaBase = 0.8;
                    PhCrudaBase = 7.20;
                    ConductividadCrudaBase = 95.0;
                    PresionB1Base = 2.45;
                    BombaPrincipalEstado = true;
                    DosificadorSulfatoEstado = false;
                    DosificadorCloroEstado = true;
                    NivelTanqueAguaCruda = true;
                    PlcEnFalla = false;
                    break;
            }
        }
    }

    public CommandResponse ExecuteCommand(string target, string command, string @operator = "operador_demo")
    {
        lock (_sync)
        {
            CommandSequence++;

            if (!ModoRemotoHabilitado)
            {
                ComandoRechazado = true;
                UltimoComandoAceptado = false;
                return new CommandResponse
                {
                    Accepted = false,
                    Error = "Comando rechazado: Modo remoto deshabilitado en tablero",
                    Target = target,
                    Command = command
                };
            }

            if (SimulatePlcDisconnected)
            {
                ComandoRechazado = true;
                UltimoComandoAceptado = false;
                return new CommandResponse
                {
                    Accepted = false,
                    Error = "COMUNICACIÓN FALLIDA: Enlace con autómata interrumpido (Simulación desconectada)",
                    Target = target,
                    Command = command
                };
            }

            bool isStart = command.Equals("start", StringComparison.OrdinalIgnoreCase);

            if (target.Equals("bomba_principal", StringComparison.OrdinalIgnoreCase))
            {
                BombaPrincipalEstado = isStart;
            }
            else if (target.Equals("dosificador_sulfato", StringComparison.OrdinalIgnoreCase))
            {
                DosificadorSulfatoEstado = isStart;
                if (isStart) ModoRecirculacion = false;
            }
            else if (target.Equals("dosificador_cloro", StringComparison.OrdinalIgnoreCase))
            {
                DosificadorCloroEstado = isStart;
                if (isStart) ModoRecirculacion = false;
            }
            else if (target.Equals("sistema", StringComparison.OrdinalIgnoreCase) && command.Equals("reset_alarmas", StringComparison.OrdinalIgnoreCase))
            {
                PlcEnFalla = false;
                ComandoRechazado = false;
            }
            else if (target.Equals("recirculacion", StringComparison.OrdinalIgnoreCase) || target.Equals("circuito_cerrado", StringComparison.OrdinalIgnoreCase))
            {
                ModoRecirculacion = isStart;
                if (isStart)
                {
                    BombaPrincipalEstado = true;
                    DosificadorSulfatoEstado = false;
                    DosificadorCloroEstado = false;
                }
            }
            else if (target.Equals("ALL", StringComparison.OrdinalIgnoreCase) || target.Equals("planta", StringComparison.OrdinalIgnoreCase))
            {
                ModoRecirculacion = false;
                BombaPrincipalEstado = false;
                DosificadorSulfatoEstado = false;
                DosificadorCloroEstado = false;
            }
            else
            {
                ComandoRechazado = true;
                return new CommandResponse
                {
                    Accepted = false,
                    Error = $"Equipo desconocido: {target}",
                    Target = target,
                    Command = command
                };
            }

            ComandoRechazado = false;
            UltimoComandoAceptado = true;
            LastCommandResult = $"Orden ejecutada con éxito: {command} en {target} por {@operator}";

            return new CommandResponse
            {
                Accepted = true,
                Message = LastCommandResult,
                Target = target,
                Command = command,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public TelemetryResponse GenerateTelemetryStep()
    {
        lock (_sync)
        {
            if (SimulatePlcDisconnected)
            {
                return new TelemetryResponse
                {
                    Timestamp = DateTime.UtcNow,
                    PlcConnected = false,
                    Source = "SIMULADOR_DESCONECTADO",
                    Values = new Dictionary<string, object?>()
                };
            }

            _stepCounter++;
            WatchdogPlc = (WatchdogPlc + 1) % 65535;

            // Onda sinusoidal suave de afluente natural
            double t = _stepCounter * 0.05;
            double slowWave = Math.Sin(t * 0.2) * 2.0;

            // 1. Agua Cruda con ruido gaussiano
            double phCruda = Math.Clamp(PhCrudaBase + NextGaussian(0, 0.03), 4.0, 10.0);
            double turbCruda = Math.Max(2.0, TurbidezCrudaBase + slowWave + NextGaussian(0, 0.4));
            double condCruda = Math.Max(100.0, ConductividadCrudaBase + NextGaussian(0, 2.5));

            // Coordinación química inteligente en modo automático simulado
            if (CurrentMode == PtapDataSourceMode.SimulatedAuto)
            {
                if (!BombaPrincipalEstado || ModoRecirculacion)
                {
                    DosificadorSulfatoEstado = false;
                    DosificadorCloroEstado = false;
                }
                else
                {
                    if (turbCruda > 5.0)
                    {
                        // Turbidez alta: Activar sulfato para coagulación, pausar cloro
                        DosificadorSulfatoEstado = true;
                        DosificadorCloroEstado = false;
                    }
                    else
                    {
                        // Turbidez óptima: Sulfato apagado, cloro activo para desinfección continua
                        DosificadorSulfatoEstado = false;
                        DosificadorCloroEstado = true;
                    }
                }
            }

            // 2. Hidráulica de Bombeo
            double caudalB1 = 0.0;
            double presionB1 = 0.0;
            double caudalDist = 0.0;

            if (BombaPrincipalEstado)
            {
                caudalB1 = Math.Max(0.0, CaudalB1Base + NextGaussian(0, 0.15));
                presionB1 = Math.Max(0.0, PresionB1Base + NextGaussian(0, 0.04));
                caudalDist = Math.Max(0.0, caudalB1 * 0.97 + NextGaussian(0, 0.1));
            }

            // 3. Calidad de Agua Tratada
            double turbTratada;
            double phTratada = phCruda;

            if (DosificadorSulfatoEstado && BombaPrincipalEstado)
            {
                // Coagulación efectiva
                turbTratada = Math.Max(0.25, (turbCruda * 0.025) + NextGaussian(0, 0.03));
                phTratada = Math.Max(6.0, phCruda - 0.20 + NextGaussian(0, 0.02));
            }
            else
            {
                // Sin coagulante o bomba apagada -> Turbidez alta en salida
                turbTratada = Math.Max(1.5, (turbCruda * 0.45) + NextGaussian(0, 0.1));
            }

            double condTratada = condCruda + (DosificadorCloroEstado ? 20.0 : 0.0) + NextGaussian(0, 1.5);

            var dict = new Dictionary<string, object?>
            {
                ["bomba_principal_estado"] = BombaPrincipalEstado,
                ["dosificador_sulfato_estado"] = DosificadorSulfatoEstado,
                ["dosificador_cloro_estado"] = DosificadorCloroEstado,
                ["modo_recirculacion"] = ModoRecirculacion,
                ["nivel_tanque_agua_cruda"] = NivelTanqueAguaCruda,
                ["modo_remoto_habilitado"] = ModoRemotoHabilitado,
                ["plc_en_falla"] = PlcEnFalla,
                ["comando_rechazado"] = ComandoRechazado,
                ["ultimo_comando_aceptado"] = UltimoComandoAceptado,
                ["ph_agua_cruda"] = Math.Round(phCruda, 2),
                ["turbidez_agua_cruda"] = Math.Round(turbCruda, 1),
                ["conductividad_agua_cruda"] = Math.Round(condCruda, 0),
                ["ph_agua_tratada"] = Math.Round(phTratada, 2),
                ["turbidez_agua_tratada"] = Math.Round(turbTratada, 2),
                ["conductividad_agua_tratada"] = Math.Round(condTratada, 0),
                ["caudal_bomba_principal"] = Math.Round(caudalB1, 1),
                ["caudal_distribucion"] = Math.Round(caudalDist, 1),
                ["presion_bomba_principal"] = Math.Round(presionB1, 2),
                ["watchdog_plc"] = WatchdogPlc
            };

            return new TelemetryResponse
            {
                Timestamp = DateTime.UtcNow,
                PlcConnected = true,
                Source = "SIMULADOR_NATIVO_HIDROCONTROL",
                Values = dict
            };
        }
    }

    public List<AlarmEvent> GetActiveAlarms()
    {
        lock (_sync)
        {
            return _activeAlarms.OrderByDescending(a => a.StartedAt).ToList();
        }
    }

    public List<AlarmEvent> GetAlarmHistory(int limit = 100)
    {
        lock (_sync)
        {
            return _alarmHistory.OrderByDescending(a => a.StartedAt).Take(limit).ToList();
        }
    }

    public void SetConnectionAlarm(bool isOnline, string details = "")
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            var existing = _activeAlarms.FirstOrDefault(a => a.TagId == "enlace_servidor");

            if (!isOnline)
            {
                if (existing is null)
                {
                    var alarm = new AlarmEvent
                    {
                        Id = _nextAlarmId++,
                        TagId = "enlace_servidor",
                        NombreVisible = "Enlace Servidor",
                        AlarmType = "COMM_FAULT",
                        Severity = "critical",
                        Message = string.IsNullOrWhiteSpace(details) ? "Pérdida de enlace con el servidor de planta (Offline)" : details,
                        State = "ACTIVA",
                        StartedAt = now
                    };
                    _activeAlarms.Add(alarm);
                    _alarmHistory.Insert(0, alarm);
                    SaveAlarmsToDisk();
                }
            }
            else
            {
                if (existing is not null)
                {
                    existing.State = "RESUELTA";
                    existing.ClearedAt = now;
                    _activeAlarms.Remove(existing);

                    _alarmHistory.Insert(0, new AlarmEvent
                    {
                        Id = _nextAlarmId++,
                        TagId = "enlace_servidor",
                        NombreVisible = "Enlace Servidor",
                        AlarmType = "COMM_RESTORED",
                        Severity = "info",
                        Message = "Conexión con el servidor de planta restablecida con éxito",
                        State = "RESUELTA",
                        StartedAt = now,
                        ClearedAt = now
                    });
                    SaveAlarmsToDisk();
                }
            }
        }
    }

    public void EvaluateTelemetryAlarms(TelemetryResponse telemetry)
    {
        if (telemetry?.Values == null) return;

        lock (_sync)
        {
            var now = telemetry.Timestamp != default ? telemetry.Timestamp : DateTimeOffset.UtcNow;
            bool changed = false;

            // 1. Turbidez (detecta Agua con tierra tanto en cruda como en tratada)
            double turbT = GetDictDouble(telemetry.Values, "turbidez_agua_tratada");
            double turbC = GetDictDouble(telemetry.Values, "turbidez_agua_cruda");
            bool isTurbAlarm = turbT > 1.80 || turbC > 5.0;
            double effectiveTurb = turbT > 1.80 ? turbT : turbC;

            var turbAlarm = _activeAlarms.FirstOrDefault(a => a.TagId == "turbidez");
            if (isTurbAlarm)
            {
                string msg = turbT > 1.80
                    ? $"Turbidez tratada fuera de norma: {turbT:0.00} NTU (Límite: 1.80 NTU)"
                    : $"Turbidez elevada en agua cruda: {turbC:0.0} NTU (Límite: 5.0 NTU)";

                if (turbAlarm is null)
                {
                    turbAlarm = new AlarmEvent
                    {
                        Id = _nextAlarmId++,
                        TagId = "turbidez",
                        NombreVisible = "Turbidez",
                        AlarmType = "HIGH_LIMIT",
                        Severity = effectiveTurb > 20.0 ? "critical" : "high",
                        ValueNumeric = (decimal)Math.Round(effectiveTurb, 2),
                        Message = msg,
                        State = "ACTIVA",
                        StartedAt = now
                    };
                    _activeAlarms.Add(turbAlarm);
                    _alarmHistory.Insert(0, turbAlarm);
                    changed = true;
                }
                else
                {
                    turbAlarm.ValueNumeric = (decimal)Math.Round(effectiveTurb, 2);
                    turbAlarm.Message = msg;
                }
            }
            else if (turbAlarm is not null && (turbT > 0 || turbC > 0))
            {
                turbAlarm.State = "RESUELTA";
                turbAlarm.ClearedAt = now;
                _activeAlarms.Remove(turbAlarm);
                changed = true;
            }

            // 2. pH (detecta Agua con limón tanto en cruda como en tratada)
            double phT = GetDictDouble(telemetry.Values, "ph_agua_tratada");
            double phC = GetDictDouble(telemetry.Values, "ph_agua_cruda");
            bool isPhAcid = (phT > 0 && phT < 6.5) || (phC > 0 && phC < 6.5);
            bool isPhAlkaline = phT > 8.5 || phC > 8.5;
            bool isPhAlarm = isPhAcid || isPhAlkaline;
            double effectivePh = (phT > 0 && (phT < 6.5 || phT > 8.5)) ? phT : (phC > 0 ? phC : 7.0);

            var phAlarm = _activeAlarms.FirstOrDefault(a => a.TagId == "ph");
            if (isPhAlarm)
            {
                string desc = isPhAcid ? "ácido corrosivo" : "alcalino";
                string msg = $"pH {desc} fuera de norma: {effectivePh:0.00} pH (Rango: 6.5 — 8.5)";

                if (phAlarm is null)
                {
                    phAlarm = new AlarmEvent
                    {
                        Id = _nextAlarmId++,
                        TagId = "ph",
                        NombreVisible = "pH de Agua",
                        AlarmType = isPhAcid ? "LOW_LIMIT" : "HIGH_LIMIT",
                        Severity = "high",
                        ValueNumeric = (decimal)Math.Round(effectivePh, 2),
                        Message = msg,
                        State = "ACTIVA",
                        StartedAt = now
                    };
                    _activeAlarms.Add(phAlarm);
                    _alarmHistory.Insert(0, phAlarm);
                    changed = true;
                }
                else
                {
                    phAlarm.ValueNumeric = (decimal)Math.Round(effectivePh, 2);
                    phAlarm.Message = msg;
                }
            }
            else if (phAlarm is not null && ((phT >= 6.5 && phT <= 8.5) || (phC >= 6.5 && phC <= 8.5)))
            {
                phAlarm.State = "RESUELTA";
                phAlarm.ClearedAt = now;
                _activeAlarms.Remove(phAlarm);
                changed = true;
            }

            // 3. Conductividad (detecta Agua con sal / intrusión salina)
            double condT = GetDictDouble(telemetry.Values, "conductividad_agua_tratada");
            double condC = GetDictDouble(telemetry.Values, "conductividad_agua_cruda");
            bool isCondAlarm = condT > 200.0 || condC > 200.0;
            double effectiveCond = condT > 200.0 ? condT : condC;

            var condAlarm = _activeAlarms.FirstOrDefault(a => a.TagId == "conductividad");
            if (isCondAlarm)
            {
                string msg = $"Conductividad elevada / intrusión salina: {effectiveCond:0} µS/cm (Nominal: < 200 µS/cm)";
                if (condAlarm is null)
                {
                    condAlarm = new AlarmEvent
                    {
                        Id = _nextAlarmId++,
                        TagId = "conductividad",
                        NombreVisible = "Conductividad",
                        AlarmType = "HIGH_LIMIT",
                        Severity = effectiveCond > 500.0 ? "critical" : "high",
                        ValueNumeric = (decimal)Math.Round(effectiveCond, 0),
                        Message = msg,
                        State = "ACTIVA",
                        StartedAt = now
                    };
                    _activeAlarms.Add(condAlarm);
                    _alarmHistory.Insert(0, condAlarm);
                    changed = true;
                }
                else
                {
                    condAlarm.ValueNumeric = (decimal)Math.Round(effectiveCond, 0);
                    condAlarm.Message = msg;
                }
            }
            else if (condAlarm is not null && (condT > 0 || condC > 0) && condT <= 200.0 && condC <= 200.0)
            {
                condAlarm.State = "RESUELTA";
                condAlarm.ClearedAt = now;
                _activeAlarms.Remove(condAlarm);
                changed = true;
            }

            if (changed)
            {
                SaveAlarmsToDisk();
            }
        }
    }

    public void RecordHistoryBatch(IEnumerable<TelemetryHistoryRecord> records)
    {
        lock (_sync)
        {
            foreach (var rec in records)
            {
                if (!DateTimeOffset.TryParse(rec.Timestamp, out var time)) continue;

                if (rec.PhAguaTratada.HasValue)
                    AddHistoryPointDirect("ph_agua_tratada", time, rec.PhAguaTratada.Value, "pH");
                if (rec.TurbidezAguaTratada.HasValue)
                    AddHistoryPointDirect("turbidez_agua_tratada", time, rec.TurbidezAguaTratada.Value, "NTU");
                if (rec.ConductividadAguaTratada.HasValue)
                    AddHistoryPointDirect("conductividad_agua_tratada", time, rec.ConductividadAguaTratada.Value, "µS/cm");
                if (rec.PhAguaCruda.HasValue)
                    AddHistoryPointDirect("ph_agua_cruda", time, rec.PhAguaCruda.Value, "pH");
                if (rec.TurbidezAguaCruda.HasValue)
                    AddHistoryPointDirect("turbidez_agua_cruda", time, rec.TurbidezAguaCruda.Value, "NTU");
                if (rec.ConductividadAguaCruda.HasValue)
                    AddHistoryPointDirect("conductividad_agua_cruda", time, rec.ConductividadAguaCruda.Value, "µS/cm");

                var mockTel = new TelemetryResponse
                {
                    PlcConnected = rec.PlcConnected != 0,
                    Timestamp = time.UtcDateTime,
                    Values = new Dictionary<string, object?>
                    {
                        ["ph_agua_tratada"] = rec.PhAguaTratada,
                        ["turbidez_agua_tratada"] = rec.TurbidezAguaTratada,
                        ["conductividad_agua_tratada"] = rec.ConductividadAguaTratada,
                        ["ph_agua_cruda"] = rec.PhAguaCruda,
                        ["turbidez_agua_cruda"] = rec.TurbidezAguaCruda,
                        ["conductividad_agua_cruda"] = rec.ConductividadAguaCruda
                    }
                };
                EvaluateTelemetryAlarms(mockTel);
            }
            SaveHistoryToDisk(force: true);
            SaveAlarmsToDisk();
        }
    }

    private DateTimeOffset _lastRecordedSampleTime = DateTimeOffset.MinValue;
    private double _lastRecordedSampleTurbT = -999;
    private double _lastRecordedSampleTurbC = -999;
    private double _lastRecordedSamplePhT = -999;
    private double _lastRecordedSamplePhC = -999;
    private double _lastRecordedSampleCondT = -999;
    private double _lastRecordedSampleCondC = -999;

    public void RecordTelemetrySample(TelemetryResponse telemetry)
    {
        EvaluateTelemetryAlarms(telemetry);
        if (telemetry?.Values == null || !telemetry.PlcConnected) return;

        lock (_sync)
        {
            var now = telemetry.Timestamp != default ? telemetry.Timestamp : DateTimeOffset.UtcNow;

            // Filtro Deadband: registrar de inmediato si hay cambio o si transcurrieron 2 segundos
            double currentTurbT = GetDictDouble(telemetry.Values, "turbidez_agua_tratada");
            double currentTurbC = GetDictDouble(telemetry.Values, "turbidez_agua_cruda");
            double currentPhT = GetDictDouble(telemetry.Values, "ph_agua_tratada");
            double currentPhC = GetDictDouble(telemetry.Values, "ph_agua_cruda");
            double currentCondT = GetDictDouble(telemetry.Values, "conductividad_agua_tratada");
            double currentCondC = GetDictDouble(telemetry.Values, "conductividad_agua_cruda");

            bool significantChange = Math.Abs(currentTurbT - _lastRecordedSampleTurbT) >= 0.10 ||
                                     Math.Abs(currentTurbC - _lastRecordedSampleTurbC) >= 0.50 ||
                                     Math.Abs(currentPhT - _lastRecordedSamplePhT) >= 0.04 ||
                                     Math.Abs(currentPhC - _lastRecordedSamplePhC) >= 0.08 ||
                                     Math.Abs(currentCondT - _lastRecordedSampleCondT) >= 5.0 ||
                                     Math.Abs(currentCondC - _lastRecordedSampleCondC) >= 10.0;

            if ((now - _lastRecordedSampleTime).TotalSeconds < 2.0 && !significantChange)
            {
                return; // Omitir muestra redundante en estado estacionario
            }

            _lastRecordedSampleTime = now;
            _lastRecordedSampleTurbT = currentTurbT;
            _lastRecordedSampleTurbC = currentTurbC;
            _lastRecordedSamplePhT = currentPhT;
            _lastRecordedSamplePhC = currentPhC;
            _lastRecordedSampleCondT = currentCondT;
            _lastRecordedSampleCondC = currentCondC;

            foreach (var (key, val) in telemetry.Values)
            {
                if (_historyStore.TryGetValue(key, out var list))
                {
                    double numVal = 0;
                    if (val is double d) numVal = d;
                    else if (val is float f) numVal = f;
                    else if (val is decimal dec) numVal = (double)dec;
                    else if (val is int i) numVal = i;
                    else if (val is long l) numVal = l;
                    else if (val is System.Text.Json.JsonElement je)
                    {
                        if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetDouble(out var parsedDbl))
                            numVal = parsedDbl;
                        else if (double.TryParse(je.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                            numVal = parsed;
                        else continue;
                    }
                    else if (val != null && double.TryParse(val.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    {
                        numVal = parsed;
                    }
                    else continue;

                    string unit = key.StartsWith("ph_") ? "pH" : (key.StartsWith("turbidez_") ? "NTU" : (key.StartsWith("conductividad_") ? "µS/cm" : ""));
                    string quality = "GOOD";
                    if (key == "turbidez_agua_tratada" && numVal > 1.80) quality = "BAD";
                    else if (key == "turbidez_agua_cruda" && numVal > 50.0) quality = "WARNING";

                    list.Add(new QualityHistoryPoint
                    {
                        RecordedAt = now,
                        TagId = key,
                        ValueNumeric = (decimal)Math.Round(numVal, 2),
                        Unit = unit,
                        Quality = quality
                    });

                    if (list.Count > MaxHistoryPointsPerTag)
                    {
                        list.RemoveAt(0);
                    }
                }
            }
            SaveHistoryToDisk();
        }
    }

    private void AddHistoryPointDirect(string tag, DateTimeOffset time, double value, string unit)
    {
        if (_historyStore.TryGetValue(tag, out var list))
        {
            string quality = "GOOD";
            if (tag == "turbidez_agua_tratada" && value > 1.80) quality = "BAD";
            else if (tag == "turbidez_agua_cruda" && value > 50.0) quality = "WARNING";

            list.Add(new QualityHistoryPoint
            {
                RecordedAt = time,
                TagId = tag,
                ValueNumeric = (decimal)Math.Round(value, 2),
                Unit = unit,
                Quality = quality
            });
        }
    }

    private void InitializeFullHistory()
    {
        var now = DateTimeOffset.UtcNow;
        const int totalPoints = 360; // 360 puntos * 20 s = 2 horas completas
        for (int i = totalPoints; i >= 0; i--)
        {
            var ptTime = now.AddSeconds(-i * 20);
            double t = (totalPoints - i) * 0.05;
            double slowWave = Math.Sin(t * 0.2) * 1.5;

            double phC = Math.Clamp(PhCrudaBase + NextGaussian(0, 0.02), 4.0, 10.0);
            double turbC = Math.Max(2.0, TurbidezCrudaBase + slowWave + NextGaussian(0, 0.3));
            double condC = Math.Max(100.0, ConductividadCrudaBase + NextGaussian(0, 2.0));

            double turbT = GetEstimatedTurbTratada(turbC);
            double phT = GetEstimatedPhTratada(phC);
            double condT = condC + (DosificadorCloroEstado ? 20.0 : 0.0) + NextGaussian(0, 1.0);

            double caudalB1 = BombaPrincipalEstado ? Math.Max(0.0, CaudalB1Base + NextGaussian(0, 0.15)) : 0.0;
            double presionB1 = BombaPrincipalEstado ? Math.Max(0.0, PresionB1Base + NextGaussian(0, 0.04)) : 0.0;
            double caudalDist = BombaPrincipalEstado ? Math.Max(0.0, caudalB1 * 0.97 + NextGaussian(0, 0.1)) : 0.0;

            AddHistoryPointDirect("ph_agua_cruda", ptTime, phC, "pH");
            AddHistoryPointDirect("turbidez_agua_cruda", ptTime, turbC, "NTU");
            AddHistoryPointDirect("conductividad_agua_cruda", ptTime, condC, "µS/cm");
            AddHistoryPointDirect("ph_agua_tratada", ptTime, phT, "pH");
            AddHistoryPointDirect("turbidez_agua_tratada", ptTime, turbT, "NTU");
            AddHistoryPointDirect("conductividad_agua_tratada", ptTime, condT, "µS/cm");
            AddHistoryPointDirect("caudal_bomba_principal", ptTime, caudalB1, "L/min");
            AddHistoryPointDirect("presion_bomba_principal", ptTime, presionB1, "bar");
            AddHistoryPointDirect("caudal_distribucion", ptTime, caudalDist, "L/min");
        }
    }

    private static double GetDictDouble(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var val) || val == null) return 0.0;
        if (val is double d) return d;
        if (val is float f) return f;
        if (val is decimal dec) return (double)dec;
        if (val is int i) return i;
        if (val is long l) return l;
        if (val is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetDouble(out var parsedDbl))
                return parsedDbl;
            if (double.TryParse(je.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedStr))
                return parsedStr;
            if (double.TryParse(je.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedAny))
                return parsedAny;
            return 0.0;
        }
        if (double.TryParse(val.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return 0.0;
    }

    public List<QualityHistoryPoint> GetQualityHistory(string tag, int limit = 100)
    {
        lock (_sync)
        {
            if (!_historyStore.TryGetValue(tag, out var list) || list == null || list.Count == 0)
            {
                return new List<QualityHistoryPoint>();
            }

            return list.Count > limit ? list.Skip(list.Count - limit).ToList() : new List<QualityHistoryPoint>(list);
        }
    }

    private double NextGaussian(double mean, double standardDeviation)
    {
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + standardDeviation * randStdNormal;
    }

    private readonly List<AlarmLimit> _inMemoryAlarmLimits = new()
    {
        new AlarmLimit
        {
            Id = 1,
            TagId = "ph",
            NombreVisible = "pH",
            AlarmType = "range",
            Enabled = true,
            Severity = "high",
            LimitLow = 6.5m,
            LimitHigh = 8.5m,
            Unit = "pH",
            Message = "pH fuera de rango normativo (6.5 — 8.5)",
            TechnicalNote = "Límites estándar según Resolución 2115 / 2007",
            UpdatedAt = DateTimeOffset.UtcNow
        },
        new AlarmLimit
        {
            Id = 2,
            TagId = "turbidez",
            NombreVisible = "Turbidez",
            AlarmType = "high",
            Enabled = true,
            Severity = "high",
            LimitHigh = 2.0m,
            Unit = "NTU",
            Message = "Turbidez excede el límite máximo admisible (2.0 NTU)",
            TechnicalNote = "Norma sanitaria máxima 2.0 NTU (óptimo < 0.5 NTU)",
            UpdatedAt = DateTimeOffset.UtcNow
        },
        new AlarmLimit
        {
            Id = 3,
            TagId = "conductividad",
            NombreVisible = "Conductividad",
            AlarmType = "high",
            Enabled = true,
            Severity = "medium",
            LimitHigh = 1000.0m,
            Unit = "µS/cm",
            Message = "Conductividad excede el límite admisible (1.000 µS/cm)",
            TechnicalNote = "Límite máximo recomendado según norma técnica",
            UpdatedAt = DateTimeOffset.UtcNow
        }
    };

    private readonly List<NotificationEmail> _inMemoryEmails = new()
    {
        new NotificationEmail { Id = 1, Email = "operaciones@potenzia.app", Name = "Operaciones PTAP", Enabled = true },
        new NotificationEmail { Id = 2, Email = "calidad@potenzia.app", Name = "Control de Calidad", Enabled = true }
    };

    private readonly List<NotificationWhatsApp> _inMemoryWhatsApp = new()
    {
        new NotificationWhatsApp { RawId = 1L, PhoneNumber = "+573001234567", Name = "Operador Turno PTAP", Enabled = true },
        new NotificationWhatsApp { RawId = 2L, PhoneNumber = "+573109876543", Name = "Supervisor de Planta", Enabled = true }
    };

    public List<AlarmLimit> GetAlarmLimits()
    {
        lock (_sync)
        {
            return _inMemoryAlarmLimits.Select(l => new AlarmLimit
            {
                Id = l.Id,
                TagId = l.TagId,
                NombreVisible = l.NombreVisible,
                AlarmType = l.AlarmType,
                Enabled = l.Enabled,
                Severity = l.Severity,
                LimitLow = l.LimitLow,
                LimitHigh = l.LimitHigh,
                BoolAlarmValue = l.BoolAlarmValue,
                Unit = l.Unit,
                Message = l.Message,
                TechnicalNote = l.TechnicalNote,
                UpdatedAt = l.UpdatedAt
            }).ToList();
        }
    }

    public AlarmLimit UpdateAlarmLimit(AlarmLimit limit)
    {
        lock (_sync)
        {
            var existing = _inMemoryAlarmLimits.FirstOrDefault(x => x.Id == limit.Id || (x.TagId == limit.TagId && x.AlarmType == limit.AlarmType));
            if (existing != null)
            {
                existing.Enabled = limit.Enabled;
                existing.Severity = limit.Severity;
                existing.LimitLow = limit.LimitLow;
                existing.LimitHigh = limit.LimitHigh;
                existing.BoolAlarmValue = limit.BoolAlarmValue;
                existing.Message = limit.Message;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                return existing;
            }

            if (limit.Id <= 0)
            {
                limit.Id = _inMemoryAlarmLimits.Count > 0 ? _inMemoryAlarmLimits.Max(x => x.Id) + 1 : 1;
            }
            limit.UpdatedAt = DateTimeOffset.UtcNow;
            _inMemoryAlarmLimits.Add(limit);
            return limit;
        }
    }

    public List<NotificationEmail> GetNotificationEmails()
    {
        lock (_sync)
        {
            return _inMemoryEmails.Select(e => new NotificationEmail
            {
                Id = e.Id,
                Email = e.Email,
                Name = e.Name,
                Enabled = e.Enabled,
                CreatedAt = e.CreatedAt
            }).ToList();
        }
    }

    public NotificationEmail AddNotificationEmail(string email, string? name)
    {
        lock (_sync)
        {
            var newId = _inMemoryEmails.Count > 0 ? _inMemoryEmails.Max(x => x.Id) + 1 : 1;
            var item = new NotificationEmail
            {
                Id = newId,
                Email = email,
                Name = name,
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _inMemoryEmails.Insert(0, item);
            return item;
        }
    }

    public List<NotificationWhatsApp> GetNotificationWhatsApp()
    {
        lock (_sync)
        {
            return _inMemoryWhatsApp.Select(w => new NotificationWhatsApp
            {
                RawId = w.RawId,
                PhoneNumber = w.PhoneNumber,
                Name = w.Name,
                ApiKey = w.ApiKey,
                Enabled = w.Enabled,
                RecordedAt = w.RecordedAt
            }).ToList();
        }
    }

    public NotificationWhatsApp AddNotificationWhatsApp(string phone, string? name)
    {
        lock (_sync)
        {
            var newId = _inMemoryWhatsApp.Count > 0 ? _inMemoryWhatsApp.Max(x => x.Id) + 1 : 1;
            var item = new NotificationWhatsApp
            {
                RawId = newId,
                PhoneNumber = phone,
                Name = name,
                Enabled = true,
                RecordedAt = DateTime.UtcNow
            };
            _inMemoryWhatsApp.Insert(0, item);
            return item;
        }
    }

    public bool DeleteNotificationWhatsApp(long id)
    {
        lock (_sync)
        {
            return _inMemoryWhatsApp.RemoveAll(x => x.Id == id) > 0;
        }
    }
}
