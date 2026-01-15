using System;

namespace Integration.Services;

public sealed class ApiConnectionState
{
    public ApiConnectionStatus Status { get; set; } = ApiConnectionStatus.unknown;

    public string? ErrorCode { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }
}

public enum ApiConnectionStatus
{
    unknown,    // при старте
    ok,         // последний тик успешен
    error       // ошибка именно API партнёра
}