using System.Text.Json.Serialization;

namespace PTAPControl.Models;

public sealed class AlarmLimit
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tag_id")]
    public string TagId { get; set; } = string.Empty;

    [JsonPropertyName("nombre_visible")]
    public string? NombreVisible { get; set; }

    [JsonPropertyName("alarm_type")]
    public string AlarmType { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "medium";

    [JsonPropertyName("limit_low")]
    public decimal? LimitLow { get; set; }

    [JsonPropertyName("limit_high")]
    public decimal? LimitHigh { get; set; }

    [JsonPropertyName("bool_alarm_value")]
    public bool? BoolAlarmValue { get; set; }

    [JsonIgnore]
    public bool BoolAlarmValueEdit
    {
        get => BoolAlarmValue ?? false;
        set => BoolAlarmValue = value;
    }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("technical_note")]
    public string? TechnicalNote { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
