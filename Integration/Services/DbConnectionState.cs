// Integration/Services/DbConnectionState.cs

using System;
using System.Text.Json.Serialization;
using Integration.Models;

namespace Integration.Services;

/// <summary>
/// Runtime DB connection state (mirrors runtime_state.json.db).
/// Used for healthcheck visualization and diagnostics.
/// </summary>
public sealed class DbConnectionState
{
    /// <summary>
    /// Human-readable connection name for UI (e.g. "Real Eko", "Test Eko").
    /// Mirrors runtime_state.json.db.connection_name.
    /// </summary>
    public string? Connection_Name { get; set; }

    /// <summary>
    /// Logical DB profile key (from parameters.json), e.g. "eko_test".
    /// </summary>
    public string? ProfileKey { get; set; }

    /// <summary>
    /// DB name/info returned by integration_pack.get_db_name / ping.
    /// Mirrors runtime_state.json.db.db_name.
    /// </summary>
    public string? Db_Name { get; set; }

    /// <summary>
    /// Current DB connection status.
    /// </summary>
    public ConnectionStateKind State { get; set; } = ConnectionStateKind.unknown;

    /// <summary>
    /// Human-readable status text for UI.
    /// Mirrors runtime_state.json.db.text.
    /// </summary>
    public string Text { get; set; } = "Состояние: неизвестно";

    public DateTimeOffset? Last_Success_At_Utc { get; set; }
    public DateTimeOffset? Last_Error_At_Utc { get; set; }

    public DbErrorState Last_Error { get; set; } = new();

    //Начало изменений
    // Backward-compatible aliases for old code (do not affect JSON)
    [JsonIgnore] public string? DbName { get => Db_Name; set => Db_Name = value; }
    [JsonIgnore] public string? ConnectionName { get => Connection_Name; set => Connection_Name = value; }
    //Конец изменений

    // summary:
    // Runtime DB connection state, aligned with runtime_state.json:
    // connection_name, state, text, last_success_at_utc, last_error_at_utc, last_error{...}.
    // Дополнительно хранит profileKey и db_name для диагностики.
}

/// <summary>
/// DB error details block.
/// </summary>
public sealed class DbErrorState
{
    public AgentStatusErrors? Code { get; set; }
    public string? Kind { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Normalized DB connection state for UI/runtime.
/// </summary>
public enum ConnectionStateKind
{
    unknown = 0,
    ok = 1,
    error = 2
}
