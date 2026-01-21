using System;
using Integration.Services;

namespace Integration.Core;

public sealed class EventBus : IEventBus
{
    public event Action<LogEntry>? GlobalLogAdded;
    public event Action<AgentLogEntry>? AgentLogAdded;
    //Начало изменений
    public event Action<string, AgentStatus>? AgentStatusChanged;
    public event Action<string>? AgentScheduleChanged;

    // NEW
    public event Action<string, ApiConnectionStatus, string?, string?>? AgentApiStateChanged;
    //Конец изменений

    public void PublishGlobal(LogEntry entry)
    {
        if (entry is null)
            throw new ArgumentNullException(nameof(entry));

        SafeInvoke(GlobalLogAdded, entry);
    }

    public void PublishAgent(AgentLogEntry entry)
    {
        if (entry is null)
            throw new ArgumentNullException(nameof(entry));

        SafeInvoke(AgentLogAdded, entry);
    }

    //Начало изменений
    public void PublishAgentStatus(string agentId, AgentStatus status)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId is required.", nameof(agentId));

        SafeInvoke(AgentStatusChanged, agentId, status);
    }

    public void PublishAgentScheduleChanged(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId is required.", nameof(agentId));

        SafeInvoke(AgentScheduleChanged, agentId);
    }
    //Конец изменений

    // NEW
    public void PublishAgentApiStateChanged(
        string agentId,
        ApiConnectionStatus status,
        string? errorCode = null,
        string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId is required.", nameof(agentId));

        SafeInvoke(AgentApiStateChanged, agentId, status, errorCode, errorMessage);
    }

    private static void SafeInvoke<T>(Action<T>? evt, T arg)
    {
        if (evt is null) return;

        foreach (var d in evt.GetInvocationList())
        {
            try { ((Action<T>)d).Invoke(arg); }
            catch { /* см. комментарии ниже */ }
        }
    }

    private static void SafeInvoke<T1, T2>(Action<T1, T2>? evt, T1 arg1, T2 arg2)
    {
        if (evt is null) return;

        foreach (var d in evt.GetInvocationList())
        {
            try { ((Action<T1, T2>)d).Invoke(arg1, arg2); }
            catch { /* см. комментарии ниже */ }
        }
    }

    // NEW
    private static void SafeInvoke<T1, T2, T3, T4>(Action<T1, T2, T3, T4>? evt, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (evt is null) return;

        foreach (var d in evt.GetInvocationList())
        {
            try { ((Action<T1, T2, T3, T4>)d).Invoke(arg1, arg2, arg3, arg4); }
            catch
            {
                // MVP: глушим, чтобы один сломанный подписчик не ломал всю шину.
                // Позже можно логировать в GlobalLogAdded или отдельный internal log.
            }
        }
    }

    //Начало изменений
    // summary: Реализация IEventBus — централизованной шины событий между агентами/оркестратором и UI.
    //          Публикует логи, статусы агентов, сигнал обновления расписания (AgentScheduleChanged)
    //          и явное состояние коннекта к API (AgentApiStateChanged).
    //Конец изменений

}
