using System;
using Integration.Services;

namespace Integration.Core;

public interface IEventBus
{
    // --- Publish API (вызывают агенты/оркестратор) ---

    void PublishGlobal(LogEntry entry);

    void PublishAgent(AgentLogEntry entry);

    void PublishAgentStatus(string agentId, AgentStatus status);

    // NEW: явное изменение состояния коннекта к API агента
    void PublishAgentApiStateChanged(
        string agentId,
        ApiConnectionStatus status,
        string? errorCode = null,
        string? errorMessage = null);

    // --- Subscribe API (используют VM / UI) ---

    event Action<LogEntry>? GlobalLogAdded;

    event Action<AgentLogEntry>? AgentLogAdded;

    event Action<string, AgentStatus>? AgentStatusChanged;

    // NEW
    event Action<string, ApiConnectionStatus, string?, string?>? AgentApiStateChanged;
}

public enum LogLevel
{
    info,
    warning,
    error
}

public sealed record LogEntry(
    DateTimeOffset At,
    LogLevel Level,
    string Message);

public sealed record AgentLogEntry(
    string AgentId,
    DateTimeOffset At,
    LogLevel Level,
    string Message);

// summary:
// IEventBus — центральная шина событий между агентами/оркестратором и UI.
// Теперь умеет передавать явный статус коннекта к API (AgentApiStateChanged),
// чтобы UI не строил эвристику по текстам логов.