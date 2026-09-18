using System.Text.Json.Serialization;

namespace PTAPControl.Models;

public sealed class AlarmEvent
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tag_id")]
    public string TagId { get; set; } = string.Empty;

    [JsonPropertyName("nombre_visible")]
    public string? NombreVisible { get; set; }

    [JsonPropertyName("alarm_type")]
    public string AlarmType { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "medium";

    [JsonPropertyName("value_numeric")]
    public decimal? ValueNumeric { get; set; }

    [JsonPropertyName("value_bool")]
    public bool? ValueBool { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; set; }

    [JsonPropertyName("acknowledged_at")]
    public DateTimeOffset? AcknowledgedAt { get; set; }

    [JsonPropertyName("cleared_at")]
    public DateTimeOffset? ClearedAt { get; set; }

    [JsonPropertyName("operator_name")]
    public string? OperatorName { get; set; }
}
