using System.Net.Http.Json;
using PTAPControl.Models;

namespace PTAPControl.Services;

public sealed class NodeRedApiClient(HttpClient http)
{
    public async Task<TelemetryResponse?> GetCurrentTelemetryAsync(CancellationToken cancellationToken = default)
    {
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
    }

    public async Task<CommandResponse?> SendCommandAsync(string target, string command, CancellationToken cancellationToken = default)
    {
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
        var url = $"api/telemetry/quality-history?tag={Uri.EscapeDataString(tag)}&limit={limit}";
        return await http.GetFromJsonAsync<List<QualityHistoryPoint>>(url, cancellationToken) ?? [];
    }

    public async Task<List<AlarmEvent>> GetActiveAlarmsAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<AlarmEvent>>("api/alarms/active", cancellationToken) ?? [];
    }

    public async Task<List<AlarmEvent>> GetAlarmHistoryAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<AlarmEvent>>($"api/alarms/history?limit={limit}", cancellationToken) ?? [];
    }

    public async Task<List<AlarmLimit>> GetAlarmLimitsAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<AlarmLimit>>("api/alarm-limits", cancellationToken) ?? [];
    }

    public async Task<AlarmLimit?> UpdateAlarmLimitAsync(AlarmLimit limit, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/alarm-limits", limit, cancellationToken);
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<AlarmLimit>>(cancellationToken);
        return rows?.FirstOrDefault();
    }

    public async Task<List<NotificationEmail>> GetNotificationEmailsAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<NotificationEmail>>("api/notification-emails", cancellationToken) ?? [];
    }

    public async Task<NotificationEmail?> AddNotificationEmailAsync(string email, string? name, CancellationToken cancellationToken = default)
    {
        var request = new NotificationEmail { Email = email, Name = name, Enabled = true };
        var response = await http.PostAsJsonAsync("api/notification-emails", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<NotificationEmail>>(cancellationToken);
        return rows?.FirstOrDefault();
    }

    public async Task<List<NotificationWhatsApp>> GetNotificationWhatsAppAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<NotificationWhatsApp>>("api/notification-whatsapp", cancellationToken) ?? [];
    }

    public async Task<NotificationWhatsApp?> AddNotificationWhatsAppAsync(string phone, string? name, string? apiKey, CancellationToken cancellationToken = default)
    {
        var request = new { phone, name, apikey = apiKey };
        var response = await http.PostAsJsonAsync("api/notification-whatsapp", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<NotificationWhatsApp>>(cancellationToken);
        return rows?.FirstOrDefault();
    }

    public async Task<bool> DeleteNotificationWhatsAppAsync(long id, CancellationToken cancellationToken = default)
    {
        var request = new { id };
        var response = await http.PostAsJsonAsync("api/notification-whatsapp/delete", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SendTestWhatsAppAsync(string? phone = null, string? name = null, CancellationToken cancellationToken = default)
    {
        var request = new { phone, name };
        var response = await http.PostAsJsonAsync("api/notification-whatsapp/test", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<SimulatorStateDto?> GetSimulatorStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await http.GetFromJsonAsync<SimulatorStateDto>("api/simulator/state", cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<SimulatorStateDto?> UpdateSimulatorStateAsync(SimulatorStateDto state, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync("api/simulator/state", state, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var res = await response.Content.ReadFromJsonAsync<SimulatorUpdateResponse>(cancellationToken);
            return res?.State;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> SetSimulatorScenarioAsync(string scenario, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.GetAsync($"api/scenario/{scenario}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

