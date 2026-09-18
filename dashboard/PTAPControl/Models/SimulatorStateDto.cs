using System.Text.Json.Serialization;

namespace PTAPControl.Models;

/// <summary>
/// Modelo de datos para el control interactivo y modificación en caliente
/// de las variables físicas, químicas y estados del simulador PTAP offline.
/// </summary>
public sealed class SimulatorStateDto
{
    [JsonPropertyName("ph_cruda_base")]
    public double PhCrudaBase { get; set; } = 7.25;

    [JsonPropertyName("turbidez_cruda_base")]
    public double TurbidezCrudaBase { get; set; } = 18.5;

    [JsonPropertyName("conductividad_cruda_base")]
    public double ConductividadCrudaBase { get; set; } = 425.0;

    [JsonPropertyName("presion_b1_base")]
    public double PresionB1Base { get; set; } = 2.45;

    [JsonPropertyName("caudal_b1_base")]
    public double CaudalB1Base { get; set; } = 15.0;

    [JsonPropertyName("bomba_principal_estado")]
    public bool BombaPrincipalEstado { get; set; } = true;

    [JsonPropertyName("dosificador_sulfato_estado")]
    public bool DosificadorSulfatoEstado { get; set; } = true;

    [JsonPropertyName("dosificador_cloro_estado")]
    public bool DosificadorCloroEstado { get; set; } = true;

    [JsonPropertyName("nivel_tanque_agua_cruda")]
    public bool NivelTanqueAguaCruda { get; set; } = true;

    [JsonPropertyName("modo_remoto_habilitado")]
    public bool ModoRemotoHabilitado { get; set; } = true;

    [JsonPropertyName("plc_en_falla")]
    public bool PlcEnFalla { get; set; } = false;

    [JsonPropertyName("scenario")]
    public string Scenario { get; set; } = "normal";
}

public sealed class SimulatorUpdateResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("state")]
    public SimulatorStateDto? State { get; set; }
}
