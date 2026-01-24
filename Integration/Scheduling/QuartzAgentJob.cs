using System;
using System.Diagnostics;
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

            // даже тут просим UI обновить next-run (на случай рассинхрона)
            bus.PublishAgentScheduleChanged(agentId);
            return;
        }

        var ct = context.CancellationToken;

        var tickCtx = new AgentTickContext
        {
            CancellationToken = ct,
            EventBus = bus,
            StartedAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

        var sw = Stopwatch.StartNew();

        try
        {
            // (Опционально) общий старт — агент сам пишет свои "tick start", но это полезно для единообразия.
            bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Quartz tick started: {agent.DisplayName}"));
            bus.PublishAgent(new AgentLogEntry(agentId, DateTimeOffset.Now, LogLevel.info, $"Quartz tick started: {agent.DisplayName}"));

            await agent.ExecuteTickAsync(tickCtx).ConfigureAwait(false);

            sw.Stop();

            // ВАЖНО: это то, чего не хватало — конец тика для Quartz-пути
            bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Quartz tick finished: {agent.DisplayName} ({sw.ElapsedMilliseconds} ms)"));
            bus.PublishAgent(new AgentLogEntry(agentId, DateTimeOffset.Now, LogLevel.info, $"Quartz tick finished: {agent.DisplayName} ({sw.ElapsedMilliseconds} ms)"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"Quartz tick canceled: {agent.DisplayName}"));
            bus.PublishAgent(new AgentLogEntry(agentId, DateTimeOffset.Now, LogLevel.warning, "Quartz tick canceled."));
        }
        catch (Exception ex)
        {
            sw.Stop();
            bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.error,
                $"Quartz tick error: {agent.DisplayName}. {ex.GetType().Name}: {ex.Message}"));

            bus.PublishAgent(new AgentLogEntry(agentId, DateTimeOffset.Now, LogLevel.error,
                $"Quartz tick error: {ex.GetType().Name}: {ex.Message}"));
        }
        finally
        {
            // ВАЖНО: без этого UI не узнаёт, что next-run изменился после Quartz-тика
            bus.PublishAgentScheduleChanged(agentId);
        }
    }

    // summary: Quartz Job-обёртка над агентом. Вызывает agent.ExecuteTickAsync по расписанию Quartz,
    //          не изменяя статус агента (paused/stopped не форсим) и уважая CancellationToken.
    //          Пишет "tick finished" и всегда дергает PublishAgentScheduleChanged, чтобы UI обновлял next-run/iterations.
}
