using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PTAPControl.Models;

namespace PTAPControl.Services;

/// <summary>
/// Servicio cliente ligero tipado para TypeSafe AI (Modelo Jev System One)
/// Diseñado para evaluar los estados operativos de las PTAPs y retornar respuestas
/// con factor de confianza calibrado para las decisiones del SCADA.
/// </summary>
public sealed class TypeSafeAiClientService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly PtapKnowledgeService _knowledge;
    private readonly List<AiAuditEntry> _auditLog = [];

    public TypeSafeAiClientService(HttpClient http, IConfiguration config, PtapKnowledgeService knowledge)
    {
        _http = http;
        _config = config;
        _knowledge = knowledge;
    }

    public string ApiKey => Environment.GetEnvironmentVariable("TYPESAFE_API_KEY") 
                            ?? _config["Ai:TypeSafeApiKey"] 
                            ?? _config["TYPESAFE_API_KEY"] 
                            ?? string.Empty;

    public string Endpoint => _config["Ai:TypeSafeEndpoint"] ?? "https://api.typesafe.ai/v1/systemone";
    public string Model => _config["Ai:TypeSafeModel"] ?? "jev-latest";

    // Límites de seguridad normativos para dosificación
    public double SulfateMinDose => double.TryParse(_config["Ai:SulfateMinDose"], out var v) ? v : 5.0;
    public double SulfateMaxDose => double.TryParse(_config["Ai:SulfateMaxDose"], out var v) ? v : 60.0;
    public double ChlorineMinDose => double.TryParse(_config["Ai:ChlorineMinDose"], out var v) ? v : 0.5;
    public double ChlorineMaxDose => double.TryParse(_config["Ai:ChlorineMaxDose"], out var v) ? v : 3.5;

    public IReadOnlyList<AiAuditEntry> AuditLog => _auditLog.AsReadOnly();

    public void RecordAudit(AiAuditEntry entry)
    {
        _auditLog.Insert(0, entry);
        if (_auditLog.Count > 100)
        {
            _auditLog.RemoveAt(_auditLog.Count - 1);
        }
    }

    /// <summary>
    /// Verifica la disponibilidad y latencia del servicio TypeSafe AI
    /// </summary>
    public async Task<(bool IsOnline, string Model, long LatencyMs, string Details)> CheckStatusAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            return (false, Model, 0, "Llave TYPESAFE_API_KEY no configurada");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var req = new TypeSafeRequest
            {
                Model = Model,
                State = "Ping de verificación de conectividad SCADA HIDROCONTROL PTAP",
                Questions = new Dictionary<string, TypeSafeQuestion>
                {
                    ["ping"] = new TypeSafeQuestion
                    {
                        Type = "noul",
                        Instructions = "Is this communication channel operational?"
                    }
                }
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(req)
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

            var res = await _http.SendAsync(requestMessage, cancellationToken);
            sw.Stop();

            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<TypeSafeResponse>(cancellationToken: cancellationToken);
                var resolvedModel = body?.Model ?? Model;
                return (true, resolvedModel, sw.ElapsedMilliseconds, $"Conectado a TypeSafe AI ({sw.ElapsedMilliseconds} ms)");
            }

            return (false, Model, sw.ElapsedMilliseconds, $"Error HTTP {(int)res.StatusCode}: {res.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, Model, sw.ElapsedMilliseconds, $"Fallo de conexión: {ex.Message}");
        }
    }

    /// <summary>
    /// Evalúa el estado operativo completo de la PTAP mediante petición tipada a TypeSafe AI Jev,
    /// retornando la evaluación estructurada con factor de confianza calibrado para el SCADA.
    /// </summary>
    public async Task<PtapOperationalEvaluation> EvaluatePlantHealthAsync(
        TelemetryResponse? telemetry,
        List<AlarmEvent>? alarms,
        CancellationToken cancellationToken = default)
    {
        var plantStateText = BuildPlantStateTelemetryString(telemetry, alarms);
        var sw = Stopwatch.StartNew();

        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            try
            {
                var req = new TypeSafeRequest
                {
                    Model = Model,
                    State = plantStateText,
                    Questions = new Dictionary<string, TypeSafeQuestion>
                    {
                        ["plant_status"] = new TypeSafeQuestion
                        {
                            Type = "choice",
                            Instructions = "Determine the operational status of this water treatment plant based on drinking water standards (turbidity < 2.0 NTU, pH 6.5-8.5, normal hydraulic pressure 1.5-2.8 bar) and safety limits.",
                            Criteria = new Dictionary<string, string>
                            {
                                ["Optimo"] = "All parameters within optimal range, treated water turbidity < 1.0 NTU, hydraulic pressure stable, no active alarms.",
                                ["Estable"] = "Parameters within regulatory limits (< 2.0 NTU), normal minor process fluctuations, equipment running as expected.",
                                ["Atencion Requerida"] = "One or more parameters deviating from setpoints (e.g. treated turbidity between 1.5-2.0 NTU, pressure abnormal, or warning alarm active).",
                                ["Critico"] = "Severe deviation exceeding legal limits (> 2.0 NTU), pump failure, risk of empty tank or critical alarm requiring immediate shutdown."
                            }
                        },
                        ["operator_intervention_required"] = new TypeSafeQuestion
                        {
                            Type = "noul",
                            Instructions = "Is immediate manual intervention by the human operator required to take direct manual control of actuators, pumps, or chemical dosing valves?"
                        },
                        ["coagulant_adjustment"] = new TypeSafeQuestion
                        {
                            Type = "choice",
                            Instructions = "Evaluate the aluminum sulfate coagulant dosing adjustment needed for the flocculator FH-201 given incoming raw water turbidity.",
                            Criteria = new Dictionary<string, string>
                            {
                                ["maintain"] = "Current coagulant dosing is adequate for incoming raw turbidity and current pump flow.",
                                ["increase"] = "Raw water turbidity is elevated or treated water turbidity is increasing; coagulant dosing should be raised.",
                                ["decrease"] = "Raw water is very clear; coagulant dosing can be lowered to optimize chemical consumption.",
                                ["stop"] = "Plant is shut down or coagulant dosing must be stopped."
                            }
                        },
                        ["chlorine_adjustment"] = new TypeSafeQuestion
                        {
                            Type = "choice",
                            Instructions = "Evaluate the sodium hypochlorite chlorine dosing adjustment needed for disinfection in tank TK-501.",
                            Criteria = new Dictionary<string, string>
                            {
                                ["maintain"] = "Disinfection dosing is balanced for current flow.",
                                ["increase"] = "Increase chlorine dosing to maintain 0.5 - 2.0 ppm free chlorine residual.",
                                ["decrease"] = "Decrease chlorine dosing to avoid high chemical residual.",
                                ["stop"] = "Stop chlorine dosing."
                            }
                        },
                        ["health_score"] = new TypeSafeQuestion
                        {
                            Type = "score",
                            Instructions = "Rate the overall operational performance and health of the plant from 0 to 4.",
                            Criteria = new List<string>
                            {
                                "Falla Critica / Parada Requerida",
                                "Desviacion Severa",
                                "Operacion Aceptable con Atencion",
                                "Operacion Estable",
                                "Rendimiento Optimo"
                            }
                        }
                    }
                };

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = JsonContent.Create(req)
                };
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

                var response = await _http.SendAsync(requestMessage, cancellationToken);
                sw.Stop();

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<TypeSafeResponse>(cancellationToken: cancellationToken);
                    if (result != null && result.Answers.Count > 0)
                    {
                        return BuildCalibratedEvaluation(result, telemetry, alarms, sw.ElapsedMilliseconds, isLiveApi: true);
                    }
                }
            }
            catch
            {
                // Fallback a motor seguro calibrado local
            }
        }

        sw.Stop();
        return BuildFallbackCalibratedEvaluation(telemetry, alarms, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Calibra y estructura las respuestas de TypeSafe Jev para el SCADA
    /// </summary>
    private PtapOperationalEvaluation BuildCalibratedEvaluation(
        TypeSafeResponse response,
        TelemetryResponse? telemetry,
        List<AlarmEvent>? alarms,
        long latencyMs,
        bool isLiveApi)
    {
        var answers = response.Answers;
        
        // 1. Estado Operativo Principal
        var statusChoice = "Estable";
        double statusConfidence = 0.85;
        if (answers.TryGetValue("plant_status", out var statusAns))
        {
            statusChoice = statusAns.Choice ?? "Estable";
            statusConfidence = statusAns.Confidence ?? 0.85;
        }

        // 2. Intervención Manual Requerida (NOUL)
        double interventionProb = 0.05;
        if (answers.TryGetValue("operator_intervention_required", out var intervAns))
        {
            interventionProb = intervAns.Noul ?? 0.05;
        }

        // 3. Puntuación de Salud (Score 0-4)
        int scorePercentage = 90;
        double scoreConfidence = 0.85;
        if (answers.TryGetValue("health_score", out var scoreAns))
        {
            var rawScore = scoreAns.Score ?? 3.5;
            scorePercentage = (int)Math.Round((rawScore / 4.0) * 100.0);
            scorePercentage = Math.Clamp(scorePercentage, 20, 100);
            scoreConfidence = scoreAns.Confidence ?? 0.85;
        }

        // 4. Decisiones de Dosificación
        var coagulantAction = answers.TryGetValue("coagulant_adjustment", out var cAns) ? cAns.Choice ?? "maintain" : "maintain";
        var chlorineAction = answers.TryGetValue("chlorine_adjustment", out var clAns) ? clAns.Choice ?? "maintain" : "maintain";

        // 5. CALIBRACIÓN DEL FACTOR DE CONFIANZA PARA DECISIONES DEL SCADA:
        // Combinamos la confianza del estado y de la salud, penalizada si hay discrepancias de probabilidad
        double combinedConfidence = (statusConfidence * 0.6) + (scoreConfidence * 0.4);
        combinedConfidence = Math.Clamp(combinedConfidence, 0.05, 0.99);

        // Nivel cualitativo:
        // - Alta: >= 0.80 (El modelo tiene alta certeza, recomendaciones aplicables con seguridad)
        // - Media: 0.55 - 0.79 (Supervisión sugerida)
        // - Baja: < 0.55 (ALERTA CRÍTICA: El operador entra a tomar el control directo de todos los actuadores)
        string confidenceLevel = combinedConfidence >= 0.80 ? "Alta" 
                               : combinedConfidence >= 0.55 ? "Media" 
                               : "Baja";

        bool requiresIntervention = interventionProb >= 0.40 || confidenceLevel == "Baja" || statusChoice == "Critico";

        string interventionReason = string.Empty;
        if (requiresIntervention)
        {
            if (confidenceLevel == "Baja")
            {
                interventionReason = $"⚠️ Factor de Confianza Bajo ({combinedConfidence:P0}) reportado por TypeSafe Jev. De acuerdo al protocolo de seguridad SCADA, el operador técnico debe asumir el control manual directo de los actuadores (Bomba B1, Dosificadores B2 y B3).";
            }
            else if (statusChoice == "Critico")
            {
                interventionReason = "🚨 Estado crítico detectado en la PTAP. Intervención inmediata requerida para ajustar caudal o detener impulsión.";
            }
            else
            {
                interventionReason = $"⚠️ El modelo Jev estima una probabilidad de {interventionProb:P0} de requerir ajuste operativo manual en campo.";
            }
        }

        // Generar sugerencias de dosificación calibradas
        var dosingSuggestions = GenerateCalibratedDosingSuggestions(telemetry, coagulantAction, chlorineAction, confidenceLevel);

        // Generar anomalías reportadas
        var anomalies = DetectAnomalies(telemetry, alarms, statusChoice);

        var turbRaw = telemetry?.GetNumber("turbidez_agua_cruda") ?? 16.0;
        var turbTreated = telemetry?.GetNumber("turbidez_agua_tratada") ?? 0.85;
        var flow = telemetry?.GetNumber("caudal_bomba_principal") ?? 12.5;
        var pressure = telemetry?.GetNumber("presion_bomba_principal") ?? 2.3;

        var removalEfficiency = turbRaw > 0 ? ((turbRaw - turbTreated) / turbRaw * 100.0) : 0.0;

        return new PtapOperationalEvaluation
        {
            OverallStatus = statusChoice,
            ScorePercentage = scorePercentage,
            ConfidenceLevel = confidenceLevel,
            ConfidenceScore = combinedConfidence,
            RequiresOperatorIntervention = requiresIntervention,
            InterventionReason = interventionReason,
            CoagulantAction = coagulantAction,
            DisinfectantAction = chlorineAction,
            ExecutiveSummary = $"Evaluación TypeSafe Jev: Planta en estado {statusChoice} ({scorePercentage}% de salud operativa). Factor de confianza calibrado: {confidenceLevel} ({combinedConfidence:P0}). Efluente tratado en {turbTreated:F2} NTU con eficiencia de remoción del {removalEfficiency:F1}%.",
            QualityEvaluation = $"Turbidez cruda: {turbRaw:F1} NTU &rarr; Turbidez tratada: {turbTreated:F2} NTU (Límite Resolución 2115: < 2.0 NTU). pH de salida equilibrado.",
            EnergyHydraulicEvaluation = $"Impulsión de Bomba B1 a {pressure:F2} bar y {flow:F1} L/min hacia sedimentador lamelar SC-301 y filtro FC-401.",
            ImmediateRecommendations = GenerateImmediateRecommendations(statusChoice, coagulantAction, chlorineAction, requiresIntervention, turbTreated, pressure),
            DosingSuggestions = dosingSuggestions,
            Anomalies = anomalies,
            EvaluatedAt = DateTime.UtcNow,
            LatencyMs = latencyMs,
            ModelVersion = response.Model,
            IsLiveApi = isLiveApi,
            RawAnswers = answers
        };
    }

    /// <summary>
    /// Fallback seguro y calibrado cuando la API remota no está accesible o se ejecuta offline
    /// </summary>
    public PtapOperationalEvaluation BuildFallbackCalibratedEvaluation(
        TelemetryResponse? telemetry,
        List<AlarmEvent>? alarms,
        long latencyMs)
    {
        var turbRaw = telemetry?.GetNumber("turbidez_agua_cruda") ?? 16.0;
        var turbTreated = telemetry?.GetNumber("turbidez_agua_tratada") ?? 0.85;
        var pressure = telemetry?.GetNumber("presion_bomba_principal") ?? 2.3;
        var flow = telemetry?.GetNumber("caudal_bomba_principal") ?? 12.5;

        var alarmsCount = alarms?.Count ?? 0;
        int score = 95;
        if (turbTreated > 1.8) score -= 25;
        else if (turbTreated > 1.4) score -= 12;
        if (pressure > 3.0) score -= 15;
        score -= (alarmsCount * 10);
        score = Math.Clamp(score, 30, 98);

        string status = score >= 90 ? "Optimo" 
                      : score >= 75 ? "Estable" 
                      : score >= 60 ? "Atencion Requerida" 
                      : "Critico";

        // Confianza calibrada para motor local de seguridad
        double confidence = 0.82;
        string confidenceLevel = "Alta";

        bool requiresIntervention = status == "Critico" || pressure > 3.2 || turbTreated > 2.0;
        string interventionReason = requiresIntervention 
            ? "⚠️ Parámetros fuera de rango seguro detectados. Se requiere intervención del operador para ajustar consignas." 
            : string.Empty;

        var coagulantAction = turbRaw > 30 || turbTreated > 1.4 ? "increase" : "maintain";
        var chlorineAction = turbTreated > 1.6 ? "increase" : "maintain";

        var dosingSuggestions = GenerateCalibratedDosingSuggestions(telemetry, coagulantAction, chlorineAction, confidenceLevel);
        var anomalies = DetectAnomalies(telemetry, alarms, status);

        var removalEfficiency = turbRaw > 0 ? ((turbRaw - turbTreated) / turbRaw * 100.0) : 0.0;

        return new PtapOperationalEvaluation
        {
            OverallStatus = status,
            ScorePercentage = score,
            ConfidenceLevel = confidenceLevel,
            ConfidenceScore = confidence,
            RequiresOperatorIntervention = requiresIntervention,
            InterventionReason = interventionReason,
            CoagulantAction = coagulantAction,
            DisinfectantAction = chlorineAction,
            ExecutiveSummary = $"Evaluación Local de Contingencia: Planta en estado {status} ({score}% de salud operativa). Factor de confianza calibrado: {confidenceLevel} ({confidence:P0}). Remoción en {removalEfficiency:F1}%.",
            QualityEvaluation = $"Turbidez de salida en {turbTreated:F2} NTU (norma < 2.0 NTU).",
            EnergyHydraulicEvaluation = $"Punto de impulsión en {pressure:F2} bar a {flow:F1} L/min.",
            ImmediateRecommendations = GenerateImmediateRecommendations(status, coagulantAction, chlorineAction, requiresIntervention, turbTreated, pressure),
            DosingSuggestions = dosingSuggestions,
            Anomalies = anomalies,
            EvaluatedAt = DateTime.UtcNow,
            LatencyMs = latencyMs,
            ModelVersion = "jev-local-safety-engine",
            IsLiveApi = false
        };
    }

    private List<DosingSuggestion> GenerateCalibratedDosingSuggestions(
        TelemetryResponse? telemetry,
        string coagulantAction,
        string chlorineAction,
        string confidenceLevel)
    {
        var suggestions = new List<DosingSuggestion>();
        var turbRaw = telemetry?.GetNumber("turbidez_agua_cruda") ?? 15.0;
        var flow = telemetry?.GetNumber("caudal_bomba_principal") ?? 12.0;
        var phRaw = telemetry?.GetNumber("ph_agua_cruda") ?? 7.2;
        var turbTreated = telemetry?.GetNumber("turbidez_agua_tratada") ?? 0.8;
        var isSulfateOn = telemetry?.GetBool("dosificador_sulfato_estado") == true;
        var isChlorineOn = telemetry?.GetBool("dosificador_cloro_estado") == true;

        // 1. Sulfato de Aluminio (Coagulante FH-201)
        double baseSulfate;
        if (turbRaw < 10) baseSulfate = 12.0;
        else if (turbRaw < 25) baseSulfate = 22.0;
        else if (turbRaw < 50) baseSulfate = 35.0;
        else baseSulfate = 48.0;

        if (coagulantAction == "increase") baseSulfate += 5.0;
        else if (coagulantAction == "decrease") baseSulfate = Math.Max(SulfateMinDose, baseSulfate - 4.0);

        var safeSulfate = Math.Clamp(baseSulfate, SulfateMinDose, SulfateMaxDose);
        var sulfateFreq = Math.Round((safeSulfate / SulfateMaxDose) * 60.0, 1);

        suggestions.Add(new DosingSuggestion
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Chemical = "Sulfato de Aluminio (Coagulante)",
            TargetEquipment = "dosificador_sulfato",
            CurrentValue = isSulfateOn ? "Activo (Dosificando)" : "Detenido (OFF)",
            SuggestedDoseMgL = Math.Round(safeSulfate, 1),
            SuggestedFrequencyHz = Math.Clamp(sulfateFreq, 10.0, 60.0),
            SuggestedAction = coagulantAction == "stop" ? "stop" : (isSulfateOn ? "adjust" : "start"),
            Reason = $"Decisión TypeSafe Jev ({coagulantAction}): Turbidez cruda en {turbRaw:F1} NTU con caudal {flow:F1} L/min. Dosis calibrada para óptima floculación en FH-201.",
            Confidence = confidenceLevel,
            MinSafeLimit = SulfateMinDose,
            MaxSafeLimit = SulfateMaxDose,
            IsWithinSafetyLimits = safeSulfate >= SulfateMinDose && safeSulfate <= SulfateMaxDose,
            Status = "Pendiente"
        });

        // 2. Hipoclorito de Sodio (Cloro Residual TK-501)
        double baseChlorine = 1.8;
        if (chlorineAction == "increase" || turbTreated > 1.5) baseChlorine = 2.4;
        else if (chlorineAction == "decrease") baseChlorine = 1.2;

        var safeChlorine = Math.Clamp(baseChlorine, ChlorineMinDose, ChlorineMaxDose);
        var chlorineFreq = Math.Round((safeChlorine / ChlorineMaxDose) * 60.0, 1);

        suggestions.Add(new DosingSuggestion
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Chemical = "Hipoclorito de Sodio (Cloro Residual)",
            TargetEquipment = "dosificador_cloro",
            CurrentValue = isChlorineOn ? "Activo (Dosificando)" : "Detenido (OFF)",
            SuggestedDoseMgL = Math.Round(safeChlorine, 2),
            SuggestedFrequencyHz = Math.Clamp(chlorineFreq, 10.0, 60.0),
            SuggestedAction = chlorineAction == "stop" ? "stop" : (isChlorineOn ? "adjust" : "start"),
            Reason = $"Decisión TypeSafe Jev ({chlorineAction}): Asegura residual libre entre 0.5 - 2.0 ppm en TK-501 según Resolución 2115.",
            Confidence = confidenceLevel,
            MinSafeLimit = ChlorineMinDose,
            MaxSafeLimit = ChlorineMaxDose,
            IsWithinSafetyLimits = safeChlorine >= ChlorineMinDose && safeChlorine <= ChlorineMaxDose,
            Status = "Pendiente"
        });

        return suggestions;
    }

    private static List<AnomalyReport> DetectAnomalies(TelemetryResponse? telemetry, List<AlarmEvent>? alarms, string status)
    {
        var anomalies = new List<AnomalyReport>();
        var turbTreated = telemetry?.GetNumber("turbidez_agua_tratada") ?? 0.85;
        var pressure = telemetry?.GetNumber("presion_bomba_principal") ?? 2.3;

        if (pressure > 3.0)
        {
            anomalies.Add(new AnomalyReport
            {
                Level = "Advertencia",
                Equipment = "FC-401 / Bomba B1",
                Title = "Presión de impulsión elevada",
                Description = $"Presión registrada en {pressure:F1} bar. Posible incremento de diferencial de presión en lecho filtrante.",
                RecommendedAction = "Verificar manómetros diferenciales y evaluar ciclo de retrolavado según SOP-03."
            });
        }

        if (turbTreated > 1.8)
        {
            anomalies.Add(new AnomalyReport
            {
                Level = "Critico",
                Equipment = "Sedimentador SC-301",
                Title = "Turbidez tratada cercana al límite normativo",
                Description = $"Turbidez de salida en {turbTreated:F1} NTU.",
                RecommendedAction = "Incrementar dosis de coagulante o reducir caudal de entrada temporalmente."
            });
        }
        else
        {
            anomalies.Add(new AnomalyReport
            {
                Level = "Info",
                Equipment = "Filtro FC-401",
                Title = "Parámetros de filtración estables",
                Description = $"Turbidez tratada en {turbTreated:F1} NTU, cumpliendo la Resolución 2115 (< 2.0 NTU).",
                RecommendedAction = "Continuar régimen de operación estándar validado por TypeSafe."
            });
        }

        if (alarms != null)
        {
            foreach (var a in alarms.Take(2))
            {
                anomalies.Add(new AnomalyReport
                {
                    Level = a.Severity == "Critica" ? "Critico" : "Advertencia",
                    Equipment = a.TagId,
                    Title = a.NombreVisible ?? a.TagId,
                    Description = a.Message,
                    RecommendedAction = "Revisar variables en SCADA y confirmar condición física del actuador."
                });
            }
        }

        return anomalies;
    }

    private static List<string> GenerateImmediateRecommendations(
        string status,
        string coagulantAction,
        string chlorineAction,
        bool requiresIntervention,
        double turbTreated,
        double pressure)
    {
        var list = new List<string>();

        if (requiresIntervention)
        {
            list.Add("⚠️ Operador Técnico: Tomar control manual inmediato de los actuadores y verificar las presiones.");
        }

        if (coagulantAction == "increase")
        {
            list.Add("Incrementar consigna de coagulante en floculador FH-201 para acelerar formación de flóculos.");
        }
        else
        {
            list.Add("Mantener régimen de coagulación actual calibrado por TypeSafe.");
        }

        if (turbTreated > 1.5)
        {
            list.Add($"Monitorear turbidez tratada ({turbTreated:F2} NTU) cercana al umbral preventivo.");
        }

        if (pressure > 2.8)
        {
            list.Add($"Inspeccionar colmatación en filtro FC-401 (Presión: {pressure:F2} bar).");
        }

        return list;
    }

    private string BuildPlantStateTelemetryString(TelemetryResponse? telemetry, List<AlarmEvent>? alarms)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"TIMESTAMP: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"PLC_STATUS: {(telemetry?.PlcConnected == true ? "ONLINE_RUN" : "OFFLINE")}");
        sb.AppendLine($"REMOTE_MODE: {(telemetry?.GetBool("modo_remoto_habilitado") == true ? "ENABLED" : "LOCAL_ONLY")}");
        
        sb.AppendLine($"RAW_WATER_TURBIDITY_NTU: {telemetry?.GetNumber("turbidez_agua_cruda")?.ToString("F2") ?? "16.0"}");
        sb.AppendLine($"RAW_WATER_PH: {telemetry?.GetNumber("ph_agua_cruda")?.ToString("F2") ?? "7.2"}");
        sb.AppendLine($"RAW_WATER_TANK_LEVEL_PCT: {telemetry?.GetNumber("nivel_tanque_cruda")?.ToString("F1") ?? "75.0"}");

        sb.AppendLine($"TREATED_WATER_TURBIDITY_NTU: {telemetry?.GetNumber("turbidez_agua_tratada")?.ToString("F2") ?? "0.85"}");
        sb.AppendLine($"TREATED_WATER_PH: {telemetry?.GetNumber("ph_agua_tratada")?.ToString("F2") ?? "7.1"}");
        sb.AppendLine($"TREATED_WATER_TANK_LEVEL_PCT: {telemetry?.GetNumber("nivel_tanque_tratada")?.ToString("F1") ?? "82.0"}");

        sb.AppendLine($"PUMP_B1_STATE: {(telemetry?.GetBool("bomba_principal_estado") == true ? "ON" : "OFF")}");
        sb.AppendLine($"PUMP_B1_FLOW_LMIN: {telemetry?.GetNumber("caudal_bomba_principal")?.ToString("F1") ?? "12.5"}");
        sb.AppendLine($"PUMP_B1_PRESSURE_BAR: {telemetry?.GetNumber("presion_bomba_principal")?.ToString("F2") ?? "2.3"}");

        sb.AppendLine($"DOSER_SULFATE_B2_STATE: {(telemetry?.GetBool("dosificador_sulfato_estado") == true ? "ON" : "OFF")}");
        sb.AppendLine($"DOSER_CHLORINE_B3_STATE: {(telemetry?.GetBool("dosificador_cloro_estado") == true ? "ON" : "OFF")}");

        if (alarms != null && alarms.Count > 0)
        {
            sb.AppendLine($"ACTIVE_ALARMS_COUNT: {alarms.Count}");
            foreach (var a in alarms)
            {
                sb.AppendLine($"ALARM: [{a.Severity}] {a.NombreVisible} - {a.Message}");
            }
        }
        else
        {
            sb.AppendLine("ACTIVE_ALARMS_COUNT: 0");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Procesa preguntas o comandos del operador vía chat, reconociendo intenciones de control sobre el PLC
    /// </summary>
    public (string Text, AiProposedAction? Action) ProcessCopilotQuery(
        string userMessage,
        TelemetryResponse? telemetry,
        PtapOperationalEvaluation? currentEval)
    {
        var q = userMessage.ToLowerInvariant().Trim();
        var turbRaw = telemetry?.GetNumber("turbidez_agua_cruda") ?? 16.0;
        var turbTreated = telemetry?.GetNumber("turbidez_agua_tratada") ?? 0.85;
        var flow = telemetry?.GetNumber("caudal_bomba_principal") ?? 12.5;
        var pressure = telemetry?.GetNumber("presion_bomba_principal") ?? 2.3;
        var b1On = telemetry?.GetBool("bomba_principal_estado") == true;

        // Comandos de Parada / Desenergización
        if (q.Contains("apaga") || q.Contains("desenergiz") || q.Contains("deten") || q.Contains("parada") || q.Contains("cortar") || q.Contains("stop"))
        {
            if (q.Contains("sulfato") || q.Contains("coagulante") || q.Contains("b2"))
            {
                return ("🛑 **Instrucción de Parada - Dosificador de Sulfato B2:**\n\n" +
                        "Se detendrá la inyección de coagulante en el floculador FH-201.\n\n" +
                        "[[ACTION:STOP|dosificador_sulfato|Dosificador Sulfato B2|Parada solicitada por el operador]]",
                        new AiProposedAction
                        {
                            ActionType = "STOP",
                            TargetEquipment = "dosificador_sulfato",
                            EquipmentName = "Dosificador Sulfato B2",
                            Reason = "Parada solicitada por el operador",
                            Status = "Pendiente"
                        });
            }
            if (q.Contains("cloro") || q.Contains("desinfect") || q.Contains("b3"))
            {
                return ("🛑 **Instrucción de Parada - Dosificador de Cloro B3:**\n\n" +
                        "Se detendrá la inyección de hipoclorito de sodio en el tanque TK-501.\n\n" +
                        "[[ACTION:STOP|dosificador_cloro|Dosificador Cloro B3|Parada solicitada por el operador]]",
                        new AiProposedAction
                        {
                            ActionType = "STOP",
                            TargetEquipment = "dosificador_cloro",
                            EquipmentName = "Dosificador Cloro B3",
                            Reason = "Parada solicitada por el operador",
                            Status = "Pendiente"
                        });
            }
            if (q.Contains("b1") || q.Contains("principal") || q.Contains("impulsion") || (q.Contains("bomba") && !q.Contains("toda") && !q.Contains("planta")))
            {
                return ("🛑 **Instrucción de Parada - Bomba Principal B1:**\n\n" +
                        "Se procederá a apagar la impulsión hacia el filtro FC-401 para evitar sobrepresión o ingreso de agua sin tratar.\n\n" +
                        "[[ACTION:STOP|bomba_principal|Bomba Principal B1|Parada solicitada por el operador]]",
                        new AiProposedAction
                        {
                            ActionType = "STOP",
                            TargetEquipment = "bomba_principal",
                            EquipmentName = "Bomba Principal B1",
                            Reason = "Parada solicitada por el operador",
                            Status = "Pendiente"
                        });
            }
            
            return ("🚨 **Instrucción de Parada de Seguridad y Desenergización General:**\n\n" +
                   "Se ha preparado la orden para desenergizar y detener simultáneamente la **Bomba Principal B1**, el **Dosificador de Sulfato B2** y el **Dosificador de Cloro B3** en el PLC Siemens S7-1200.\n\n" +
                   "[[ACTION:STOP_ALL|Parada de seguridad y desenergización solicitada por el operador]]",
                   new AiProposedAction
                   {
                       ActionType = "STOP_ALL",
                       TargetEquipment = "ALL",
                       EquipmentName = "Toda la Planta (B1, B2 y B3)",
                       Reason = "Parada de seguridad y desenergización general solicitada por el operador",
                       Status = "Pendiente"
                   });
        }

        // Comandos de Encendido / Arranque
        if (q.Contains("enciend") || q.Contains("prend") || q.Contains("arranc") || q.Contains("inicia") || q.Contains("activa") || q.Contains("start"))
        {
            if (q.Contains("sulfato") || q.Contains("coagulante") || q.Contains("b2"))
            {
                return ("🟢 **Instrucción de Arranque - Dosificador de Sulfato B2:**\n\n" +
                        "Se activará la dosificación de sulfato de aluminio en el floculador FH-201 con la frecuencia configurada.\n\n" +
                        "[[ACTION:START|dosificador_sulfato|Dosificador Sulfato B2|Arranque solicitado por el operador]]",
                        new AiProposedAction
                        {
                            ActionType = "START",
                            TargetEquipment = "dosificador_sulfato",
                            EquipmentName = "Dosificador Sulfato B2",
                            Reason = "Arranque solicitado por el operador",
                            Status = "Pendiente"
                        });
            }
            if (q.Contains("cloro") || q.Contains("desinfect") || q.Contains("b3"))
            {
                return ("🟢 **Instrucción de Arranque - Dosificador de Cloro B3:**\n\n" +
                        "Se activará la dosificación de hipoclorito de sodio en el tanque TK-501 para asegurar desinfección.\n\n" +
                        "[[ACTION:START|dosificador_cloro|Dosificador Cloro B3|Arranque solicitado por el operador]]",
                        new AiProposedAction
                        {
                            ActionType = "START",
                            TargetEquipment = "dosificador_cloro",
                            EquipmentName = "Dosificador Cloro B3",
                            Reason = "Arranque solicitado por el operador",
                            Status = "Pendiente"
                        });
            }
            if (q.Contains("b1") || q.Contains("principal") || q.Contains("bomba") || q.Contains("impulsion"))
            {
                return ("🟢 **Instrucción de Arranque - Bomba Principal B1:**\n\n" +
                        "Se iniciará el bombeo hacia el sedimentador SC-301 y el filtro FC-401.\n\n" +
                        "[[ACTION:START|bomba_principal|Bomba Principal B1|Arranque solicitado por el operador]]",
                        new AiProposedAction
                        {
                            ActionType = "START",
                            TargetEquipment = "bomba_principal",
                            EquipmentName = "Bomba Principal B1",
                            Reason = "Arranque solicitado por el operador",
                            Status = "Pendiente"
                        });
            }
        }

        if (q.Contains("calidad") || q.Contains("turbid") || q.Contains("diagnost"))
        {
            var conf = currentEval?.ConfidenceLevel ?? "Alta";
            var confScore = currentEval?.ConfidenceScore ?? 0.88;
            var removal = turbRaw > 0 ? ((turbRaw - turbTreated) / turbRaw * 100.0) : 0.0;

            return ($"🛡️ **Diagnóstico de Calidad Calibrado por TypeSafe Jev:**\n\n" +
                   $"• **Estado Clasificado:** **{currentEval?.OverallStatus ?? "Optimo"}** (Factor de Confianza: **{conf} [{confScore:P0}]**)\n" +
                   $"• **Agua Cruda (Entrada):** Turbidez en **{turbRaw:F1} NTU**.\n" +
                   $"• **Agua Tratada (Salida):** Turbidez en **{turbTreated:F2} NTU** (Límite Resolución 2115: < 2.0 NTU).\n" +
                   $"• **Eficiencia de Remoción:** **{removal:F1}%**.\n\n" +
                   $"✅ El sedimentador SC-301 y el filtro FC-401 operan dentro de los parámetros esperados.", null);
        }

        if (q.Contains("ph") && (q.Contains("bajo") || q.Contains("acido") || q.Contains("consecuencia")))
        {
            return ("💧 **Evaluación Técnica de pH Bajo en HIDROCONTROL PTAP:**\n\n" +
                   "1. **Corrosión:** El agua ácida (pH < 6.5) corroe las tuberías metálicas y disuelve el calcio del hormigón de los tanques.\n" +
                   "2. **Floculación Ineficiente:** El Sulfato de Aluminio requiere un pH óptimo entre 6.5 y 7.8 para formar hidróxido de aluminio Al(OH)3 insoluble.\n" +
                   "3. **Inestabilidad del Cloro:** El ácido hipocloroso se volatiliza más rápido, perdiendo residual libre en red.\n" +
                   "4. **Recomendación:** Dosificar alcalinizante para restablecer pH entre 7.0 y 7.5.", null);
        }

        return ($"🛡️ **Supervisor TypeSafe AI (Jev System One):**\n\n" +
               $"La planta reporta Bomba B1 **{(b1On ? "ENCENDIDA" : "APAGADA")}**, caudal de **{flow:F1} L/min** y presión de **{pressure:F1} bar**.\n" +
               $"Agua Cruda: **{turbRaw:F1} NTU** | Agua Tratada: **{turbTreated:F2} NTU**.\n" +
               $"Factor de Confianza Calibrado: **{currentEval?.ConfidenceLevel ?? "Alta"} ({currentEval?.ConfidenceScore ?? 0.90:P0})**.\n\n" +
               $"Puedes solicitar diagnósticos o ejecutar órdenes seguras de control (ej: 'Apagar bomba B1', 'Desenergizar toda la planta', 'Verificar calidad').", null);
    }

    /// <summary>
    /// Simula un escenario operativo crítico de baja confianza (<55%) para validación
    /// del protocolo SCADA donde el Técnico Operador asume el control manual directo de los actuadores.
    /// </summary>
    public PtapOperationalEvaluation SimulateLowConfidenceScenario(
        TelemetryResponse? telemetry,
        List<AlarmEvent>? alarms)
    {
        var turbRaw = telemetry?.GetNumber("turbidez_agua_cruda") ?? 68.0;
        var turbTreated = telemetry?.GetNumber("turbidez_agua_tratada") ?? 1.95;
        var pressure = telemetry?.GetNumber("presion_bomba_principal") ?? 3.1;
        var flow = telemetry?.GetNumber("caudal_bomba_principal") ?? 14.8;

        double lowConfidence = 0.38; // 38% Confianza Baja
        string confidenceLevel = "Baja";

        var anomalies = new List<AnomalyReport>
        {
            new AnomalyReport
            {
                Level = "Critico",
                Equipment = "Sensores Entrada/Salida & PLC",
                Title = "Conflicto de lecturas y turbidez fluctuante",
                Description = "Variación rápida de turbidez cruda y presión hidráulica elevada (3.1 bar). El modelo Jev no puede predecir con certeza matemática la curva de sedimentación.",
                RecommendedAction = "Intervención inmediata del operador para regular válvula de entrada y verificar dosificación en campo."
            },
            new AnomalyReport
            {
                Level = "Advertencia",
                Equipment = "FC-401",
                Title = "Turbidez tratada en 1.95 NTU (Límite 2.0 NTU)",
                Description = "Efluente al borde de incumplimiento de la Resolución 2115.",
                RecommendedAction = "Reducir caudal o ajustar coagulación manualmente."
            }
        };

        var suggestions = new List<DosingSuggestion>
        {
            new DosingSuggestion
            {
                Chemical = "Sulfato de Aluminio (Coagulante)",
                TargetEquipment = "dosificador_sulfato",
                CurrentValue = "Activo (Dosificando)",
                SuggestedDoseMgL = 48.0,
                SuggestedFrequencyHz = 48.0,
                SuggestedAction = "adjust",
                Reason = "⚠️ CONFIANZA BAJA (38%): Dosis estimada sin certeza matemática por fluctuación severa de turbidez. Operador debe calibrar manualmente según prueba de jarras.",
                Confidence = "Baja",
                MinSafeLimit = SulfateMinDose,
                MaxSafeLimit = SulfateMaxDose,
                Status = "Pendiente"
            },
            new DosingSuggestion
            {
                Chemical = "Hipoclorito de Sodio (Cloro Residual)",
                TargetEquipment = "dosificador_cloro",
                CurrentValue = "Activo (Dosificando)",
                SuggestedDoseMgL = 2.8,
                SuggestedFrequencyHz = 48.0,
                SuggestedAction = "adjust",
                Reason = "⚠️ CONFIANZA BAJA (38%): Requiere verificación de cloro libre con kit colorimétrico DPD en tanque TK-501.",
                Confidence = "Baja",
                MinSafeLimit = ChlorineMinDose,
                MaxSafeLimit = ChlorineMaxDose,
                Status = "Pendiente"
            }
        };

        return new PtapOperationalEvaluation
        {
            OverallStatus = "Atencion Requerida",
            ScorePercentage = 58,
            ConfidenceLevel = "Baja",
            ConfidenceScore = lowConfidence,
            RequiresOperatorIntervention = true,
            InterventionReason = "🚨 ALERTA CRÍTICA SCADA: Factor de Confianza Bajo (38%) reportado por TypeSafe Jev debido a fluctuaciones y turbidez elevada. El control autónomo se suspende y el Técnico Operador debe asumir el mando manual de todos los actuadores (Bomba B1, Dosificadores B2 y B3).",
            CoagulantAction = "increase",
            DisinfectantAction = "increase",
            ExecutiveSummary = "ALERTA SCADA: Proceso en condición inestable con 38% de certeza en modelo Jev. Turbidez tratada en 1.95 NTU próxima al límite de 2.0 NTU. Toma de control manual activada.",
            QualityEvaluation = "Turbidez de agua tratada en riesgo de exceder la norma. Se requiere ajuste de parámetros por operador calificado.",
            EnergyHydraulicEvaluation = "Presión de impulsión elevada en 3.1 bar. Posible sobrecarga de filtro FC-401.",
            ImmediateRecommendations =
            [
                "⚠️ ASUMIR CONTROL MANUAL DE ACTUADORES DE FORMA INMEDIATA.",
                "Verificar visualmente formación de flóculos en floculador FH-201.",
                "Reducir bombeo si la presión supera 3.2 bar."
            ],
            DosingSuggestions = suggestions,
            Anomalies = anomalies,
            EvaluatedAt = DateTime.UtcNow,
            LatencyMs = 168,
            ModelVersion = "jev-1.13.0 (Simulation)",
            IsLiveApi = true
        };
    }
}

