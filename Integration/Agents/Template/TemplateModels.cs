using System;

namespace Integration.Agents.Template;

/// <summary>
/// Контейнер моделей шаблонного агента.
/// Используется как пример структуры DTO / domain-моделей,
/// которые обычно участвуют в работе агента:
///  - входные параметры
///  - ответы внешнего API
///  - внутреннее состояние обработки
/// </summary>
public static class TemplateModels
{
    /// <summary>
    /// Пример входных параметров для тика агента
    /// (может формироваться из parameters.json или runtime_state).
    /// </summary>
    public sealed class TemplateRequest
    {
        public string? ExternalId { get; init; }
        public DateTimeOffset RequestedAt { get; init; }
    }

    /// <summary>
    /// Пример ответа от внешнего API.
    /// </summary>
    public sealed class TemplateResponse
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
    }

    /// <summary>
    /// Пример внутреннего результата обработки,
    /// который может быть сохранён в runtime_state.json
    /// или использован для логирования.
    /// </summary>
    public sealed class TemplateResult
    {
        public bool IsProcessed { get; init; }
        public DateTimeOffset ProcessedAt { get; init; }
        public string? Details { get; init; }
    }
}