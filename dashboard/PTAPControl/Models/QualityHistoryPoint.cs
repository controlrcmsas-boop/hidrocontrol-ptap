using System.Text.Json.Serialization;

namespace PTAPControl.Models;

public sealed class QualityHistoryPoint
{
    [JsonPropertyName("tag_id")]
    public string TagId { get; set; } = string.Empty;

    [JsonPropertyName("value_numeric")]
    public decimal ValueNumeric { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("recorded_at")]
    public DateTimeOffset RecordedAt { get; set; }
}
