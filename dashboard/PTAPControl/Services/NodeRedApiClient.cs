using System.Net.Http.Json;
using PTAPControl.Models;

namespace PTAPControl.Services;

public sealed class NodeRedApiClient(HttpClient http, PtapSimulationEngine simulationEngine)
{
    public PtapSimulationEngine Simulation => simulationEngine;

    public async Task<TelemetryResponse?> GetCurrentTelemetryAsync(CancellationToken cancellationToken = default)
    {
        // Si estamos en modo de simulación (automático o manual), respondemos desde el motor nativo en memoria
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.GenerateTelemetryStep();
        }

        try
        {
            var response = await http.GetAsync("api/telemetry/current", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                return new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow };
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TelemetryResponse>(cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            return new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow };
        }
        catch
        {
            // Si el PLC/Node-RED físico no responde, reportamos desconexión limpia
            return new TelemetryResponse { PlcConnected = false, Timestamp = DateTime.UtcNow };
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

        var response = await http.PostAsJsonAsync("api/control/command", request, cancellationToken);
        var commandResponse = await response.Content.ReadFromJsonAsync<CommandResponse>(cancellationToken);

        if (!response.IsSuccessStatusCode && commandResponse is not null)
        {
            commandResponse.Accepted = false;
        }

        return commandResponse;
    }

    public async Task<List<QualityHistoryPoint>> GetQualityHistoryAsync(string tag, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return simulationEngine.GetQualityHistory(tag, limit);
        }

        var url = $"api/telemetry/quality-history?tag={Uri.EscapeDataString(tag)}&limit={limit}";
        try
        {
            return await http.GetFromJsonAsync<List<QualityHistoryPoint>>(url, cancellationToken) ?? [];
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

    public async Task<NotificationWhatsApp?> AddNotificationWhatsAppAsync(string phone, string? name, string? apiKey, CancellationToken cancellationToken = default)
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
        if (simulationEngine.CurrentMode != PtapDataSourceMode.PhysicalPlc)
        {
            return true;
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

        return await GetSimulatorStateAsync(cancellationToken);
    }

    public async Task<bool> SetSimulatorScenarioAsync(string scenario, CancellationToken cancellationToken = default)
    {
        simulationEngine.SetScenario(scenario);
        return await Task.FromResult(true);
    }
}

