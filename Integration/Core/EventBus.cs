using System;

namespace Integration.Core;

public sealed class EventBus : IEventBus
{
    public event Action<LogEntry>? GlobalLogAdded;
    public event Action<AgentLogEntry>? AgentLogAdded;
    public event Action<string, AgentStatus>? AgentStatusChanged;

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

    public void PublishAgentStatus(string agentId, AgentStatus status)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId is required.", nameof(agentId));

        SafeInvoke(AgentStatusChanged, agentId, status);
    }

    private static void SafeInvoke<T>(Action<T>? evt, T arg)
    {
        if (evt is null) return;

        foreach (var d in evt.GetInvocationList())
        {
            try
            {
                ((Action<T>)d).Invoke(arg);
            }
            catch
            {
                // MVP: глушим, чтобы один сломанный подписчик не ломал всю шину.
                // Позже можно прокидывать это в GlobalLogAdded или отдельный internal log.
            }
        }
    }

    private static void SafeInvoke<T1, T2>(Action<T1, T2>? evt, T1 arg1, T2 arg2)
    {
        if (evt is null) return;

        foreach (var d in evt.GetInvocationList())
        {
            try
            {
                ((Action<T1, T2>)d).Invoke(arg1, arg2);
            }
            catch
            {
                // см. комментарий выше
            }
        }
    }
}