using System;
using Integration.Services;

//Начало изменений
/// <summary>
/// Runtime state of a single agent.
/// Mirrors runtime_state.json structure (api/db/tick/progress).
/// </summary>
//Конец изменений
public sealed class AgentRuntimeState
{
    // ===== API partner state =====

    public ApiConnectionState Api { get; init; } = new();

    // ===== DB state =====

    public DbConnectionState Db { get; init; } = new();

    // ===== Tick / execution =====

    //Начало изменений
    /// <summary>
    /// Tick execution info (last run timings & result).
    /// </summary>
    public TickState Tick { get; init; } = new();
    //Конец изменений

    // ===== Progress =====

    //Начало изменений
    /// <summary>
    /// Progress info (pagination / items).
    /// </summary>
    public ProgressState Progress { get; init; } = new();
    //Конец изменений

    // ===== Legacy / cross-cutting errors =====

    /// <summary>
    /// Время последней ошибки (любой: API / DB / внутренняя)
    /// </summary>
    public DateTimeOffset? LastErrorAt { get; set; }

    /// <summary>
    /// Сообщение последней ошибки
    /// </summary>
    public string? LastErrorMessage { get; set; }

    // ===== Correlation =====

    public string? LastMessageId { get; set; }

    //Начало изменений
    public static AgentRuntimeState CreateEmpty()
        => new();
    //Конец изменений

    // summary:
    // Runtime-состояние одного агента.
    // Полностью соответствует runtime_state.json:
    // api / db / tick / progress + общая ошибка.
    // Используется как in-memory snapshot и для сериализации.
}

/// <summary>
/// Tick execution block.
/// </summary>
public sealed class TickState
{
    public DateTimeOffset? Last_Started_At_Utc { get; set; }
    public DateTimeOffset? Last_Finished_At_Utc { get; set; }
    public long? Last_Duration_Ms { get; set; }
    public string Last_Result { get; set; } = "none";
}

/// <summary>
/// Progress / pagination block.
/// </summary>
public sealed class ProgressState
{
    public int Iterations_Total { get; set; }
    public int? Last_Page { get; set; }
    public int? Page_Total { get; set; }
    public int? Items_Last_Tick { get; set; }
}
