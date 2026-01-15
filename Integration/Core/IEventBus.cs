using System;

namespace Integration.Core;



public interface IEventBus
{
    // --- Publish API (вызывают агенты/оркестратор) ---

    void PublishGlobal(LogEntry entry);

    void PublishAgent(AgentLogEntry entry);

    void PublishAgentStatus(string agentId, AgentStatus status);

    // --- Subscribe API (используют VM / UI) ---

    event Action<LogEntry>? GlobalLogAdded;

    event Action<AgentLogEntry>? AgentLogAdded;

    event Action<string, AgentStatus>? AgentStatusChanged;
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