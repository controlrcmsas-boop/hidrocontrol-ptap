using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTAPControl.Services;

public class PtapKnowledgeService
{
    private readonly string _filePath;
    private readonly ILogger<PtapKnowledgeService> _logger;
    private PtapKnowledgeData _data = new();

    public PtapKnowledgeData Data => _data;

    public PtapKnowledgeService(IHostEnvironment env, ILogger<PtapKnowledgeService> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(env.ContentRootPath, "Data", "PtapKnowledgeBase.json");
        LoadKnowledge();
    }

    public void LoadKnowledge()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _data = JsonSerializer.Deserialize<PtapKnowledgeData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new PtapKnowledgeData();
                _logger.LogInformation("Base de conocimiento de PTAP cargada exitosamente desde {Path}", _filePath);
            }
            else
            {
                _logger.LogWarning("Archivo de conocimiento no encontrado en {Path}. Inicializando vacío.", _filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar base de conocimiento de PTAP.");
        }
    }

    public async Task AddRuleAsync(string condition, string action)
    {
        if (string.IsNullOrWhiteSpace(condition) || string.IsNullOrWhiteSpace(action)) return;

        var newRule = new LearnedRule
        {
            Id = "rule-" + (Data.LearnedOperatorRules.Count + 1).ToString("D2"),
            Condition = condition.Trim(),
            Action = action.Trim()
        };

        Data.LearnedOperatorRules.Add(newRule);
        await SaveKnowledgeAsync();
    }

    public async Task RemoveRuleAsync(string id)
    {
        Data.LearnedOperatorRules.RemoveAll(r => r.Id == id);
        await SaveKnowledgeAsync();
    }

    public async Task SaveKnowledgeAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_filePath, json);
            _logger.LogInformation("Base de conocimiento guardada en {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar la base de conocimiento.");
        }
    }

    public string BuildSystemInstruction()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Eres el INGENIERO SUPERVISOR Y OPERADOR VIRTUAL de HIDROCONTROL PTAP V2.2.");
        sb.AppendLine("Estás instalado y ejecutándote 100% de forma local y offline en el computador del operador.");
        sb.AppendLine("Tu objetivo es supervisar los procesos de potabilización, diagnosticar anomalías, asesorar en dosificación química y ejecutar órdenes de control al PLC Siemens S7-1200 cuando el operador lo instruya.");
        sb.AppendLine("\n=== BASE DE CONOCIMIENTO DE INGENIERÍA DE LA PLANTA ===");
        sb.AppendLine($"Planta: {_data.PlantInfo?.Name} ({_data.PlantInfo?.Type})");
        sb.AppendLine($"Caudal de Diseño: {_data.PlantInfo?.DesignFlowNominalLpm} L/min (Rango: {_data.PlantInfo?.DesignFlowMinLpm} a {_data.PlantInfo?.DesignFlowMaxLpm} L/min)");
        sb.AppendLine($"Metas de Calidad: Turbidez Tratada < {_data.PlantInfo?.QualityTargets?.TurbidityTreatedMaxNtu} NTU (Óptimo < {_data.PlantInfo?.QualityTargets?.TurbidityTreatedOptimalNtu} NTU), pH: {_data.PlantInfo?.QualityTargets?.PhMin} - {_data.PlantInfo?.QualityTargets?.PhMax}, Cloro Libre: {_data.PlantInfo?.QualityTargets?.FreeChlorineMinMgL} - {_data.PlantInfo?.QualityTargets?.FreeChlorineMaxMgL} mg/L.");

        sb.AppendLine("\n--- FICHAS TÉCNICAS DE EQUIPOS DEL PROCESO ---");
        foreach (var eq in _data.EquipmentSpecs)
        {
            sb.AppendLine($"* [{eq.Tag}] {eq.Name} ({eq.Type}): {eq.Objective ?? eq.Description}");
            if (!string.IsNullOrEmpty(eq.OperationalNotes)) sb.AppendLine($"  Nota operativa: {eq.OperationalNotes}");
            if (!string.IsNullOrEmpty(eq.ChemicalInjected)) sb.AppendLine($"  Reactivo: {eq.ChemicalInjected}");
            if (!string.IsNullOrEmpty(eq.DoseRangeMgL)) sb.AppendLine($"  Rango Seguro: {eq.DoseRangeMgL} ({eq.FrequencyRangeHz})");
            if (!string.IsNullOrEmpty(eq.BackwashTrigger)) sb.AppendLine($"  Criterio de Retrolavado: {eq.BackwashTrigger}");
        }

        sb.AppendLine("\n--- PROCEDIMIENTOS OPERATIVOS ESTÁNDAR (SOPs) ---");
        foreach (var sop in _data.StandardOperatingProcedures)
        {
            sb.AppendLine($"* [{sop.Code}] {sop.Title}:");
            foreach (var step in sop.Steps)
            {
                sb.AppendLine($"    {step}");
            }
        }

        if (_data.LearnedOperatorRules.Count > 0)
        {
            sb.AppendLine("\n--- REGLAS OPERATIVAS CAPACITADAS POR EL OPERADOR ---");
            foreach (var rule in _data.LearnedOperatorRules)
            {
                sb.AppendLine($"* [{rule.Id}] SI {rule.Condition} -> ACCIÓN: {rule.Action}");
            }
        }

        sb.AppendLine("\n=== PROTOCOLO ESTRICTO DE CONTROL DE SALIDAS AL PLC ===");
        sb.AppendLine("Si el usuario te solicita APAGAR, DETENER o DESENERGIZAR equipos o la planta:");
        sb.AppendLine("1. Explica brevemente la maniobra técnica y su justificativo de seguridad.");
        sb.AppendLine("2. Para parada general de toda la planta emite al final de tu mensaje: [[ACTION:STOP_ALL|Todos los equipos|Parada general solicitada]]");
        sb.AppendLine("3. Para apagar la Bomba Principal B1 emite: [[ACTION:STOP|bomba_principal|Bomba Principal B1|Apagado solicitado]]");
        sb.AppendLine("4. Para apagar el Dosificador de Sulfato B2 emite: [[ACTION:STOP|dosificador_sulfato|Dosificador Sulfato B2|Apagado solicitado]]");
        sb.AppendLine("5. Para apagar el Dosificador de Cloro B3 emite: [[ACTION:STOP|dosificador_cloro|Dosificador Cloro B3|Apagado solicitado]]");
        sb.AppendLine("6. Para encender un equipo emite: [[ACTION:START|target|Nombre Equipo|Motivo]]");

        return sb.ToString();
    }
}

public class PtapKnowledgeData
{
    public PlantInfo PlantInfo { get; set; } = new();
    public List<EquipmentSpec> EquipmentSpecs { get; set; } = [];
    public List<StandardOperatingProcedure> StandardOperatingProcedures { get; set; } = [];
    public List<LearnedRule> LearnedOperatorRules { get; set; } = [];
}

public class PlantInfo
{
    public string Name { get; set; } = "HIDROCONTROL PTAP V2.2";
    public string Type { get; set; } = "PTAP Filtración Rápida";
    public double DesignFlowMinLpm { get; set; } = 5.0;
    public double DesignFlowNominalLpm { get; set; } = 15.0;
    public double DesignFlowMaxLpm { get; set; } = 30.0;
    public string TargetNorm { get; set; } = "Resolución 2115";
    public QualityTargets QualityTargets { get; set; } = new();
}

public class QualityTargets
{
    public double TurbidityTreatedMaxNtu { get; set; } = 2.0;
    public double TurbidityTreatedOptimalNtu { get; set; } = 0.5;
    public double PhMin { get; set; } = 6.5;
    public double PhMax { get; set; } = 8.5;
    public double FreeChlorineMinMgL { get; set; } = 0.5;
    public double FreeChlorineMaxMgL { get; set; } = 2.0;
    public double PressureMaxBar { get; set; } = 3.5;
}

public class EquipmentSpec
{
    public string Tag { get; set; } = string.Empty;
    public string? ControlTarget { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double? CapacityLiters { get; set; }
    public double? PowerKw { get; set; }
    public string? Objective { get; set; }
    public string? Description { get; set; }
    public string? OperationalNotes { get; set; }
    public string? ChemicalInjected { get; set; }
    public string? DoseRangeMgL { get; set; }
    public string? FrequencyRangeHz { get; set; }
    public string? CalculationRule { get; set; }
    public string? BackwashTrigger { get; set; }
    public string? Interlocks { get; set; }
    public List<string>? SensorParameters { get; set; }
}

public class StandardOperatingProcedure
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = [];
}

public class LearnedRule
{
    public string Id { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}