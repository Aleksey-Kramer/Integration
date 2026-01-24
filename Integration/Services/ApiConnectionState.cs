// Integration/Services/ApiConnectionState.cs

//Начало изменений
using System;
using Integration.Models;

namespace Integration.Services;

/// <summary>
/// Runtime API connection state (mirrors runtime_state.json.api).
/// Used for healthcheck visualization and diagnostics.
/// </summary>
public sealed class ApiConnectionState
{
    /// <summary>
    /// API base url for diagnostics (runtime_state.json.api.base_url).
    /// </summary>
    public string? Base_Url { get; set; }

    /// <summary>
    /// Current API connection status (runtime_state.json.api.state).
    /// </summary>
    public ApiConnectionStatus Status { get; set; } = ApiConnectionStatus.unknown;

    /// <summary>
    /// Human-readable status text for UI (runtime_state.json.api.text).
    /// </summary>
    public string Text { get; set; } = "Состояние: неизвестно";

    public DateTimeOffset? Last_Success_At_Utc { get; set; }
    public DateTimeOffset? Last_Error_At_Utc { get; set; }

    public ApiErrorState Last_Error { get; set; } = new();
}

/// <summary>
/// API error details block (runtime_state.json.api.last_error).
/// </summary>
public sealed class ApiErrorState
{
    public AgentStatusErrors? Code { get; set; }
    public string? Kind { get; set; }
    public string? Message { get; set; }
}

public enum ApiConnectionStatus
{
    unknown = 0,
    ok = 1,
    error = 2
}
//Конец изменений