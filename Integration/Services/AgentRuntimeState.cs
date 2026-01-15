using System;

namespace Integration.Services;

public sealed class AgentRuntimeState
{
    // API partner state
    public ApiConnectionState Api { get; init; } = new();

    // last failure (общий, не только API)
    public DateTimeOffset? LastErrorAt { get; set; }
    public string? LastErrorMessage { get; set; }

    // будущее
    public string? LastMessageId { get; set; }
    // summary: Runtime-состояние одного агента, живущее между рестартами приложения.
    //          Содержит состояние API-подключения и последнюю ошибку/сообщение для отображения в UI.
}