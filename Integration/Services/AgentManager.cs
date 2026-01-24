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

    /// <summary>
    /// Manual запуск: должен "вытаскивать" из paused (Resume) и выполнять тик.
    /// stopped — по-прежнему не запускаем (чтобы не "воскрешать" агента без явного Resume).
    /// </summary>
    public Task StartNowAsync(string agentId)
        => RunTickAsync(agentId, externalToken: null, source: "manual", forceResumeIfPaused: true);

    /// <summary>
    /// Запуск из Quartz: уважает paused/stopped (не меняет статус).
    /// </summary>
    public Task ExecuteScheduledAsync(string agentId, CancellationToken quartzToken)
        => RunTickAsync(agentId, externalToken: quartzToken, source: "quartz", forceResumeIfPaused: false);

    private async Task RunTickAsync(string agentId, CancellationToken? externalToken, string source, bool forceResumeIfPaused)
    {
        if (!TryGetAgent(agentId, out var agent) || agent is null)
        {
            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"{source}: unknown agent '{agentId}'"));
            return;
        }

        var rt = _runtime[agentId];

        // Do not allow overlapping ticks
        if (!rt.TryEnterTick())
        {
            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"{source}: ignored (already running): {agent.DisplayName}"));
            return;
        }

        CancellationTokenSource? linkedCts = null;

        try
        {
            // paused
            if (agent.Status == AgentStatus.paused)
            {
                if (forceResumeIfPaused)
                {
                    agent.Resume();
                    _bus.PublishAgentStatus(agent.Id, agent.Status);
                    _bus.PublishAgentScheduleChanged(agent.Id);
                    _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Resumed by StartNow: {agent.DisplayName}"));
                }
                else
                {
                    _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"Tick skipped (paused): {agent.DisplayName}"));
                    _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.warning, "Tick skipped: agent is paused."));
                    _bus.PublishAgentScheduleChanged(agent.Id);
                    return;
                }
                if (string.Equals(source, "manual", StringComparison.OrdinalIgnoreCase))
                {
                    agent.Resume();
                    _bus.PublishAgentStatus(agent.Id, agent.Status);
                    _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Resumed by StartNow: {agent.DisplayName}"));
                }
                else
                {
                    _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"Tick skipped (paused): {agent.DisplayName}"));
                    _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.warning, "Tick skipped: agent is paused."));
                    _bus.PublishAgentScheduleChanged(agent.Id);
                    return;
                }
            }

            // stopped — не выполняем
            if (agent.Status == AgentStatus.stopped)
            {
                _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"Tick skipped (stopped): {agent.DisplayName}"));
                _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.warning, "Tick skipped: agent is stopped."));
                _bus.PublishAgentScheduleChanged(agent.Id);
                return;
            }

            linkedCts = externalToken.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(externalToken.Value)
                : new CancellationTokenSource();

            rt.SetCurrentTickCts(linkedCts);

            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Tick started [{source}]: {agent.DisplayName}"));
            _bus.PublishAgentStatus(agent.Id, agent.Status);

            var ctx = new AgentTickContext
            {
                CancellationToken = linkedCts.Token,
                EventBus = _bus,
                StartedAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid().ToString("N")
            };

            await agent.ExecuteTickAsync(ctx).ConfigureAwait(false);

            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Tick finished [{source}]: {agent.DisplayName}"));
            _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.info, $"Tick finished [{source}]: {agent.DisplayName}"));
            _bus.PublishAgentScheduleChanged(agent.Id);
        }
        catch (OperationCanceledException)
        {
            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, $"Tick cancelled [{source}]: {agent.DisplayName}"));
            _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.warning, $"Tick cancelled [{source}]: {agent.DisplayName}"));
            _bus.PublishAgentScheduleChanged(agent.Id);
        }
        catch (Exception ex)
        {
            _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.error, $"Tick error [{source}]: {agent.DisplayName}. {ex.GetType().Name}: {ex.Message}"));
            _bus.PublishAgent(new AgentLogEntry(agent.Id, DateTimeOffset.Now, LogLevel.error, $"Tick error [{source}]: {agent.DisplayName}. {ex.GetType().Name}: {ex.Message}"));

            try
            {
                agent.Stop();
                _bus.PublishAgentApiStateChanged(agent.Id, ApiConnectionStatus.unknown, errorCode: null, errorMessage: null);
                _bus.PublishAgentStatus(agent.Id, agent.Status);
                _bus.PublishAgentScheduleChanged(agent.Id);
            }
            catch { /* ignore */ }
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

        agent.Pause();
        _bus.PublishAgentStatus(agent.Id, agent.Status);
        _bus.PublishAgentScheduleChanged(agent.Id);
        _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Paused: {agent.DisplayName}"));
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
            try { _currentTickCts?.Cancel(); }
            catch { /* ignore */ }
        }
    }
}
