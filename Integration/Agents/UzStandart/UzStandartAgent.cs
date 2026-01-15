using System;
using System.Text.Json;
using System.Threading.Tasks;
using Integration.Core;
using Integration.Services;

namespace Integration.Agents.UzStandart;

public sealed class UzStandartAgent : IAgent
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true
    };

    private readonly ParametersStore _parameters;
    private readonly UzStandartClient _client;

    private UzStandartState _state;

    public string Id => "uzstandart";
    public string DisplayName { get; }

    public AgentStatus Status { get; private set; } = AgentStatus.stopped;

    public UzStandartAgent(ParametersStore parameters, UzStandartClient client)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _client = client ?? throw new ArgumentNullException(nameof(client));

        // Берём настройки из parameters.json
        var snapshot = _parameters.GetSnapshot();

        if (!snapshot.Agents.TryGetValue(Id, out var agentCfg))
            throw new InvalidOperationException($"Agent config '{Id}' not found in parameters.json");

        DisplayName = agentCfg.Display_Name ?? "TIMV UzStandart";

        var startPage = agentCfg.Paging?.Start_Page ?? 1;
        var perPage = agentCfg.Paging?.Per_Page ?? 10;
        var maxPagesPerTick = agentCfg.Paging?.Max_Pages_Per_Tick ?? 1;

        _state = new UzStandartState(startPage, perPage, maxPagesPerTick);
    }

    public void Pause()
    {
        if (Status == AgentStatus.stopped)
            return;

        Status = AgentStatus.paused;
    }

    public void Resume()
    {
        Status = AgentStatus.active;
    }

    public void Stop()
    {
        Status = AgentStatus.stopped;

        // При stop возвращаемся к start_page (чтобы сравнивать с Postman с начала).
        // Позже можно сделать "continue from last" через файл/БД.
        var snapshot = _parameters.GetSnapshot();
        if (snapshot.Agents.TryGetValue(Id, out var agentCfg))
        {
            var startPage = agentCfg.Paging?.Start_Page ?? 1;
            _state.Reset(startPage);
        }
    }

    public async Task ExecuteTickAsync(AgentTickContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Если stopped/paused — тик не выполняем
        if (Status != AgentStatus.active)
        {
            context.EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning,
                $"{DisplayName}: tick skipped (status={Status})."));
            context.EventBus.PublishAgent(new AgentLogEntry(Id, DateTimeOffset.Now, LogLevel.warning,
                $"tick skipped (status={Status})."));

            return;
        }

        var page = _state.CurrentPage;
        var perPage = _state.PerPage;

        context.EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info,
            $"{DisplayName}: tick start (page={page}, per_page={perPage})."));
        
        context.EventBus.PublishAgent(new AgentLogEntry(Id, DateTimeOffset.Now, LogLevel.info,
            $"tick start (page={page}, per_page={perPage})."));

        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var (resp, raw) = await _client.GetCertificatesAsync(page, perPage, context.CancellationToken)
                .ConfigureAwait(false);

            context.CancellationToken.ThrowIfCancellationRequested();

            // Базовая проверка логики API
            if (!resp.Success)
            {
                var msg = $"{DisplayName}: API returned success=false. status={resp.Status}, msg={resp.Msg}";
                _state.MarkError(msg);

                context.EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.error, msg));
                context.EventBus.PublishAgent(new AgentLogEntry(Id, DateTimeOffset.Now, LogLevel.error, raw));
                return;
            }

            _state.UpdatePageTotal(resp.PageTotal);

            var itemsCount = resp.Data?.Count ?? 0;

            // Global log — коротко
            context.EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info,
                $"{DisplayName}: page {page} received, items {itemsCount}."));

            // Agent details log — строка + полный JSON (pretty)
            context.EventBus.PublishAgent(new AgentLogEntry(Id, DateTimeOffset.Now, LogLevel.info,
                $"page {page}: items {itemsCount}"));

            // pretty raw (если raw уже json, просто форматируем; если вдруг мусор — выводим как есть)
            var pretty = TryPrettifyJson(raw);
            context.EventBus.PublishAgent(new AgentLogEntry(Id, DateTimeOffset.Now, LogLevel.info, pretty));

            // commit: страница обработана → двигаемся дальше
            _state.MarkPageProcessed(page);
        }
        catch (Exception ex)
        {
            var msg = $"{DisplayName}: tick error. {ex.GetType().Name}: {ex.Message}";
            _state.MarkError(msg);

            context.EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.error, msg));
            context.EventBus.PublishAgent(new AgentLogEntry(Id, DateTimeOffset.Now, LogLevel.error, msg));
        }
    }

    private static string TryPrettifyJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc.RootElement, PrettyJson);
        }
        catch
        {
            return raw;
        }
    }

    /// <summary>
    /// Для первоначального запуска — вызови это один раз, например после регистрации агента.
    /// </summary>
    public void Activate()
    {
        if (Status == AgentStatus.stopped)
            Status = AgentStatus.active;
    }
}
