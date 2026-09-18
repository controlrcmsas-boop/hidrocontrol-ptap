using System.Text.Json.Serialization;

namespace PTAPControl.Models;

public sealed class NotificationWhatsApp
{
    [JsonPropertyName("id")]
    public object? RawId { get; set; }

    [JsonIgnore]
    public long Id
    {
        get
        {
            if (RawId is null) return 0;
            if (RawId is long l) return l;
            if (RawId is int i) return i;
            if (long.TryParse(RawId.ToString(), out var parsed)) return parsed;
            return 0;
        }
    }

    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("apikey")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("recorded_at")]
    public DateTime? RecordedAt { get; set; }
}
