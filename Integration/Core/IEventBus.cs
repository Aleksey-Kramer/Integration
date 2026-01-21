using System;
using Integration.Services;

namespace Integration.Core;

public interface IEventBus
{
    // --- Publish API (вызывают агенты/оркестратор) ---

    void PublishGlobal(LogEntry entry);

    void PublishAgent(AgentLogEntry entry);

    void PublishAgentStatus(string agentId, AgentStatus status);

    /// <summary>
    /// Сигнал, что данные расписания/следующего запуска для агента могли измениться
    /// (например, после тика, смены статуса, переинициализации Quartz).
    /// </summary>
    void PublishAgentScheduleChanged(string agentId);

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

    /// <summary>
    /// Сигнал UI, что пора обновить данные расписания (Next run / Iterations) для агента.
    /// UI сам запрашивает актуальные данные через Scheduling-сервис (без зависимости от Quartz API).
    /// </summary>
    event Action<string>? AgentScheduleChanged;

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
// Передаёт логи, статусы, явный статус коннекта к API (AgentApiStateChanged),
// и сигнал на обновление данных расписания/следующего запуска (AgentScheduleChanged) без зависимости UI от Quartz.