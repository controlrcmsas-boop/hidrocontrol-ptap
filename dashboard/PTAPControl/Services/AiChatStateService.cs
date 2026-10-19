using System.Text.Json;
using PTAPControl.Models;

namespace PTAPControl.Services;

/// <summary>
/// Servicio Singleton/Scoped para mantener y persistir el historial de conversación
/// con el Agente Supervisor IA (TypeSafe JEV / Agente Local).
/// Guarda automáticamente las conversaciones en disco para que NUNCA se borren
/// al recargar la página, navegar o reiniciar la aplicación.
/// </summary>
public sealed class AiChatStateService
{
    private readonly List<AiChatMessage> _messages = [];
    private bool _initialized;
    private static readonly object _lock = new();
    private readonly string _storagePath;

    public IReadOnlyList<AiChatMessage> Messages
    {
        get
        {
            lock (_lock)
            {
                return _messages.ToList().AsReadOnly();
            }
        }
    }

    public event Action? OnChange;

    public AiChatStateService()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }
        _storagePath = Path.Combine(dataDir, "ai-chat-history.json");
    }

    public void EnsureInitialized()
    {
        lock (_lock)
        {
            if (_initialized) return;

            LoadFromDisk();

            if (_messages.Count == 0)
            {
                _messages.Add(new AiChatMessage
                {
                    Role = "assistant",
                    Content = "**Hola, soy tu Asistente IA para HIDROCONTROL PTAP.**\n\nSuperviso en tiempo real las variables operativas de la planta y te asisto en diagnóstico de calidad, dosificación química y control. ¿En qué puedo ayudarte hoy?",
                    Timestamp = DateTime.UtcNow
                });
                SaveToDisk();
            }

            _initialized = true;
        }

        NotifyStateChanged();
    }

    public void AddUserMessage(string text)
    {
        EnsureInitialized();
        lock (_lock)
        {
            _messages.Add(new AiChatMessage
            {
                Role = "user",
                Content = text,
                Timestamp = DateTime.UtcNow
            });
            SaveToDisk();
        }
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
        lock (_lock)
        {
            _messages.Add(thinking);
        }
        NotifyStateChanged();
        return thinking;
    }

    public void ReplaceThinkingWithResponse(AiChatMessage thinkingMsg, string responseText, AiProposedAction? action = null)
    {
        lock (_lock)
        {
            _messages.Remove(thinkingMsg);
            _messages.Add(new AiChatMessage
            {
                Role = "assistant",
                Content = responseText,
                Action = action,
                Timestamp = DateTime.UtcNow
            });
            SaveToDisk();
        }
        NotifyStateChanged();
    }

    public void AddAssistantMessage(string content, AiProposedAction? action = null)
    {
        EnsureInitialized();
        lock (_lock)
        {
            _messages.Add(new AiChatMessage
            {
                Role = "assistant",
                Content = content,
                Action = action,
                Timestamp = DateTime.UtcNow
            });
            SaveToDisk();
        }
        NotifyStateChanged();
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _messages.Clear();
            _messages.Add(new AiChatMessage
            {
                Role = "assistant",
                Content = "**Historial reiniciado.** Estoy listo para asistirte en la supervisión operativa.",
                Timestamp = DateTime.UtcNow
            });
            SaveToDisk();
        }
        NotifyStateChanged();
    }

    public List<AiChatMessage> GetHistoryForCopilot(int count = 12)
    {
        lock (_lock)
        {
            return _messages
                .Where(m => !m.IsThinking && !string.IsNullOrWhiteSpace(m.Content))
                .TakeLast(count)
                .ToList();
        }
    }

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                var json = File.ReadAllText(_storagePath);
                var items = JsonSerializer.Deserialize<List<AiChatMessage>>(json);
                if (items is not null && items.Count > 0)
                {
                    _messages.Clear();
                    _messages.AddRange(items.Where(m => !m.IsThinking));
                }
            }
        }
        catch
        {
            // Fallback si el archivo está bloqueado o corrupto
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var validMessages = _messages.Where(m => !m.IsThinking).ToList();
            var json = JsonSerializer.Serialize(validMessages, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storagePath, json);
        }
        catch
        {
            // Silencioso en caso de error de concurrencia
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
