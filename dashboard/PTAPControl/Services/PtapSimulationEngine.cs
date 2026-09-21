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

    public int WatchdogPlc { get; private set; } = 1000;
    public int CommandSequence { get; private set; } = 1;
    public string LastCommandResult { get; private set; } = "Simulador nativo listo";

    // Parámetros base
    public double PhCrudaBase { get; set; } = 7.25;
    public double TurbidezCrudaBase { get; set; } = 18.5;
    public double ConductividadCrudaBase { get; set; } = 425.0;
    public double PresionB1Base { get; set; } = 2.45;
    public double CaudalB1Base { get; set; } = 15.0;

    // Modo de operación y escenario
    public PtapDataSourceMode CurrentMode { get; set; } = PtapDataSourceMode.SimulatedAuto;
    public string ActiveScenario { get; private set; } = "normal";
    public bool SimulatePlcDisconnected { get; set; } = false;

    private long _stepCounter = 0;
    private readonly Random _random = new();

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
                case "rain":
                    TurbidezCrudaBase = 85.0;
                    PhCrudaBase = 6.80;
                    break;
                case "pump_fault":
                    BombaPrincipalEstado = false;
                    PlcEnFalla = true;
                    break;
                case "chlorine_fault":
                    DosificadorCloroEstado = false;
                    break;
                case "tank_empty":
                    NivelTanqueAguaCruda = false;
                    BombaPrincipalEstado = false;
                    break;
                default: // normal
                    ActiveScenario = "normal";
                    TurbidezCrudaBase = 18.5;
                    PhCrudaBase = 7.25;
                    BombaPrincipalEstado = true;
                    DosificadorSulfatoEstado = true;
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
            }
            else if (target.Equals("dosificador_cloro", StringComparison.OrdinalIgnoreCase))
            {
                DosificadorCloroEstado = isStart;
            }
            else if (target.Equals("sistema", StringComparison.OrdinalIgnoreCase) && command.Equals("reset_alarmas", StringComparison.OrdinalIgnoreCase))
            {
                PlcEnFalla = false;
                ComandoRechazado = false;
            }
            else if (target.Equals("ALL", StringComparison.OrdinalIgnoreCase) || target.Equals("planta", StringComparison.OrdinalIgnoreCase))
            {
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
            var list = new List<AlarmEvent>();
            if (PlcEnFalla || SimulatePlcDisconnected)
            {
                list.Add(new AlarmEvent
                {
                    Id = 1,
                    TagId = "plc_comms",
                    NombreVisible = "Enlace PLC",
                    AlarmType = "COMM_FAULT",
                    Severity = "critical",
                    Message = "Pérdida de enlace con el autómata Siemens S7-1200",
                    State = "ACTIVE",
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                });
            }

            if (!BombaPrincipalEstado && ActiveScenario == "pump_fault")
            {
                list.Add(new AlarmEvent
                {
                    Id = 2,
                    TagId = "bomba_principal_estado",
                    NombreVisible = "Bomba 1",
                    AlarmType = "TRIP",
                    Severity = "high",
                    Message = "Disparo por sobrecorriente o sobrecalentamiento en Bomba 1",
                    State = "ACTIVE",
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
                });
            }

            if (!DosificadorCloroEstado && ActiveScenario == "chlorine_fault")
            {
                list.Add(new AlarmEvent
                {
                    Id = 3,
                    TagId = "dosificador_cloro_estado",
                    NombreVisible = "Dosificador Cloro",
                    AlarmType = "LOW_DOSAGE",
                    Severity = "high",
                    Message = "Caudal de hipoclorito de sodio fuera de rango mínimo",
                    State = "ACTIVE",
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
                });
            }

            return list;
        }
    }

    public List<QualityHistoryPoint> GetQualityHistory(string tag, int limit = 100)
    {
        lock (_sync)
        {
            var points = new List<QualityHistoryPoint>();
            var now = DateTimeOffset.UtcNow;
            double baseVal = tag switch
            {
                "ph_agua_cruda" => PhCrudaBase,
                "turbidez_agua_cruda" => TurbidezCrudaBase,
                "conductividad_agua_cruda" => ConductividadCrudaBase,
                "ph_agua_tratada" => 7.1,
                "turbidez_agua_tratada" => 0.45,
                "conductividad_agua_tratada" => 440.0,
                _ => 10.0
            };

            for (int i = limit; i >= 0; i--)
            {
                points.Add(new QualityHistoryPoint
                {
                    RecordedAt = now.AddSeconds(-i * 5),
                    ValueNumeric = (decimal)Math.Round(baseVal + NextGaussian(0, Math.Max(0.01, baseVal * 0.05)), 2),
                    TagId = tag
                });
            }
            return points;
        }
    }

    private double NextGaussian(double mean, double standardDeviation)
    {
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + standardDeviation * randStdNormal;
    }
}
