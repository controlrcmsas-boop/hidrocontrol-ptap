namespace PTAPControl.Models;

public sealed class CommandRequest
{
    public string Target { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Operator { get; set; } = "operador";
    public string Source { get; set; } = "dashboard";
}
