using System;
using System.Linq;
using System.Threading.Tasks;
using Integration.Core;
using Integration.Services;
using Quartz;
using Quartz.Impl;

namespace Integration.Scheduling;

/// <summary>
/// Сервис-обёртка над Quartz Scheduler.
/// Отвечает за:
///  - инициализацию in-memory scheduler
///  - регистрацию Quartz Job для каждого агента
///  - получение информации о расписании (next run, описание)
/// UI и ViewModel не знают о Quartz напрямую.
/// </summary>
public sealed class QuartzSchedulerService
{
    private readonly IScheduler _scheduler;
    private readonly ParametersStore _parameters;
    private readonly AgentManager _agentManager;
    private readonly IEventBus _eventBus;

    public QuartzSchedulerService(
        ParametersStore parameters,
        AgentManager agentManager,
        IEventBus eventBus)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _agentManager = agentManager ?? throw new ArgumentNullException(nameof(agentManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        // in-memory scheduler
        _scheduler = StdSchedulerFactory.GetDefaultScheduler().GetAwaiter().GetResult();

        //Начало изменений
        // QuartzAgentJob читает зависимости из SchedulerContext.
        // Кладём их один раз при инициализации.
        _scheduler.Context.Put("agentManager", _agentManager);
        _scheduler.Context.Put("eventBus", _eventBus);
        //Конец изменений
    }

    // -------------------------
    // Lifecycle
    // -------------------------

    public async Task StartAsync()
    {
        if (!_scheduler.IsStarted)
            await _scheduler.Start();
    }

    public async Task ShutdownAsync()
    {
        if (!_scheduler.IsShutdown)
            await _scheduler.Shutdown(waitForJobsToComplete: false);
    }

    // -------------------------
    // Registration
    // -------------------------

    public async Task RegisterAgentsAsync()
    {
        var snapshot = _parameters.GetSnapshot();

        foreach (var (agentId, agentCfg) in snapshot.Agents)
        {
            if (!agentCfg.Enabled || agentCfg.Schedule is null)
                continue;

            await RegisterAgentAsync(agentId, agentCfg.Schedule);
        }
    }

    private async Task RegisterAgentAsync(string agentId, ScheduleBlock schedule)
    {
        var jobKey = new JobKey(agentId, "agents");

        if (await _scheduler.CheckExists(jobKey))
            return;

        var job = JobBuilder.Create<QuartzAgentJob>()
            .WithIdentity(jobKey)
            .UsingJobData("agentId", agentId)
            .Build();

        var trigger = QuartzScheduleReporter.BuildTrigger(agentId, schedule);

        await _scheduler.ScheduleJob(job, trigger);
    }

    // -------------------------
    // Query API (for UI)
    // -------------------------

    public async Task<DateTimeOffset?> GetNextRunAsync(string agentId)
    {
        var triggers = await _scheduler.GetTriggersOfJob(new JobKey(agentId, "agents"));
        var trigger = triggers.FirstOrDefault();

        return trigger?.GetNextFireTimeUtc()?.ToLocalTime();
    }

    public async Task<string> GetScheduleDescriptionAsync(string agentId)
    {
        var triggers = await _scheduler.GetTriggersOfJob(new JobKey(agentId, "agents"));
        var trigger = triggers.FirstOrDefault();

        return QuartzScheduleReporter.Describe(trigger);
    }

    // summary: QuartzSchedulerService — единая точка управления Quartz (in-memory): старт/стоп scheduler,
    //          регистрация job/trigger из parameters.json и выдача данных расписания (next run + описание) для UI через сервис.
    //          Прокидывает зависимости (AgentManager, EventBus) в SchedulerContext для QuartzAgentJob.
}
