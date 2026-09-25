using System.Text.Json.Serialization;

namespace PTAPControl.Models;

/// <summary>
/// Petición tipada hacia TypeSafe AI (Endpoint: /v1/systemone, Modelo: Jev)
/// </summary>
public sealed class TypeSafeRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "jev-latest";

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("questions")]
    public Dictionary<string, TypeSafeQuestion> Questions { get; set; } = [];
}

/// <summary>
/// Definición de pregunta tipada para Jev (choice, noul, score)
/// </summary>
public sealed class TypeSafeQuestion
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "choice"; // "choice", "noul", "score"

    [JsonPropertyName("instructions")]
    public string Instructions { get; set; } = string.Empty;

    [JsonPropertyName("criteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Criteria { get; set; }
}

/// <summary>
/// Respuesta cruda de la API de TypeSafe AI
/// </summary>
public sealed class TypeSafeResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("answers")]
    public Dictionary<string, TypeSafeAnswer> Answers { get; set; } = [];

    [JsonPropertyName("usage")]
    public TypeSafeUsage? Usage { get; set; }
}

public sealed class TypeSafeAnswer
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("choice")]
    public string? Choice { get; set; }

    [JsonPropertyName("noul")]
    public double? Noul { get; set; }

    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    [JsonPropertyName("probabilities")]
    public Dictionary<string, double>? Probabilities { get; set; }

    [JsonPropertyName("legend")]
    public Dictionary<string, string>? Legend { get; set; }
}

public sealed class TypeSafeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

/// <summary>
/// Evaluación operativa calibrada de la PTAP para toma de decisiones del SCADA
/// </summary>
public sealed class PtapOperationalEvaluation
{
    public string OverallStatus { get; set; } = "Estable"; // "Optimo", "Estable", "Atencion Requerida", "Critico"
    public int ScorePercentage { get; set; } = 95;

    /// <summary>
    /// Nivel de confianza calibrado: "Alta" (>= 0.85), "Media" (0.60 - 0.84), "Baja" (< 0.60)
    /// </summary>
    public string ConfidenceLevel { get; set; } = "Alta";

    /// <summary>
    /// Puntuación numérica calibrada de confianza (0.0 a 1.0)
    /// </summary>
    public double ConfidenceScore { get; set; } = 0.90;

    /// <summary>
    /// Bandera crítica: Indica si el operador técnico debe asumir control manual directo de los actuadores
    /// </summary>
    public bool RequiresOperatorIntervention { get; set; }

    public string InterventionReason { get; set; } = string.Empty;

    public string CoagulantAction { get; set; } = "maintain"; // "maintain", "increase", "decrease", "stop"
    public string DisinfectantAction { get; set; } = "maintain";

    public string ExecutiveSummary { get; set; } = string.Empty;
    public string QualityEvaluation { get; set; } = string.Empty;
    public string EnergyHydraulicEvaluation { get; set; } = string.Empty;
    public List<string> ImmediateRecommendations { get; set; } = [];

    public List<DosingSuggestion> DosingSuggestions { get; set; } = [];
    public List<AnomalyReport> Anomalies { get; set; } = [];

    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public long LatencyMs { get; set; }
    public string ModelVersion { get; set; } = "jev-latest";
    public bool IsLiveApi { get; set; }

    public Dictionary<string, TypeSafeAnswer>? RawAnswers { get; set; }
}
