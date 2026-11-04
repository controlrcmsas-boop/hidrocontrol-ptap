using System.Text.Json.Serialization;

namespace PTAPControl.Models;

public sealed class TelemetryHistoryRecord
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("plc_connected")]
    public int PlcConnected { get; set; }

    [JsonPropertyName("ph_agua_cruda")]
    public double? PhAguaCruda { get; set; }

    [JsonPropertyName("turbidez_agua_cruda")]
    public double? TurbidezAguaCruda { get; set; }

    [JsonPropertyName("conductividad_agua_cruda")]
    public double? ConductividadAguaCruda { get; set; }

    [JsonPropertyName("ph_agua_tratada")]
    public double? PhAguaTratada { get; set; }

    [JsonPropertyName("turbidez_agua_tratada")]
    public double? TurbidezAguaTratada { get; set; }

    [JsonPropertyName("conductividad_agua_tratada")]
    public double? ConductividadAguaTratada { get; set; }

    [JsonPropertyName("bomba_principal")]
    public int? BombaPrincipal { get; set; }

    [JsonPropertyName("dosificador_sulfato")]
    public int? DosificadorSulfato { get; set; }

    [JsonPropertyName("dosificador_cloro")]
    public int? DosificadorCloro { get; set; }

    [JsonPropertyName("modo_operacion")]
    public string? ModoOperacion { get; set; }

    [JsonPropertyName("sincronizado")]
    public int? Sincronizado { get; set; }
}
