using System;
using Integration.Models;

namespace Integration.Services;

public sealed class ApiConnectionState
{
    public ApiConnectionStatus Status { get; set; } = ApiConnectionStatus.unknown;

    // Код ошибки (для UI достаточно ToString()).
    // Заполняем enum-ом, а не длинными текстами исключений.
    public AgentStatusErrors ErrorCode { get; set; } = AgentStatusErrors.none;

    // Тех.детали (можно логировать, но не обязательно показывать в UI).
    public string? ErrorMessage { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }
}

public enum ApiConnectionStatus
{
    unknown,    // при старте
    ok,         // последний тик успешен
    error       // ошибка именно API партнёра
}

// summary: ApiConnectionState хранит runtime-состояние подключения к внешнему API:
//          статус (unknown/ok/error), код ошибки (enum для UI), тех.сообщение и время последней проверки.