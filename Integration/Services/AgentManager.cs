using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Integration.Core;

namespace Integration.Services;

public sealed class AgentManager
{
    private readonly IEventBus _bus;

    // Agent registry
    private readonly ConcurrentDictionary<string, IAgent> _agents = new(StringComparer.OrdinalIgnoreCase);

    // Per-agent runtime (cts + gating)
    private readonly ConcurrentDictionary<string, AgentRuntime> _runtime = new(StringComparer.OrdinalIgnoreCase);

    public AgentManager(IEventBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    public IReadOnlyList<IAgent> GetAgents()
        => _agents.Values.OrderBy(a => a.DisplayName).ToList();

    public bool TryGetAgent(string agentId, out IAgent? agent)
        => _agents.TryGetValue(agentId, out agent);

    public void Register(IAgent agent)
    {
        if (agent is null) throw new ArgumentNullException(nameof(agent));
        if (string.IsNullOrWhiteSpace(agent.Id))
            throw new ArgumentException("Agent Id is required.", nameof(agent));

        if (!_agents.TryAdd(agent.Id, agent))
            throw new InvalidOperationException($"Agent with id '{agent.Id}' is already registered.");

        _runtime.TryAdd(agent.Id, new AgentRuntime());

        // Initial status broadcast
        _bus.PublishAgentStatus(agent.Id, agent.Status);
        _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Agent registered: {agent.DisplayName} ({agent.Id})"));
    }

    // -------------------------
    // Control: Single agent
    // -------------------------

    public async Task StartNowAsync(string agentId)
    {
        if (!TryGetAgent(agentId, out var agent) || agent is null)
        {
            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"StartNow: unknown agent '{agentId}'"));
            return;
        }

        var rt = _runtime[agentId];

        // Do not allow overlapping ticks
        if (!rt.TryEnterTick())
        {
            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"StartNow ignored (already running): {agent.DisplayName}"));
            return;
        }

        try
        {
            // If agent is stopped, StartNow still runs one tick (по твоей логике можно запретить — скажешь).
            /*if (agent.Status == AgentStatus.paused)
            {
                _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"StartNow while paused: {agent.DisplayName}"));
            }
            if (agent.Status == AgentStatus.stopped)
            {
                _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"StartNow while stopped: {agent.DisplayName}"));
            }*/
            
            //Начало изменений

            // IMPORTANT: StartNow НЕ должен менять статус агента.
            // - Ручной запуск может быть отдельной командой пользователя.
            // - Quartz Job должен уважать paused/stopped и не "воскрешать" агента.
            // Если хочешь иной сценарий для ручного запуска — разделим методы: StartNowAsync(forceResume: true/false).
            if (agent.Status == AgentStatus.paused)
            {
                _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"Tick skipped (paused): {agent.DisplayName}"));
                _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.warning, "Tick skipped: agent is paused."));
                _bus.PublishAgentScheduleChanged(agent.Id);
                return;
            }

            if (agent.Status == AgentStatus.stopped)
            {
                _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"Tick skipped (stopped): {agent.DisplayName}"));
                _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.warning, "Tick skipped: agent is stopped."));
                _bus.PublishAgentScheduleChanged(agent.Id);
                return;
            }

            var cts = new CancellationTokenSource();
            rt.SetCurrentTickCts(cts);

            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Tick started: {agent.DisplayName}"));
            _bus.PublishAgentStatus(agent.Id, agent.Status);

            //Конец изменений

            var ctx = new AgentTickContext
            {
                CancellationToken = cts.Token,
                EventBus = _bus,
                StartedAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid().ToString("N")
            };

            await agent.ExecuteTickAsync(ctx).ConfigureAwait(false);

            //Начало изменений
            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Tick finished: {agent.DisplayName}"));
            _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.info, $"Tick finished: {agent.DisplayName}"));

            // после завершения тика next-run в Quartz сдвинулся → просим UI обновиться
            _bus.PublishAgentScheduleChanged(agent.Id);
            //Конец изменений
        }
        catch (OperationCanceledException)
        {
            //Начало изменений
            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"Tick cancelled: {agent.DisplayName}"));
            _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.warning, $"Tick cancelled: {agent.DisplayName}"));

            // даже при cancel — next-run мог измениться (особенно при misfire/коалесценции)
            _bus.PublishAgentScheduleChanged(agent.Id);
            //Конец изменений

        }
        catch (Exception ex)
        {
            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.error, $"Tick error: {agent.DisplayName}. {ex.GetType().Name}: {ex.Message}"));
            _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.error, $"Tick error: {agent.DisplayName}. {ex.GetType().Name}: {ex.Message}"));
            // Базовое безопасное поведение: остановить агента при необработанной ошибке.
            // Если хочешь иной сценарий (оставлять active/paused) — скажи.
            //Начало изменений
            try
            {
                agent.Stop();
                _bus.PublishAgentApiStateChanged(agent.Id, ApiConnectionStatus.unknown, errorCode: null, errorMessage: null);
                _bus.PublishAgentStatus(agent.Id, agent.Status);

                // статус изменился → UI должен перечитать расписание/next-run
                _bus.PublishAgentScheduleChanged(agent.Id);
            }
            catch
            {
                // ignore secondary failures
            }
            //Конец изменений
        }
        finally
        {
            rt.ClearCurrentTickCts();
            rt.ExitTick();
        }
    }

    public void Pause(string agentId)
    {
        if (!TryGetAgent(agentId, out var agent) || agent is null) return;
        //Начало изменений
        agent.Pause();
        _bus.PublishAgentStatus(agent.Id, agent.Status);
        _bus.PublishAgentScheduleChanged(agent.Id);
        _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Paused: {agent.DisplayName}"));
        //Конец изменений
    }

    public void Resume(string agentId)
    {
        if (!TryGetAgent(agentId, out var agent) || agent is null) return;

        agent.Resume();
        _bus.PublishAgentStatus(agent.Id, agent.Status);
        _bus.PublishAgentScheduleChanged(agent.Id);
        _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Resumed: {agent.DisplayName}"));
    }

    public void Stop(string agentId)
    {
        if (!TryGetAgent(agentId, out var agent) || agent is null) return;

        // cancel current tick (if any)
        if (_runtime.TryGetValue(agentId, out var rt))
        {
            rt.CancelCurrentTick();
        }

        agent.Stop();
        _bus.PublishAgentApiStateChanged(agent.Id, ApiConnectionStatus.unknown, errorCode: null, errorMessage: null);
        _bus.PublishAgentStatus(agent.Id, agent.Status);
        _bus.PublishAgentScheduleChanged(agent.Id);
        _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Stopped: {agent.DisplayName}"));
    }

    // -------------------------
    // Control: All agents
    // -------------------------

    public void PauseAll()
    {
        foreach (var agent in _agents.Values)
            agent.Pause();

        foreach (var agent in _agents.Values)
        {
            _bus.PublishAgentStatus(agent.Id, agent.Status);
            _bus.PublishAgentScheduleChanged(agent.Id);
        }
        _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, "Paused all agents."));
    }

    public void ResumeAll()
    {
        foreach (var agent in _agents.Values)
            agent.Resume();

        foreach (var agent in _agents.Values)
        {
            _bus.PublishAgentStatus(agent.Id, agent.Status);
            _bus.PublishAgentScheduleChanged(agent.Id);
        }

        _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, "Resumed all agents."));
    }

    public void StopAll()
    {
        foreach (var id in _agents.Keys)
        {
            if (_runtime.TryGetValue(id, out var rt))
                rt.CancelCurrentTick();
        }

        foreach (var agent in _agents.Values)
        {
            agent.Stop();
            _bus.PublishAgentApiStateChanged(agent.Id, ApiConnectionStatus.unknown, errorCode: null, errorMessage: null);
        }

        foreach (var agent in _agents.Values)
        {
            _bus.PublishAgentStatus(agent.Id, agent.Status);
            _bus.PublishAgentScheduleChanged(agent.Id);
        }

        _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, "Stopped all agents."));
    }
    
    // summary: Оркестратор агентов. Регистрирует агентов, управляет их жизненным циклом,
    //          предотвращает параллельные тики, умеет cancel текущего тика и публикует логи/статусы через EventBus.

    // -------------------------
    // Internal runtime helper
    // -------------------------

    private sealed class AgentRuntime
    {
        private int _isRunning; // 0/1
        private CancellationTokenSource? _currentTickCts;

        public bool TryEnterTick()
            => Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0;

        public void ExitTick()
            => Interlocked.Exchange(ref _isRunning, 0);

        public void SetCurrentTickCts(CancellationTokenSource cts)
        {
            // replace any old CTS defensively
            var old = Interlocked.Exchange(ref _currentTickCts, cts);
            try { old?.Dispose(); } catch { /* ignore */ }
        }

        public void ClearCurrentTickCts()
        {
            var old = Interlocked.Exchange(ref _currentTickCts, null);
            try { old?.Dispose(); } catch { /* ignore */ }
        }

        public void CancelCurrentTick()
        {
            try
            {
                _currentTickCts?.Cancel();
            }
            catch
            {
                // ignore
            }
        }
    }

}
