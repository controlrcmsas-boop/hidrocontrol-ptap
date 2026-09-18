using PTAPControl.Models;

namespace PTAPControl.Services;

/// <summary>
/// Servicio Scoped para mantener y persistir el historial de conversación
/// con el Agente Supervisor IA durante la sesión del operador en Blazor Server.
/// Evita que las conversaciones se borren al navegar entre pestañas del SCADA.
/// </summary>
public sealed class AiChatStateService
{
    private readonly List<AiChatMessage> _messages = [];
    private bool _initialized;

    public IReadOnlyList<AiChatMessage> Messages => _messages.AsReadOnly();

    public event Action? OnChange;

    public void EnsureInitialized()
    {
        if (_initialized) return;

        _messages.Add(new AiChatMessage
        {
            Role = "assistant",
            Content = "👋 **Hola, soy tu Asistente Inteligente de PTAP.**\n\nEstoy monitoreando en tiempo real las variables de la planta (pH, turbidez cruda/tratada, presiones y caudales). Puedes hacerme cualquier consulta técnica o darme instrucciones de control (ej: 'apaga la bomba B1', 'desenergiza todo').",
            Timestamp = DateTime.UtcNow
        });

        _initialized = true;
        NotifyStateChanged();
    }

    public void AddUserMessage(string text)
    {
        EnsureInitialized();
        _messages.Add(new AiChatMessage
        {
            Role = "user",
            Content = text,
            Timestamp = DateTime.UtcNow
        });
        NotifyStateChanged();
    }

    public AiChatMessage AddThinkingMessage()
    {
        EnsureInitialized();
        var thinking = new AiChatMessage
        {
            Role = "assistant",
            Content = string.Empty,
            IsThinking = true,
            Timestamp = DateTime.UtcNow
        };
        _messages.Add(thinking);
        NotifyStateChanged();
        return thinking;
    }

    public void ReplaceThinkingWithResponse(AiChatMessage thinkingMsg, string responseText, AiProposedAction? action = null)
    {
        _messages.Remove(thinkingMsg);
        _messages.Add(new AiChatMessage
        {
            Role = "assistant",
            Content = responseText,
            Action = action,
            Timestamp = DateTime.UtcNow
        });
        NotifyStateChanged();
    }

    public void AddAssistantMessage(string content, AiProposedAction? action = null)
    {
        EnsureInitialized();
        _messages.Add(new AiChatMessage
        {
            Role = "assistant",
            Content = content,
            Action = action,
            Timestamp = DateTime.UtcNow
        });
        NotifyStateChanged();
    }

    public void ClearHistory()
    {
        _messages.Clear();
        _initialized = false;
        EnsureInitialized();
        NotifyStateChanged();
    }

    public List<AiChatMessage> GetHistoryForCopilot(int count = 6)
    {
        return _messages
            .Where(m => !m.IsThinking && !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(count)
            .ToList();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
