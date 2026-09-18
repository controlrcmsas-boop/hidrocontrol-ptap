using System.Text.Json.Serialization;

namespace PTAPControl.Models;

public sealed class AiProposedAction
{
    public string ActionType { get; set; } = string.Empty; // "STOP_ALL", "STOP", "START"
    public string TargetEquipment { get; set; } = string.Empty; // "bomba_principal", "dosificador_sulfato", "dosificador_cloro", "ALL"
    public string EquipmentName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pendiente"; // "Pendiente", "Ejecutado", "Cancelado"
}

public sealed class AiChatMessage
{
    public string Role { get; set; } = "user"; // "user" or "model" / "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsThinking { get; set; }
    public AiProposedAction? Action { get; set; }
}

public sealed class DosingSuggestion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("chemical")]
    public string Chemical { get; set; } = "Sulfato de Aluminio";

    [JsonPropertyName("target_equipment")]
    public string TargetEquipment { get; set; } = "dosificador_sulfato";

    [JsonPropertyName("current_value")]
    public string CurrentValue { get; set; } = "--";

    [JsonPropertyName("suggested_dose_mg_l")]
    public double SuggestedDoseMgL { get; set; }

    [JsonPropertyName("suggested_frequency_hz")]
    public double SuggestedFrequencyHz { get; set; }

    [JsonPropertyName("suggested_action")]
    public string SuggestedAction { get; set; } = "start"; // "start", "stop", "adjust"

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = "Alta"; // "Alta", "Media", "Baja"

    [JsonPropertyName("min_safe_limit")]
    public double MinSafeLimit { get; set; } = 5.0;

    [JsonPropertyName("max_safe_limit")]
    public double MaxSafeLimit { get; set; } = 60.0;

    [JsonPropertyName("is_within_safety_limits")]
    public bool IsWithinSafetyLimits { get; set; } = true;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pendiente"; // "Pendiente", "Aprobado", "Rechazado"

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class AnomalyReport
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..6];

    [JsonPropertyName("level")]
    public string Level { get; set; } = "Advertencia"; // "Info", "Advertencia", "Critico"

    [JsonPropertyName("equipment")]
    public string Equipment { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("recommended_action")]
    public string RecommendedAction { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class PlantHealthAnalysis
{
    [JsonPropertyName("overall_status")]
    public string OverallStatus { get; set; } = "Estable"; // "Optimo", "Estable", "Atencion Requerida", "Critico"

    [JsonPropertyName("score_percentage")]
    public int ScorePercentage { get; set; } = 95;

    [JsonPropertyName("executive_summary")]
    public string ExecutiveSummary { get; set; } = string.Empty;

    [JsonPropertyName("quality_evaluation")]
    public string QualityEvaluation { get; set; } = string.Empty;

    [JsonPropertyName("energy_hydraulic_evaluation")]
    public string EnergyHydraulicEvaluation { get; set; } = string.Empty;

    [JsonPropertyName("immediate_recommendations")]
    public List<string> ImmediateRecommendations { get; set; } = [];

    [JsonPropertyName("dosing_suggestions")]
    public List<DosingSuggestion> DosingSuggestions { get; set; } = [];

    [JsonPropertyName("anomalies")]
    public List<AnomalyReport> Anomalies { get; set; } = [];

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AiAuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Chemical { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "Aprobado", "Rechazado", "Modificado"
    public string Operator { get; set; } = "Operador PTAP";
    public string Details { get; set; } = string.Empty;
    public double DoseMgL { get; set; }
    public string RawWaterConditions { get; set; } = string.Empty;
}
