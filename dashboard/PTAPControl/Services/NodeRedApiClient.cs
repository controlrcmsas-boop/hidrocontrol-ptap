using System.Net.Http.Json;
using PTAPControl.Models;

namespace PTAPControl.Services;

public sealed class NodeRedApiClient(HttpClient http, PtapSimulationEngine simulationEngine)
{
    public PtapSimulationEngine Simulation => simulationEngine;
    public Uri? BaseAddress => http.BaseAddress;
    public string EndpointDisplay => (http.BaseAddress?.Host is "localhost" or "127.0.0.1")
        ? "Node-RED Local"
        : (http.BaseAddress?.ToString().TrimEnd('/') ?? "Node-RED");

    private static DateTimeOffset _lastPlcAttempt = DateTimeOffset.MinValue;
    private static bool _lastPlcSuccess = false;
    private static TelemetryResponse? _lastPlcTelemetry = null;
    private static int _consecutiveFailures = 0;
    private const int MaxFailuresBeforeOffline = 3;
    private static string? _lastKnownMoMessage = null;
    private static string? _lastKnownMoSource = null;
    private static readonly TimeSpan PlcRetryInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan FastHttpTimeout = TimeSpan.FromSeconds(4);

    public bool IsPlcOnline => _lastPlcSuccess;

    public async Task<TelemetryResponse?> GetCurrentTelemetryAsync(CancellationToken cancellationToken = default)
    {
        // Si estamos en modo de simulación, respondemos desde el motor nativo en memoria de forma instantánea
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            var simStep = simulationEngine.GenerateTelemetryStep();
            simulationEngine.RecordTelemetrySample(simStep);
            return simStep;
        }

        // Modo PLC Activo: Si el último intento falló recientemente, devolvemos el estado sin bloquear el circuito Blazor
        var now = DateTimeOffset.UtcNow;
        if (!_lastPlcSuccess && _consecutiveFailures >= MaxFailuresBeforeOffline && (now - _lastPlcAttempt) < PlcRetryInterval)
        {
            return _lastPlcTelemetry ?? new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow };
        }

        _lastPlcAttempt = now;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(FastHttpTimeout);

            var response = await http.GetAsync("api/telemetry/current", cts.Token);
            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                _consecutiveFailures++;
                if (_consecutiveFailures >= MaxFailuresBeforeOffline)
                {
                    _lastPlcSuccess = false;
                    _lastPlcTelemetry = new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow, Source = "OFFLINE_LOCAL" };
                }
                return _lastPlcTelemetry ?? new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow };
            }

            response.EnsureSuccessStatusCode();
            var telemetry = await response.Content.ReadFromJsonAsync<TelemetryResponse>(cts.Token);
            if (telemetry is not null)
            {
                _consecutiveFailures = 0;
                _lastPlcSuccess = telemetry.PlcConnected;

                // Estabilidad SCADA: si el paquete no incluye mo_message pero teníamos uno previo válido, preservarlo
                if (telemetry.Values.TryGetValue("mo_message", out var msgObj) && msgObj is not null && !string.IsNullOrWhiteSpace(msgObj.ToString()))
                {
                    _lastKnownMoMessage = msgObj.ToString();
                    if (telemetry.Values.TryGetValue("mo_source", out var srcObj) && srcObj is not null)
                    {
                        _lastKnownMoSource = srcObj.ToString();
                    }
                }
                else if (_lastKnownMoMessage is not null)
                {
                    telemetry.Values["mo_message"] = _lastKnownMoMessage;
                    if (_lastKnownMoSource is not null)
                    {
                        telemetry.Values["mo_source"] = _lastKnownMoSource;
                    }
                }

                EvaluateAndDispatchJevMessage(telemetry);
                _lastPlcTelemetry = telemetry;
                simulationEngine.RecordTelemetrySample(telemetry);
                return telemetry;
            }

            _consecutiveFailures++;
            if (_consecutiveFailures >= MaxFailuresBeforeOffline)
            {
                _lastPlcSuccess = false;
                _lastPlcTelemetry = new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow, Source = "OFFLINE_LOCAL" };
            }
            return _lastPlcTelemetry ?? new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow };
        }
        catch
        {
            // Filtro de histéresis ante jitter de red: no declarar offline en un único paquete perdido
            _consecutiveFailures++;
            if (_consecutiveFailures >= MaxFailuresBeforeOffline)
            {
                _lastPlcSuccess = false;
                _lastPlcTelemetry = new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow, Source = "OFFLINE_LOCAL" };
            }
            return _lastPlcTelemetry ?? new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow };
        }
    }

    public async Task<CommandResponse?> SendCommandAsync(string target, string command, CancellationToken cancellationToken = default)
    {
        // Si estamos en modo de simulación, ejecutamos directamente en el motor nativo
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.ExecuteCommand(target, command);
        }

        var request = new CommandRequest
        {
            Target = target,
            Command = command,
            Operator = "operador_ptap",
            Source = "dashboard"
        };

        try
        {
            var response = await http.PostAsJsonAsync("api/control/command", request, cancellationToken);
            var commandResponse = await response.Content.ReadFromJsonAsync<CommandResponse>(cancellationToken);

            if (commandResponse is not null)
            {
                if (!response.IsSuccessStatusCode)
                {
                    commandResponse.Accepted = false;
                }
                return commandResponse;
            }

            return new CommandResponse
            {
                Accepted = response.IsSuccessStatusCode,
                Target = target,
                Command = command,
                Error = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new CommandResponse
            {
                Accepted = false,
                Target = target,
                Command = command,
                Error = $"Error de comunicación con Node-RED (Planta Remota): {ex.Message}",
                Timestamp = DateTimeOffset.UtcNow
            };
        }
    }

    private static string _lastEvaluatedCondition = string.Empty;
    private static DateTimeOffset _lastMoDispatchTime = DateTimeOffset.MinValue;

    private void EvaluateAndDispatchJevMessage(TelemetryResponse telemetry)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc) return;
        if (!telemetry.PlcConnected) return;

        var turb = telemetry.GetNumber("turbidez_agua_tratada") ?? telemetry.GetNumber("turbidez_agua_cruda") ?? 0.0;
        var ph = telemetry.GetNumber("ph_agua_tratada") ?? telemetry.GetNumber("ph_agua_cruda") ?? 7.0;
        var cond = telemetry.GetNumber("conductividad_agua_tratada") ?? telemetry.GetNumber("conductividad_agua_cruda") ?? 0.0;
        var bombaOn = telemetry.GetBool("bomba_principal_estado");

        string currentCondition;
        string targetMessage;

        if (!bombaOn)
        {
            currentCondition = "PUMP_OFF";
            targetMessage = "MO: PLANTA EN ESPERA. BOMBA DETENIDA. SISTEMA EN REPOSO Y SEGURIDAD FISICA ACTIVA.";
        }
        else if (turb > 5.0)
        {
            currentCondition = "HIGH_TURBIDITY";
            targetMessage = $"MO: ALERTA TURBIDEZ {turb:F1} NTU. COAGULACION CON SULFATO FH-201 PARA REMOCION Y CLARIFICACION.";
        }
        else if (ph < 5.5)
        {
            currentCondition = "ACIDIC_PH";
            targetMessage = $"MO: ALERTA pH ACIDO {ph:F2}. CLORACION PAUSADA PREVENTIVAMENTE POR SEGURIDAD QUIMICA.";
        }
        else if (ph > 8.5)
        {
            currentCondition = "ALKALINE_PH";
            targetMessage = $"MO: ALERTA pH ALCALINO {ph:F2}. AJUSTE QUIMICO REQUERIDO PARA CUMPLIR NORMA POTABLE.";
        }
        else if (cond > 1000.0)
        {
            currentCondition = "HIGH_CONDUCTIVITY";
            targetMessage = $"MO: ALERTA CONDUCTIVIDAD {cond:F0} uS/cm. INTRUSION DE SALES DETECTADA EN AFLUENTE.";
        }
        else
        {
            currentCondition = "NOMINAL";
            targetMessage = "MO: AGUA CLARIFICADA DENTRO DE NORMA POTABLE. DESINFECCION CONTINUA ACTIVA Y REMOCION OPTIMA.";
        }

        var currentMsg = telemetry.GetString("mo_message") ?? string.Empty;
        var now = DateTimeOffset.UtcNow;

        bool needsUpdate = (currentCondition != _lastEvaluatedCondition) 
                        || (!currentMsg.StartsWith("MO: ALERTA") && currentCondition != "NOMINAL" && currentCondition != "PUMP_OFF")
                        || (currentCondition == "NOMINAL" && (currentMsg.Contains("restablecida") || currentMsg.Contains("SUPERVISION NOMINAL")));

        if (needsUpdate && (now - _lastMoDispatchTime) > TimeSpan.FromSeconds(3))
        {
            _lastEvaluatedCondition = currentCondition;
            _lastMoDispatchTime = now;
            _lastKnownMoMessage = targetMessage;
            _lastKnownMoSource = "JEV_CLOUD";
            telemetry.Values["mo_message"] = targetMessage;
            telemetry.Values["mo_source"] = "JEV_CLOUD";

            _ = Task.Run(async () =>
            {
                try
                {
                    await SendMoMessageAsync(targetMessage, "JEV_CLOUD");
                }
                catch { }
            });
        }
    }

    public async Task<bool> SendMoMessageAsync(string message, string source = "JEV_CLOUD", CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                target = "mo_message",
                command = "update",
                message = message,
                source = source,
                operator_name = "jev_ai"
            };
            var response = await http.PostAsJsonAsync("api/control/command", request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<QualityHistoryPoint>> GetQualityHistoryAsync(string tag, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc || !_lastPlcSuccess)
        {
            return simulationEngine.GetQualityHistory(tag, limit);
        }

        var url = $"api/telemetry/quality-history?tag={Uri.EscapeDataString(tag)}&limit={limit}";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(400));
            return await http.GetFromJsonAsync<List<QualityHistoryPoint>>(url, cts.Token) ?? [];
        }
        catch
        {
            return simulationEngine.GetQualityHistory(tag, limit);
        }
    }

    public async Task<List<AlarmEvent>> GetActiveAlarmsAsync(CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.GetActiveAlarms();
        }

        try
        {
            return await http.GetFromJsonAsync<List<AlarmEvent>>("api/alarms/active", cancellationToken) ?? [];
        }
        catch
        {
            return simulationEngine.GetActiveAlarms();
        }
    }

    public async Task<List<AlarmEvent>> GetAlarmHistoryAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.GetActiveAlarms();
        }

        try
        {
            return await http.GetFromJsonAsync<List<AlarmEvent>>($"api/alarms/history?limit={limit}", cancellationToken) ?? [];
        }
        catch
        {
            return simulationEngine.GetActiveAlarms();
        }
    }

    public async Task<List<AlarmLimit>> GetAlarmLimitsAsync(CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.GetAlarmLimits();
        }

        try
        {
            return await http.GetFromJsonAsync<List<AlarmLimit>>("api/alarm-limits", cancellationToken) ?? simulationEngine.GetAlarmLimits();
        }
        catch
        {
            return simulationEngine.GetAlarmLimits();
        }
    }

    public async Task<AlarmLimit?> UpdateAlarmLimitAsync(AlarmLimit limit, CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.UpdateAlarmLimit(limit);
        }

        try
        {
            var response = await http.PostAsJsonAsync("api/alarm-limits", limit, cancellationToken);
            response.EnsureSuccessStatusCode();
            var rows = await response.Content.ReadFromJsonAsync<List<AlarmLimit>>(cancellationToken);
            return rows?.FirstOrDefault() ?? simulationEngine.UpdateAlarmLimit(limit);
        }
        catch
        {
            return simulationEngine.UpdateAlarmLimit(limit);
        }
    }

    public async Task<List<NotificationEmail>> GetNotificationEmailsAsync(CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.GetNotificationEmails();
        }

        try
        {
            return await http.GetFromJsonAsync<List<NotificationEmail>>("api/notification-emails", cancellationToken) ?? simulationEngine.GetNotificationEmails();
        }
        catch
        {
            return simulationEngine.GetNotificationEmails();
        }
    }

    public async Task<NotificationEmail?> AddNotificationEmailAsync(string email, string? name, CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.AddNotificationEmail(email, name);
        }

        try
        {
            var request = new NotificationEmail { Email = email, Name = name, Enabled = true };
            var response = await http.PostAsJsonAsync("api/notification-emails", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var rows = await response.Content.ReadFromJsonAsync<List<NotificationEmail>>(cancellationToken);
            return rows?.FirstOrDefault() ?? simulationEngine.AddNotificationEmail(email, name);
        }
        catch
        {
            return simulationEngine.AddNotificationEmail(email, name);
        }
    }

    public async Task<List<NotificationWhatsApp>> GetNotificationWhatsAppAsync(CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.GetNotificationWhatsApp();
        }

        try
        {
            return await http.GetFromJsonAsync<List<NotificationWhatsApp>>("api/notification-whatsapp", cancellationToken) ?? simulationEngine.GetNotificationWhatsApp();
        }
        catch
        {
            return simulationEngine.GetNotificationWhatsApp();
        }
    }

    public async Task<NotificationWhatsApp?> AddNotificationWhatsAppAsync(string phone, string? name, string? apiKey = null, CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.AddNotificationWhatsApp(phone, name);
        }

        try
        {
            var request = new { phone, name, apikey = apiKey };
            var response = await http.PostAsJsonAsync("api/notification-whatsapp", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var rows = await response.Content.ReadFromJsonAsync<List<NotificationWhatsApp>>(cancellationToken);
            return rows?.FirstOrDefault() ?? simulationEngine.AddNotificationWhatsApp(phone, name);
        }
        catch
        {
            return simulationEngine.AddNotificationWhatsApp(phone, name);
        }
    }

    public async Task<bool> DeleteNotificationWhatsAppAsync(long id, CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.DeleteNotificationWhatsApp(id);
        }

        try
        {
            var request = new { id };
            var response = await http.PostAsJsonAsync("api/notification-whatsapp/delete", request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return simulationEngine.DeleteNotificationWhatsApp(id);
        }
    }

    public async Task<bool> SendTestWhatsAppAsync(string? phone = null, string? name = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetPhone = !string.IsNullOrWhiteSpace(phone) ? phone : "+573001234567";
            var clientName = !string.IsNullOrWhiteSpace(name) ? name : "Operador";

            // Intento directo con el Gateway local de WhatsApp (Puerto 5089)
            using var localWaClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var payload = new
            {
                phone = targetPhone,
                message = $"🔔 *HIDROCONTROL PTAP — AI CONTROL*\n\nHola *{clientName}*, bienvenido a la supervisión inteligente de planta.\n\nTu dispositivo ha sido vinculado exitosamente. Recibirás en tiempo real las alertas de calidad de agua y los reportes de contingencia de *MO*."
            };
            var res = await localWaClient.PostAsJsonAsync("http://localhost:5089/send", payload, cancellationToken);
            if (res.IsSuccessStatusCode) return true;
        }
        catch
        {
            // Continuar con fallback
        }

        try
        {
            var request = new { phone, name };
            var response = await http.PostAsJsonAsync("api/notification-whatsapp/test", request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return true;
        }
    }

    public async Task<bool> SendWhatsAppMessageAsync(string message, string? phone = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var numbers = await GetNotificationWhatsAppAsync(cancellationToken);
            var targets = new List<string>();
            if (!string.IsNullOrWhiteSpace(phone)) targets.Add(phone);
            foreach (var w in numbers.Where(n => n.Enabled && !string.IsNullOrWhiteSpace(n.PhoneNumber)))
            {
                if (!targets.Contains(w.PhoneNumber)) targets.Add(w.PhoneNumber);
            }
            if (targets.Count == 0) targets.Add("+573001234567");

            using var localWaClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            bool anySuccess = false;
            foreach (var t in targets)
            {
                try
                {
                    var payload = new { phone = t, message };
                    var res = await localWaClient.PostAsJsonAsync("http://localhost:5089/send", payload, cancellationToken);
                    if (res.IsSuccessStatusCode) anySuccess = true;
                }
                catch { }
            }
            return anySuccess;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SimulatorStateDto?> GetSimulatorStateAsync(CancellationToken cancellationToken = default)
    {
        // Si el motor nativo está disponible, retornamos directamente su estado
        return await Task.FromResult(new SimulatorStateDto
        {
            PhCrudaBase = simulationEngine.PhCrudaBase,
            TurbidezCrudaBase = simulationEngine.TurbidezCrudaBase,
            ConductividadCrudaBase = simulationEngine.ConductividadCrudaBase,
            PresionB1Base = simulationEngine.PresionB1Base,
            CaudalB1Base = simulationEngine.CaudalB1Base,
            BombaPrincipalEstado = simulationEngine.BombaPrincipalEstado,
            DosificadorSulfatoEstado = simulationEngine.DosificadorSulfatoEstado,
            DosificadorCloroEstado = simulationEngine.DosificadorCloroEstado,
            NivelTanqueAguaCruda = simulationEngine.NivelTanqueAguaCruda,
            ModoRemotoHabilitado = simulationEngine.ModoRemotoHabilitado,
            PlcEnFalla = simulationEngine.PlcEnFalla,
            Scenario = simulationEngine.ActiveScenario
        });
    }

    public async Task<SimulatorStateDto?> UpdateSimulatorStateAsync(SimulatorStateDto state, CancellationToken cancellationToken = default)
    {
        // Actualizamos directamente en el motor en memoria
        simulationEngine.PhCrudaBase = state.PhCrudaBase;
        simulationEngine.TurbidezCrudaBase = state.TurbidezCrudaBase;
        simulationEngine.ConductividadCrudaBase = state.ConductividadCrudaBase;
        simulationEngine.PresionB1Base = state.PresionB1Base;
        simulationEngine.CaudalB1Base = state.CaudalB1Base;
        simulationEngine.BombaPrincipalEstado = state.BombaPrincipalEstado;
        simulationEngine.DosificadorSulfatoEstado = state.DosificadorSulfatoEstado;
        simulationEngine.DosificadorCloroEstado = state.DosificadorCloroEstado;
        simulationEngine.NivelTanqueAguaCruda = state.NivelTanqueAguaCruda;
        simulationEngine.ModoRemotoHabilitado = state.ModoRemotoHabilitado;
        simulationEngine.PlcEnFalla = state.PlcEnFalla;

        simulationEngine.NotifyParametersChanged();

        return await GetSimulatorStateAsync(cancellationToken);
    }

    public async Task<bool> SetSimulatorScenarioAsync(string scenario, CancellationToken cancellationToken = default)
    {
        simulationEngine.SetScenario(scenario);
        simulationEngine.NotifyParametersChanged();
        return await Task.FromResult(true);
    }
}

