using System.Text.Json;

namespace PTAPControl.Models;

public sealed class TelemetryResponse
{
    public DateTimeOffset Timestamp { get; set; }
    public bool PlcConnected { get; set; }
    public bool Stale { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, object?> Values { get; set; } = new();

    public bool GetBool(string key)
    {
        if (!Values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return element.GetBoolean();
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intValue))
            {
                return intValue != 0;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return bool.TryParse(element.GetString(), out var parsed) && parsed;
            }
        }

        return bool.TryParse(value.ToString(), out var result) && result;
    }

    public double? GetNumber(string key)
    {
        if (!Values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is double doubleValue)
        {
            return doubleValue;
        }

        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is decimal decimalValue)
        {
            return (double)decimalValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (value is long longValue)
        {
            return longValue;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var parsedNumber))
            {
                return parsedNumber;
            }

            if (element.ValueKind == JsonValueKind.True)
            {
                return 1;
            }

            if (element.ValueKind == JsonValueKind.False)
            {
                return 0;
            }

            if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), out var parsedString))
            {
                return parsedString;
            }
        }

        return double.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }
}
