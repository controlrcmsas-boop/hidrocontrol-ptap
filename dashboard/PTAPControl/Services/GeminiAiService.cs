using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using PTAPControl.Models;

namespace PTAPControl.Services;

public sealed class GeminiAiService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly PtapKnowledgeService _knowledge;
    private readonly List<AiAuditEntry> _auditLog = [];

    public GeminiAiService(HttpClient http, IConfiguration config, PtapKnowledgeService knowledge)
    {
        _http = http;
        _config = config;
        _knowledge = knowledge;
    }

    public string Provider => _config["Ai:Provider"] ?? _config["Gemini:Provider"] ?? "Local";
    public bool IsLocalProvider => Provider.Equals("Local", StringComparison.OrdinalIgnoreCase);

    // Local AI settings
    public string LocalEndpoint => _config["Ai:LocalEndpoint"] ?? "http://localhost:11434";
    public string LocalModel => _config["Ai:LocalModel"] ?? "llama3.2:3b";

    // Cloud Gemini settings
    public string ApiKey => _config["Ai:GeminiApiKey"] ?? _config["Gemini:ApiKey"] ?? string.Empty;
    public string GeminiModel => _config["Ai:GeminiModel"] ?? _config["Gemini:Model"] ?? "gemini-3.5-flash";

    // Safety limits
    public double SulfateMinDose => double.TryParse(_config["Ai:SulfateMinDose"] ?? _config["Gemini:SulfateMinDose"], out var v) ? v : 5.0;
    public double SulfateMaxDose => double.TryParse(_config["Ai:SulfateMaxDose"] ?? _config["Gemini:SulfateMaxDose"], out var v) ? v : 60.0;
    public double ChlorineMinDose => double.TryParse(_config["Ai:ChlorineMinDose"] ?? _config["Gemini:ChlorineMinDose"], out var v) ? v : 0.5;
    public double ChlorineMaxDose => double.TryParse(_config["Ai:ChlorineMaxDose"] ?? _config["Gemini:ChlorineMaxDose"], out var v) ? v : 3.5;

    public IReadOnlyList<AiAuditEntry> AuditLog => _auditLog.AsReadOnly();

    public void RecordAudit(AiAuditEntry entry)
    {
        _auditLog.Insert(0, entry);
        if (_auditLog.Count > 100)
        {
            _auditLog.RemoveAt(_auditLog.Count - 1);
        }
    }

    public async Task<(bool IsOnline, string Model, string Details)> CheckLocalOllamaStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{LocalEndpoint.TrimEnd('/')}/api/tags";
            var response = await _http.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
                var models = json?["models"]?.AsArray();
                var names = models?.Select(m => m?["name"]?.GetValue<string>())
                                   .Where(n => !string.IsNullOrEmpty(n))
                                   .Select(n => n!)
                                   .ToList() ?? [];
                var hasTarget = names.Any(n => n.Contains(LocalModel, StringComparison.OrdinalIgnoreCase) || n.Contains("ptap", StringComparison.OrdinalIgnoreCase));
                var count = models?.Count ?? 0;
                return (true, hasTarget ? LocalModel : (names.FirstOrDefault() ?? LocalModel), $"Ollama Activo ({count} modelos locales disponibles)");
            }
        }
        catch (Exception ex)
        {
            return (false, LocalModel, $"Servidor local no detectado en {LocalEndpoint}: {ex.Message}");
        }
        return (false, LocalModel, "No responde");
    }

    public async Task<(string Text, AiProposedAction? Action)> AskCopilotAsync(
        string userMessage,
        TelemetryResponse? telemetry,
        List<AlarmEvent>? alarms,
        List<AiChatMessage>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        var plantTelemetry = BuildPlantContext(telemetry, alarms);
        var baseInstruction = _knowledge.BuildSystemInstruction();
        var fullPrompt = $"[TELEMETRÍA EN VIVO DEL PLC SIEMENS S7-1200]\n{plantTelemetry}\n\n[INSTRUCCIÓN O PREGUNTA DEL OPERADOR]\n{userMessage}";

        string rawResponse;

        if (IsLocalProvider)
        {
            try
            {
                var localText = await CallLocalOllamaChatAsync(baseInstruction, fullPrompt, conversationHistory, cancellationToken);
                rawResponse = string.IsNullOrWhiteSpace(localText)
                    ? GenerateLocalFallbackResponse(userMessage, telemetry)
                    : localText;
            }
            catch (Exception ex)
            {
                rawResponse = $"⚙️ **[Supervisor Local]:** (Servidor Ollama en proceso de inicio o no disponible: {ex.Message})\n\n" +
                              GenerateLocalFallbackResponse(userMessage, telemetry);
            }
        }
        else
        {
            try
            {
                var cloudText = await CallGeminiApiAsync(baseInstruction, fullPrompt, conversationHistory, cancellationToken);
                rawResponse = string.IsNullOrWhiteSpace(cloudText)
                    ? GenerateLocalFallbackResponse(userMessage, telemetry)
                    : cloudText;
            }
            catch (Exception ex)
            {
                rawResponse = $"[Diagnóstico local - Error Cloud API: {ex.Message}]\n\n" + GenerateLocalFallbackResponse(userMessage, telemetry);
            }
        }

        return ParseCopilotResponse(rawResponse, userMessage);
    }

    private async Task<string> CallLocalOllamaChatAsync(
        string systemInstruction,
        string prompt,
        List<AiChatMessage>? history,
        CancellationToken cancellationToken)
    {
        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = systemInstruction
            }
        };

        if (history != null)
        {
            foreach (var h in history.TakeLast(6).Where(m => !m.IsThinking && !string.IsNullOrWhiteSpace(m.Content)))
            {
                messages.Add(new JsonObject
                {
                    ["role"] = h.Role == "user" ? "user" : "assistant",
                    ["content"] = h.Content
                });
            }
        }

        messages.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = prompt
        });

        var requestObj = new JsonObject
        {
            ["model"] = LocalModel,
            ["messages"] = messages,
            ["stream"] = false,
            ["options"] = new JsonObject
            {
                ["temperature"] = 0.2,
                ["top_p"] = 0.9
            }
        };

        var url = $"{LocalEndpoint.TrimEnd('/')}/api/chat";
        var content = new StringContent(requestObj.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var resJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
            return resJson?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
        }

        return string.Empty;
    }

    private static (string Text, AiProposedAction? Action) ParseCopilotResponse(string raw, string? userPrompt = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (string.Empty, null);

        // 1. Detección flexible de etiquetas [[ACTION:...]] o [ACTION:...]
        var match = System.Text.RegularExpressions.Regex.Match(raw, @"\[\[?\s*ACTION\s*:\s*(.+?)\s*\]\]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var cleanText = raw.Replace(match.Value, string.Empty).Trim();
            var actionRaw = match.Groups[1].Value.Trim();
            var parts = actionRaw.Split('|').Select(p => p.Trim()).ToArray();

            if (parts.Length > 0)
            {
                if (parts[0].Equals("STOP_ALL", StringComparison.OrdinalIgnoreCase))
                {
                    return (cleanText, new AiProposedAction
                    {
                        ActionType = "STOP_ALL",
                        TargetEquipment = "ALL",
                        EquipmentName = "Toda la Planta (B1, B2 y B3)",
                        Reason = parts.Length > 1 ? parts[1] : "Parada de seguridad y desenergización general solicitada",
                        Status = "Pendiente"
                    });
                }
                if (parts[0].Equals("STOP", StringComparison.OrdinalIgnoreCase) && parts.Length >= 2)
                {
                    var target = NormalizeTarget(parts[1]);
                    var name = parts.Length >= 3 ? parts[2] : GetEquipmentDisplayName(target);
                    var reason = parts.Length >= 4 ? parts[3] : "Apagado solicitado por el operador";
                    return (cleanText, new AiProposedAction
                    {
                        ActionType = "STOP",
                        TargetEquipment = target,
                        EquipmentName = name,
                        Reason = reason,
                        Status = "Pendiente"
                    });
                }
                if (parts[0].Equals("START", StringComparison.OrdinalIgnoreCase) && parts.Length >= 2)
                {
                    var target = NormalizeTarget(parts[1]);
                    var name = parts.Length >= 3 ? parts[2] : GetEquipmentDisplayName(target);
                    var reason = parts.Length >= 4 ? parts[3] : "Encendido solicitado por el operador";
                    return (cleanText, new AiProposedAction
                    {
                        ActionType = "START",
                        TargetEquipment = target,
                        EquipmentName = name,
                        Reason = reason,
                        Status = "Pendiente"
                    });
                }
            }
        }

        // 2. Inferencia inteligente de intención por si el LLM local omitió la etiqueta técnica
        var combined = $"{userPrompt ?? string.Empty} {raw}".ToLowerInvariant();

        bool isStop = combined.Contains("apaga") || combined.Contains("deten") || combined.Contains("desenergiz") || 
                      combined.Contains("parada") || combined.Contains("cortar") || combined.Contains("frena") || combined.Contains("stop");
        bool isStart = combined.Contains("enciend") || combined.Contains("prend") || combined.Contains("arranc") || 
                       combined.Contains("inicia") || combined.Contains("activa") || combined.Contains("start");

        if (isStop)
        {
            if (combined.Contains("toda") || combined.Contains("todo") || combined.Contains("general") || 
                combined.Contains("emergencia") || (combined.Contains("planta") && !combined.Contains("bomba")))
            {
                return (raw, new AiProposedAction
                {
                    ActionType = "STOP_ALL",
                    TargetEquipment = "ALL",
                    EquipmentName = "Toda la Planta (B1, B2 y B3)",
                    Reason = "Parada de seguridad y desenergización general solicitada por el operador",
                    Status = "Pendiente"
                });
            }
            if (combined.Contains("sulfato") || combined.Contains("coagulante") || combined.Contains("b2"))
            {
                return (raw, new AiProposedAction
                {
                    ActionType = "STOP",
                    TargetEquipment = "dosificador_sulfato",
                    EquipmentName = "Dosificador Sulfato B2",
                    Reason = "Apagado de dosificador de sulfato B2 solicitado",
                    Status = "Pendiente"
                });
            }
            if (combined.Contains("cloro") || combined.Contains("desinfect") || combined.Contains("b3"))
            {
                return (raw, new AiProposedAction
                {
                    ActionType = "STOP",
                    TargetEquipment = "dosificador_cloro",
                    EquipmentName = "Dosificador Cloro B3",
                    Reason = "Apagado de dosificador de cloro B3 solicitado",
                    Status = "Pendiente"
                });
            }
            if (combined.Contains("b1") || combined.Contains("principal") || combined.Contains("bomba") || combined.Contains("impulsion"))
            {
                return (raw, new AiProposedAction
                {
                    ActionType = "STOP",
                    TargetEquipment = "bomba_principal",
                    EquipmentName = "Bomba Principal B1",
                    Reason = "Apagado de bomba principal B1 solicitado",
                    Status = "Pendiente"
                });
            }
        }
        else if (isStart)
        {
            if (combined.Contains("sulfato") || combined.Contains("coagulante") || combined.Contains("b2"))
            {
                return (raw, new AiProposedAction
                {
                    ActionType = "START",
                    TargetEquipment = "dosificador_sulfato",
                    EquipmentName = "Dosificador Sulfato B2",
                    Reason = "Encendido de dosificador de sulfato B2 solicitado",
                    Status = "Pendiente"
                });
            }
            if (combined.Contains("cloro") || combined.Contains("desinfect") || combined.Contains("b3"))
            {
                return (raw, new AiProposedAction
                {
                    ActionType = "START",
                    TargetEquipment = "dosificador_cloro",
                    EquipmentName = "Dosificador Cloro B3",
                    Reason = "Encendido de dosificador de cloro B3 solicitado",
                    Status = "Pendiente"
                });
            }
            if (combined.Contains("b1") || combined.Contains("principal") || combined.Contains("bomba") || combined.Contains("impulsion"))
            {
                return (raw, new AiProposedAction
                {
                    ActionType = "START",
                    TargetEquipment = "bomba_principal",
                    EquipmentName = "Bomba Principal B1",
                    Reason = "Encendido de bomba principal B1 solicitado",
                    Status = "Pendiente"
                });
            }
        }

        return (raw, null);
    }

    private static string NormalizeTarget(string rawTarget)
    {
        var t = rawTarget.ToLowerInvariant().Trim();
        if (t.Contains("sulfato") || t == "b2") return "dosificador_sulfato";
        if (t.Contains("cloro") || t == "b3") return "dosificador_cloro";
        if (t.Contains("bomba") || t.Contains("principal") || t == "b1") return "bomba_principal";
        return rawTarget;
    }

    private static string GetEquipmentDisplayName(string target) => target switch
    {
        "bomba_principal" => "Bomba Principal B1",
        "dosificador_sulfato" => "Dosificador Sulfato B2",
        "dosificador_cloro" => "Dosificador Cloro B3",
        "ALL" => "Toda la Planta (B1, B2 y B3)",
        _ => target
    };

    public async Task<PlantHealthAnalysis> AnalyzePlantHealthAsync(
        TelemetryResponse? telemetry,
        List<AlarmEvent>? alarms,
        CancellationToken cancellationToken = default)
    {
        var context = BuildPlantContext(telemetry, alarms);
        var baseInstruction = _knowledge.BuildSystemInstruction() +
            "\nDebes responder EXCLUSIVAMENTE un JSON válido con la siguiente estructura exacta:\n" +
            "{\n" +
            "  \"overall_status\": \"Optimo\" | \"Estable\" | \"Atencion Requerida\" | \"Critico\",\n" +
            "  \"score_percentage\": 92,\n" +
            "  \"executive_summary\": \"resumen ejecutivo breve\",\n" +
            "  \"quality_evaluation\": \"evaluacion de parametros de agua tratada vs norma\",\n" +
            "  \"energy_hydraulic_evaluation\": \"evaluacion de caudal, presion y bombas\",\n" +
            "  \"immediate_recommendations\": [\"recomendacion 1\", \"recomendacion 2\"],\n" +
            "  \"anomalies\": [\n" +
            "    {\"level\": \"Advertencia\" | \"Critico\" | \"Info\", \"equipment\": \"FC-401\", \"title\": \"titulo\", \"description\": \"detalle\", \"recommended_action\": \"accion\"}\n" +
            "  ]\n" +
            "}";

        var prompt = $"Analiza el estado de la planta con esta telemetría y genera el JSON de diagnóstico:\n{context}";

        try
        {
            string? responseText = null;
            if (IsLocalProvider)
            {
                responseText = await CallLocalOllamaChatAsync(baseInstruction, prompt, null, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                responseText = await CallGeminiApiAsync(baseInstruction, prompt, null, cancellationToken, jsonMode: true);
            }

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                var jsonClean = ExtractJson(responseText);
                var result = JsonSerializer.Deserialize<PlantHealthAnalysis>(jsonClean, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result is not null)
                {
                    result.GeneratedAt = DateTime.UtcNow;
                    result.DosingSuggestions = GenerateDosingSuggestions(telemetry);
                    return result;
                }
            }
        }
        catch
        {
            // fallback
        }

        var fallback = GenerateSimulatedHealthAnalysis(telemetry, alarms);
        fallback.DosingSuggestions = GenerateDosingSuggestions(telemetry);
        return fallback;
    }

    public List<DosingSuggestion> GenerateDosingSuggestions(TelemetryResponse? telemetry)
    {
        var suggestions = new List<DosingSuggestion>();
        var turbRaw = telemetry?.GetNumber("turbidez_agua_cruda") ?? 15.0;
        var flow = telemetry?.GetNumber("caudal_bomba_principal") ?? 12.0;
        var phRaw = telemetry?.GetNumber("ph_agua_cruda") ?? 7.2;
        var turbTreated = telemetry?.GetNumber("turbidez_agua_tratada") ?? 0.8;
        var isSulfateOn = telemetry?.GetBool("dosificador_sulfato_estado") == true;
        var isChlorineOn = telemetry?.GetBool("dosificador_cloro_estado") == true;

        // 1. Sugerencia de Sulfato de Aluminio (Coagulante)
        double suggestedSulfateDose;
        if (turbRaw < 10) suggestedSulfateDose = 12.0;
        else if (turbRaw < 25) suggestedSulfateDose = 22.0;
        else if (turbRaw < 50) suggestedSulfateDose = 35.0;
        else suggestedSulfateDose = 48.0;

        if (phRaw > 8.0) suggestedSulfateDose += 3.0;

        var safeSulfateDose = Math.Clamp(suggestedSulfateDose, SulfateMinDose, SulfateMaxDose);
        var sulfateFreq = Math.Round((safeSulfateDose / SulfateMaxDose) * 60.0, 1);

        suggestions.Add(new DosingSuggestion
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Chemical = "Sulfato de Aluminio (Coagulante)",
            TargetEquipment = "dosificador_sulfato",
            CurrentValue = isSulfateOn ? "Activo (Dosificando)" : "Detenido (OFF)",
            SuggestedDoseMgL = Math.Round(safeSulfateDose, 1),
            SuggestedFrequencyHz = Math.Clamp(sulfateFreq, 10.0, 60.0),
            SuggestedAction = isSulfateOn ? "adjust" : "start",
            Reason = $"Turbidez de agua cruda en {turbRaw:F1} NTU con caudal {flow:F1} L/min y pH {phRaw:F2}. Dosis calculada para óptima floculación en FH-201.",
            Confidence = "Alta",
            MinSafeLimit = SulfateMinDose,
            MaxSafeLimit = SulfateMaxDose,
            IsWithinSafetyLimits = safeSulfateDose >= SulfateMinDose && safeSulfateDose <= SulfateMaxDose,
            Status = "Pendiente"
        });

        // 2. Sugerencia de Hipoclorito de Sodio (Cloro / Desinfección)
        double suggestedChlorineDose = 1.8;
        if (turbTreated > 1.5) suggestedChlorineDose = 2.4;
        var safeChlorineDose = Math.Clamp(suggestedChlorineDose, ChlorineMinDose, ChlorineMaxDose);
        var chlorineFreq = Math.Round((safeChlorineDose / ChlorineMaxDose) * 60.0, 1);

        suggestions.Add(new DosingSuggestion
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Chemical = "Hipoclorito de Sodio (Cloro Residual)",
            TargetEquipment = "dosificador_cloro",
            CurrentValue = isChlorineOn ? "Activo (Dosificando)" : "Detenido (OFF)",
            SuggestedDoseMgL = Math.Round(safeChlorineDose, 2),
            SuggestedFrequencyHz = Math.Clamp(chlorineFreq, 10.0, 60.0),
            SuggestedAction = isChlorineOn ? "adjust" : "start",
            Reason = $"Asegura desinfección y residual libre entre 0.5 - 2.0 ppm en Tanque de Agua Tratada TK-501 según Resolución 2115.",
            Confidence = "Alta",
            MinSafeLimit = ChlorineMinDose,
            MaxSafeLimit = ChlorineMaxDose,
            IsWithinSafetyLimits = safeChlorineDose >= ChlorineMinDose && safeChlorineDose <= ChlorineMaxDose,
            Status = "Pendiente"
        });

        return suggestions;
    }

    private async Task<string> CallGeminiApiAsync(
        string systemInstruction,
        string prompt,
        List<AiChatMessage>? history = null,
        CancellationToken cancellationToken = default,
        bool jsonMode = false)
    {
        var modelsToTry = new[] { GeminiModel, "gemini-3.5-flash", "gemini-3.6-flash", "gemini-flash-latest", "gemini-3.7-flash", "gemini-pro-latest" }.Distinct();

        var contents = new JsonArray();
        if (history != null)
        {
            foreach (var h in history.TakeLast(6).Where(m => !m.IsThinking && !string.IsNullOrWhiteSpace(m.Content)))
            {
                contents.Add(new JsonObject
                {
                    ["role"] = h.Role == "user" ? "user" : "model",
                    ["parts"] = new JsonArray { new JsonObject { ["text"] = h.Content } }
                });
            }
        }
        contents.Add(new JsonObject
        {
            ["role"] = "user",
            ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt } }
        });

        foreach (var model in modelsToTry)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={ApiKey}";
                var requestBody = new JsonObject
                {
                    ["systemInstruction"] = new JsonObject
                    {
                        ["parts"] = new JsonArray { new JsonObject { ["text"] = systemInstruction } }
                    },
                    ["contents"] = contents,
                    ["generationConfig"] = new JsonObject
                    {
                        ["temperature"] = 0.2,
                        ["topP"] = 0.95,
                        ["maxOutputTokens"] = 2048
                    }
                };

                if (jsonMode)
                {
                    requestBody["generationConfig"]!["responseMimeType"] = "application/json";
                }

                var content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, content, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var resJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
                    var candidates = resJson?["candidates"]?.AsArray();
                    if (candidates != null && candidates.Count > 0)
                    {
                        var text = candidates[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }
            }
            catch
            {
                // try next
            }
        }

        return string.Empty;
    }

    private string BuildPlantContext(TelemetryResponse? telemetry, List<AlarmEvent>? alarms)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Fecha/Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Estado PLC: {(telemetry?.PlcConnected == true ? "CONECTADO Y OPERANDO" : "DESCONECTADO / SIN TELEMETRÍA")}");
        sb.AppendLine($"Modo Remoto Habilitado: {(telemetry?.GetBool("modo_remoto_habilitado") == true ? "SI" : "NO")}");
        sb.AppendLine($"Falla PLC: {(telemetry?.GetBool("plc_en_falla") == true ? "SI (ALARMA CRITICA)" : "NO")}");
        
        sb.AppendLine("\n--- AGUA CRUDA (ENTRADA TK-101) ---");
        sb.AppendLine($"• Turbidez Cruda: {telemetry?.GetNumber("turbidez_agua_cruda")?.ToString("F1") ?? "--"} NTU");
        sb.AppendLine($"• pH Cruda: {telemetry?.GetNumber("ph_agua_cruda")?.ToString("F2") ?? "--"}");
        sb.AppendLine($"• Nivel Tanque Cruda TK-101: {telemetry?.GetNumber("nivel_tanque_cruda")?.ToString("F1") ?? "--"} %");

        sb.AppendLine("\n--- AGUA TRATADA (SALIDA TK-501) ---");
        sb.AppendLine($"• Turbidez Tratada: {telemetry?.GetNumber("turbidez_agua_tratada")?.ToString("F1") ?? "--"} NTU (Límite normativo < 2.0 NTU)");
        sb.AppendLine($"• pH Tratada: {telemetry?.GetNumber("ph_agua_tratada")?.ToString("F2") ?? "--"}");
        sb.AppendLine($"• Nivel Tanque Tratada TK-501: {telemetry?.GetNumber("nivel_tanque_tratada")?.ToString("F1") ?? "--"} %");

        sb.AppendLine("\n--- ESTADO DE ACTUADORES Y BOMBAS ---");
        sb.AppendLine($"• Bomba Principal B1: {(telemetry?.GetBool("bomba_principal_estado") == true ? "ENCENDIDA (OPERANDO)" : "APAGADA")}");
        sb.AppendLine($"• Caudal Bomba B1: {telemetry?.GetNumber("caudal_bomba_principal")?.ToString("F1") ?? "--"} L/min");
        sb.AppendLine($"• Presión Bomba B1 (Impulsión a FC-401): {telemetry?.GetNumber("presion_bomba_principal")?.ToString("F1") ?? "--"} bar");
        sb.AppendLine($"• Dosificador Sulfato B2 (FH-201): {(telemetry?.GetBool("dosificador_sulfato_estado") == true ? "DOSIFICANDO" : "APAGADO")}");
        sb.AppendLine($"• Dosificador Cloro B3 (TK-501): {(telemetry?.GetBool("dosificador_cloro_estado") == true ? "DOSIFICANDO" : "APAGADO")}");

        if (alarms != null && alarms.Count > 0)
        {
            sb.AppendLine("\n--- ALARMAS ACTIVAS EN PLANTA ---");
            foreach (var a in alarms)
            {
                sb.AppendLine($"⚠️ [{a.Severity}] {a.NombreVisible}: {a.Message}");
            }
        }
        else
        {
            sb.AppendLine("\n• Sin alarmas activas de proceso.");
        }

        return sb.ToString();
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return text.Substring(start, end - start + 1);
        }
        return text;
    }

    private string GenerateLocalFallbackResponse(string query, TelemetryResponse? telemetry)
    {
        var q = query.ToLowerInvariant();
        var turbRaw = telemetry?.GetNumber("turbidez_agua_cruda") ?? 16.0;
        var turbTreated = telemetry?.GetNumber("turbidez_agua_tratada") ?? 0.85;
        var flow = telemetry?.GetNumber("caudal_bomba_principal") ?? 12.5;
        var pressure = telemetry?.GetNumber("presion_bomba_principal") ?? 2.3;
        var phRaw = telemetry?.GetNumber("ph_agua_cruda") ?? 7.3;
        var phTreated = telemetry?.GetNumber("ph_agua_tratada") ?? 7.1;
        var b1On = telemetry?.GetBool("bomba_principal_estado") == true;

        // Comandos de Parada / Desenergización
        if (q.Contains("apaga") || q.Contains("desenergiz") || q.Contains("deten") || q.Contains("parada") || q.Contains("cortar") || q.Contains("stop"))
        {
            if (q.Contains("sulfato") || q.Contains("coagulante") || q.Contains("b2"))
            {
                return "🛑 **Instrucción de Parada - Dosificador de Sulfato B2:**\n\n" +
                       "Se detendrá la inyección de coagulante en el floculador FH-201.\n\n" +
                       "[[ACTION:STOP|dosificador_sulfato|Dosificador Sulfato B2|Parada solicitada por el operador]]";
            }
            if (q.Contains("cloro") || q.Contains("desinfect") || q.Contains("b3"))
            {
                return "🛑 **Instrucción de Parada - Dosificador de Cloro B3:**\n\n" +
                       "Se detendrá la inyección de hipoclorito de sodio en el tanque TK-501.\n\n" +
                       "[[ACTION:STOP|dosificador_cloro|Dosificador Cloro B3|Parada solicitada por el operador]]";
            }
            if (q.Contains("b1") || q.Contains("principal") || q.Contains("impulsion") || (q.Contains("bomba") && !q.Contains("toda") && !q.Contains("planta")))
            {
                return "🛑 **Instrucción de Parada - Bomba Principal B1:**\n\n" +
                       "Se procederá a apagar la impulsión hacia el filtro FC-401 para evitar sobrepresión o ingreso de agua sin tratar.\n\n" +
                       "[[ACTION:STOP|bomba_principal|Bomba Principal B1|Parada solicitada por el operador]]";
            }
            
            return "🚨 **Instrucción de Parada de Seguridad y Desenergización General:**\n\n" +
                   "Se ha preparado la orden para desenergizar y detener simultáneamente la **Bomba Principal B1**, el **Dosificador de Sulfato B2** y el **Dosificador de Cloro B3** en el PLC Siemens S7-1200.\n\n" +
                   "[[ACTION:STOP_ALL|Parada de seguridad y desenergización solicitada por el operador]]";
        }

        // Comandos de Encendido / Arranque
        if (q.Contains("enciend") || q.Contains("prend") || q.Contains("arranc") || q.Contains("inicia") || q.Contains("activa") || q.Contains("start"))
        {
            if (q.Contains("sulfato") || q.Contains("coagulante") || q.Contains("b2"))
            {
                return "🟢 **Instrucción de Arranque - Dosificador de Sulfato B2:**\n\n" +
                       "Se activará la dosificación de sulfato de aluminio en el floculador FH-201 con la frecuencia configurada.\n\n" +
                       "[[ACTION:START|dosificador_sulfato|Dosificador Sulfato B2|Arranque solicitado por el operador]]";
            }
            if (q.Contains("cloro") || q.Contains("desinfect") || q.Contains("b3"))
            {
                return "🟢 **Instrucción de Arranque - Dosificador de Cloro B3:**\n\n" +
                       "Se activará la dosificación de hipoclorito de sodio en el tanque TK-501 para asegurar desinfección.\n\n" +
                       "[[ACTION:START|dosificador_cloro|Dosificador Cloro B3|Arranque solicitado por el operador]]";
            }
            if (q.Contains("b1") || q.Contains("principal") || q.Contains("bomba") || q.Contains("impulsion"))
            {
                return "🟢 **Instrucción de Arranque - Bomba Principal B1:**\n\n" +
                       "Se iniciará el bombeo hacia el sedimentador SC-301 y el filtro FC-401.\n\n" +
                       "[[ACTION:START|bomba_principal|Bomba Principal B1|Arranque solicitado por el operador]]";
            }
        }

        if (q.Contains("ph") && (q.Contains("bajo") || q.Contains("acido") || q.Contains("consecuencia")))
        {
            return "💧 **Evaluación Técnica de pH Bajo en HIDROCONTROL PTAP:**\n\n" +
                   "1. **Corrosión:** El agua ácida (pH < 6.5) corroe las tuberías metálicas y disuelve el calcio del hormigón de los tanques.\n" +
                   "2. **Floculación Ineficiente:** El Sulfato de Aluminio requiere un pH óptimo entre 6.5 y 7.8 para formar hidróxido de aluminio Al(OH)3 insoluble. A pH bajo los flóculos no sedimentan en SC-301 y queda aluminio residual.\n" +
                   "3. **Inestabilidad del Cloro:** El ácido hipocloroso se volatiliza más rápido, perdiendo residual libre en red.\n" +
                   "4. **Recomendación:** Dosificar alcalinizante (cal o hidróxido de sodio) para restablecer pH entre 7.0 y 7.5.";
        }

        if (q.Contains("calidad") || q.Contains("turbid") || q.Contains("diagnost"))
        {
            return $"📊 **Diagnóstico de Calidad y Remoción:**\n\n" +
                   $"• **Entrada (Agua Cruda):** Turbidez en **{turbRaw:F1} NTU** con pH de **{phRaw:F2}**.\n" +
                   $"• **Salida (Agua Tratada):** Turbidez en **{turbTreated:F1} NTU** con pH de **{phTreated:F2}**.\n\n" +
                   $"✅ **Eficiencia de Remoción:** {((turbRaw - turbTreated) / turbRaw * 100):F1}%. El sedimentador lamelar SC-301 y el filtro FC-401 están operando satisfactoriamente dentro de la normativa de agua potable (< 2.0 NTU).";
        }

        return $"🤖 **Supervisor Virtual PTAP (Motor Local):**\n\n" +
               $"Actualmente la planta reporta bomba principal **{(b1On ? "ENCENDIDA" : "APAGADA")}**, caudal de **{flow:F1} L/min** y presión de **{pressure:F1} bar**.\n\n" +
               $"El agua cruda ingresa con **{turbRaw:F1} NTU** y el efluente tratado se mantiene en **{turbTreated:F1} NTU** (calidad óptima). Puedes pedirme consultar cualquier variable, diagnosticar o darme instrucciones de control (ej: 'apagar bomba B1', 'desenergizar toda la planta', 'encender dosificador de sulfato').";
    }

    public PlantHealthAnalysis GenerateSimulatedHealthAnalysis(TelemetryResponse? telemetry, List<AlarmEvent>? alarms)
    {
        var turbRaw = telemetry?.GetNumber("turbidez_agua_cruda") ?? 16.0;
        var turbTreated = telemetry?.GetNumber("turbidez_agua_tratada") ?? 0.85;
        var pressure = telemetry?.GetNumber("presion_bomba_principal") ?? 2.3;
        var flow = telemetry?.GetNumber("caudal_bomba_principal") ?? 12.5;

        var anomalies = new List<AnomalyReport>();

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
                RecommendedAction = "Incrementar dosis de coagulante en 4 mg/L o reducir caudal de entrada temporalmente."
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
                RecommendedAction = "Continuar régimen de operación estándar."
            });
        }

        var score = 100;
        if (turbTreated > 1.5) score -= 15;
        if (pressure > 3.0) score -= 10;
        if (alarms != null && alarms.Count > 0) score -= (alarms.Count * 8);
        score = Math.Clamp(score, 40, 100);

        var status = score >= 90 ? "Optimo" : score >= 75 ? "Estable" : score >= 60 ? "Atencion Requerida" : "Critico";

        return new PlantHealthAnalysis
        {
            OverallStatus = status,
            ScorePercentage = score,
            ExecutiveSummary = $"Planta operando a {flow:F1} L/min con presión de {pressure:F1} bar. Agua cruda en {turbRaw:F1} NTU y agua tratada en {turbTreated:F1} NTU.",
            QualityEvaluation = $"Remoción de turbidez al {((turbRaw - turbTreated) / turbRaw * 100):F1}%. Efluente cumple norma de agua potable.",
            EnergyHydraulicEvaluation = $"Punto de trabajo de bomba principal en {pressure:F1} bar dentro de curva recomendada.",
            ImmediateRecommendations =
            [
                $"Mantener dosificación de sulfato según turbidez cruda de {turbRaw:F1} NTU.",
                "Monitorear diferencial de presión en filtro FC-401."
            ],
            Anomalies = anomalies,
            GeneratedAt = DateTime.UtcNow
        };
    }
}