namespace PTAPControl.Models;

public sealed class CommandResponse
{
    public bool Accepted { get; set; }
    public int? Sequence { get; set; }
    public string? Target { get; set; }
    public string? Command { get; set; }
    public string? Variable { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
