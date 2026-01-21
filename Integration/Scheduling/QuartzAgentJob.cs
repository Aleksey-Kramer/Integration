using System;
using System.Threading;
using System.Threading.Tasks;
using Integration.Core;
using Integration.Services;
using Quartz;

namespace Integration.Scheduling;

[DisallowConcurrentExecution]
public sealed class QuartzAgentJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var agentId = context.MergedJobDataMap.GetString("agentId");
        if (string.IsNullOrWhiteSpace(agentId))
            throw new InvalidOperationException("QuartzAgentJob: 'agentId' is missing in JobDataMap.");

        // QuartzSchedulerService должен положить зависимости в SchedulerContext:
        //  - "agentManager" => AgentManager
        //  - "eventBus"     => IEventBus
        var mgrObj = context.Scheduler.Context.Get("agentManager");
        var busObj = context.Scheduler.Context.Get("eventBus");

        if (mgrObj is not AgentManager manager)
            throw new InvalidOperationException("QuartzAgentJob: AgentManager not found in SchedulerContext (key='agentManager').");

        if (busObj is not IEventBus bus)
            throw new InvalidOperationException("QuartzAgentJob: IEventBus not found in SchedulerContext (key='eventBus').");

        if (!manager.TryGetAgent(agentId, out var agent) || agent is null)
        {
            bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning,
                $"Quartz: tick skipped (unknown agent '{agentId}')."));
            return;
        }

        // Важно: Quartz НЕ должен форсить Resume() для paused/stopped.
        // Агент сам решает, выполнять тик или логировать "skipped".
        var ct = context.CancellationToken;

        var tickCtx = new AgentTickContext
        {
            CancellationToken = ct,
            EventBus = bus,
            StartedAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

        try
        {
            await agent.ExecuteTickAsync(tickCtx).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // cancel — нормальный сценарий при stop/shutdown
            bus.PublishAgent(new AgentLogEntry(agentId, DateTimeOffset.Now, LogLevel.warning, "Quartz tick canceled."));
        }
    }

    // summary: Quartz Job-обёртка над агентом. Вызывает agent.ExecuteTickAsync по расписанию Quartz,
    //          не изменяя статус агента (paused/stopped не форсим) и уважая CancellationToken.
}
