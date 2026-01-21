using System;
using System.Threading;
using System.Threading.Tasks;

namespace Integration.Core;

public interface IAgent
{
    /// <summary>
    /// Уникальный идентификатор агента (machine id).
    /// Пример: "uzstandart"
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Отображаемое имя агента для UI.
    /// Пример: "TIMV UzStandart"
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Текущий статус агента.
    /// </summary>
    AgentStatus Status { get; }

    /// <summary>
    /// Основной тик агента.
    /// Вызывается оркестратором по расписанию или вручную (Start now).
    /// </summary>
    Task ExecuteTickAsync(AgentTickContext context);

    /// <summary>
    /// Пауза агента (без уничтожения состояния).
    /// </summary>
    void Pause();

    /// <summary>
    /// Возобновление после паузы.
    /// </summary>
    void Resume();

    /// <summary>
    /// Полная остановка агента.
    /// После этого тик не должен выполняться.
    /// </summary>
    void Stop();
}

public sealed class AgentTickContext
{
    public required CancellationToken CancellationToken { get; init; }

    public required IEventBus EventBus { get; init; }

    /// <summary>
    /// Время старта тика (единое для всех логов этого прохода).
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Корреляционный идентификатор тика (для логов).
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
}