using System;
using System.Threading.Tasks;
using Integration.Core;

namespace Integration.Agents.Template;

public sealed class TemplateAgent : IAgent
{
    public const string AgentId = "template";

    public string Id => AgentId;

    public string DisplayName => "Template Agent";

    public AgentStatus Status { get; private set; } = AgentStatus.stopped;

    public string FunctionDescription => "Шаблон агента (копируй и адаптируй под нового агента).";

    public void Pause()
    {
        if (Status == AgentStatus.active)
            Status = AgentStatus.paused;
    }

    public void Resume()
    {
        if (Status == AgentStatus.paused)
            Status = AgentStatus.active;
    }

    public void Stop()
        => Status = AgentStatus.stopped;

    public Task ExecuteTickAsync(AgentTickContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // IMPORTANT:
        // - Здесь только каркас. Реальную логику тика добавляй в конкретном агенте.
        // - Сюда обычно добавляют: загрузку параметров, вызов API/DB, обновление runtime_state, публикацию логов.
        context.EventBus?.PublishAgent(new AgentLogEntry(
            Id,
            DateTimeOffset.Now,
            LogLevel.info,
            "Template tick executed (no-op)."));

        return Task.CompletedTask;
    }

    // summary: TemplateAgent — базовый шаблон реализации IAgent для копирования при создании новых агентов.
}