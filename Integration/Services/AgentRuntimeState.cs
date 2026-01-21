using Integration.Services;

public sealed class AgentRuntimeState
{
    // API partner state
    public ApiConnectionState Api { get; init; } = new();

    // ===== Execution facts =====

    /// <summary>
    /// Время старта последнего тика агента
    /// </summary>
    public DateTimeOffset? LastRunStartedAt { get; set; }

    /// <summary>
    /// Время завершения последнего тика агента
    /// </summary>
    public DateTimeOffset? LastRunFinishedAt { get; set; }

    /// <summary>
    /// Был ли последний тик завершён успешно
    /// </summary>
    public bool? LastRunSucceeded { get; set; }

    // ===== Errors =====

    // last failure (общий, не только API)
    public DateTimeOffset? LastErrorAt { get; set; }
    public string? LastErrorMessage { get; set; }

    // ===== Future / correlation =====
    public string? LastMessageId { get; set; }

    // summary: Runtime-состояние одного агента (in-memory + JSON).
    //          Хранит факты выполнения (старт/финиш/успех тика),
    //          состояние API-подключения и последнюю ошибку.
    //          НЕ содержит расписаний — только результат работы.
}