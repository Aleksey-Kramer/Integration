using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Integration.Core;
using Integration.Models;
//Начало изменений
using Integration.Repositories;
//Конец изменений
using Integration.Services;

namespace Integration.Agents.UzStandart;

public sealed class UzStandartAgent : IAgent
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ParametersStore _parameters;
    private readonly UzStandartClient _client;

    //Начало изменений
    private readonly RuntimeStateStore _runtimeState;
    private readonly DbHealthcheckRepository _dbHealthcheck;
    private readonly string _dbProfileKey;
    //Конец изменений

    private UzStandartState _state;

    public string Id => "uzstandart";
    public string DisplayName { get; }

    public AgentStatus Status { get; private set; } = AgentStatus.stopped;

    //Начало изменений
    public UzStandartAgent(
        ParametersStore parameters,
        UzStandartClient client,
        RuntimeStateStore runtimeState,
        DbHealthcheckRepository dbHealthcheck)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
        _dbHealthcheck = dbHealthcheck ?? throw new ArgumentNullException(nameof(dbHealthcheck));

        // Берём настройки из parameters.json
        var snapshot = _parameters.GetSnapshot();

        if (!snapshot.Agents.TryGetValue(Id, out var agentCfg))
            throw new InvalidOperationException($"Agent config '{Id}' not found in parameters.json");

        DisplayName = agentCfg.Display_Name ?? "TIMV UzStandart";

        _dbProfileKey = agentCfg.Db_Profile
                        ?? throw new InvalidOperationException($"Agent '{Id}': db_profile is missing in parameters.json");

        var startPage = agentCfg.Paging?.Start_Page ?? 1;
        var perPage = agentCfg.Paging?.Per_Page ?? 10;
        var maxPagesPerTick = agentCfg.Paging?.Max_Pages_Per_Tick ?? 1;

        _state = new UzStandartState(startPage, perPage, maxPagesPerTick);
    }
    //Конец изменений

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

            // DB healthcheck (cheap call) — always update runtime state (UI reads from it)
            await RunDbHealthcheckAsync(context).ConfigureAwait(false);

            var (resp, raw) = await _client.GetCertificatesAsync(page, perPage, context.CancellationToken)
                .ConfigureAwait(false);

            context.CancellationToken.ThrowIfCancellationRequested();

            // Базовая проверка логики API
            if (!resp.Success)
            {
                var msg = $"{DisplayName}: API returned success=false. status={resp.Status}, msg={resp.Msg}";
                _state.MarkError(msg);

                // Явно фиксируем ошибку API (без эвристики по логам)
                context.EventBus.PublishAgentApiStateChanged(
                    Id,
                    ApiConnectionStatus.error,
                    errorCode: AgentStatusErrors.api_success_false.ToString(),
                    errorMessage: resp.Msg ?? msg);

                context.EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.error, msg));
                context.EventBus.PublishAgent(new AgentLogEntry(Id, DateTimeOffset.Now, LogLevel.error, raw));
                return;
            }

            _state.UpdatePageTotal(resp.PageTotal);

            var itemsCount = resp.Data?.Count ?? 0;

            // Явно фиксируем успешный коннект к API
            context.EventBus.PublishAgentApiStateChanged(
                Id,
                ApiConnectionStatus.ok,
                errorCode: null,
                errorMessage: null);

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
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Отмена тика (Stop/Cancel) — это не ошибка API, статус не трогаем.
            var msg = $"{DisplayName}: tick canceled.";
            context.EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.warning, msg));
            context.EventBus.PublishAgent(new AgentLogEntry(Id, DateTimeOffset.Now, LogLevel.warning, msg));
        }
        catch (Exception ex)
        {
            var msg = $"{DisplayName}: tick error. {ex.GetType().Name}: {ex.Message}";
            _state.MarkError(msg);

            var code = MapErrorCode(ex);

            // Явно фиксируем ошибку API/инфраструктуры понятным кодом
            context.EventBus.PublishAgentApiStateChanged(
                Id,
                ApiConnectionStatus.error,
                errorCode: code.ToString(),
                errorMessage: ex.Message);

            context.EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.error, msg));
            context.EventBus.PublishAgent(new AgentLogEntry(Id, DateTimeOffset.Now, LogLevel.error, msg));
        }
    }

    //Начало изменений
    private async Task RunDbHealthcheckAsync(AgentTickContext context)
{
    try
    {
        var dbName = await _dbHealthcheck.GetDbNameAsync(_dbProfileKey).ConfigureAwait(false);

        _runtimeState.UpdateAgent(Id, s =>
        {
            var nowUtc = DateTimeOffset.UtcNow;

            // align with runtime_state.json db-block
            s.Db.ProfileKey = _dbProfileKey;
            s.Db.Connection_Name = _dbProfileKey; // пока так (человеческое имя можно подтянуть позже из parameters)
            s.Db.Db_Name = dbName;

            s.Db.State = ConnectionStateKind.ok;
            s.Db.Text = "Состояние: подключен";

            s.Db.Last_Success_At_Utc = nowUtc;
            s.Db.Last_Error_At_Utc = null;

            s.Db.Last_Error.Code = AgentStatusErrors.none;
            s.Db.Last_Error.Kind = null;
            s.Db.Last_Error.Message = null;
        });

        _runtimeState.Save();

        context.EventBus.PublishAgent(new AgentLogEntry(
            Id,
            DateTimeOffset.Now,
            LogLevel.info,
            $"db ok: {_dbProfileKey} | {dbName}"
        ));
    }
    catch (Exception ex)
    {
        var code = DbErrorMapper.Map(ex);

        _runtimeState.UpdateAgent(Id, s =>
        {
            var nowUtc = DateTimeOffset.UtcNow;

            s.Db.ProfileKey = _dbProfileKey;
            s.Db.Connection_Name = _dbProfileKey;
            s.Db.Db_Name = null;

            s.Db.State = ConnectionStateKind.error;
            s.Db.Text = code == AgentStatusErrors.none ? "Ошибка" : $"Ошибка: {code}";

            s.Db.Last_Error_At_Utc = nowUtc;

            s.Db.Last_Error.Code = code;
            s.Db.Last_Error.Kind = ex.GetType().Name;
            s.Db.Last_Error.Message = ex.Message;

            // общий last_error_* агента
            s.LastErrorAt = DateTimeOffset.Now;
            s.LastErrorMessage = $"db: {ex.GetType().Name}: {ex.Message}";
        });

        _runtimeState.Save();

        context.EventBus.PublishAgent(new AgentLogEntry(
            Id,
            DateTimeOffset.Now,
            LogLevel.error,
            $"db error ({code}): {ex.Message}"
        ));
    }
}
    //Конец изменений

    private static AgentStatusErrors MapErrorCode(Exception ex)
    {
        // нормализуем по "корню"
        var e = ex;
        while (e.InnerException is not null)
            e = e.InnerException;

        // --- сеть / API ---
        if (e is SocketException se)
        {
            return se.SocketErrorCode switch
            {
                SocketError.TimedOut =>
                    AgentStatusErrors.api_timeout,

                SocketError.ConnectionRefused =>
                    AgentStatusErrors.api_connection_failed,

                SocketError.HostNotFound =>
                    AgentStatusErrors.api_connection_failed,

                SocketError.NetworkUnreachable =>
                    AgentStatusErrors.api_connection_failed,

                SocketError.HostUnreachable =>
                    AgentStatusErrors.api_connection_failed,

                _ =>
                    AgentStatusErrors.api_connection_failed
            };
        }

        return e switch
        {
            // HttpClient оборачивает сетевые ошибки
            HttpRequestException =>
                AgentStatusErrors.api_http_error,

            // часто означает таймаут HttpClient (если не дошло до SocketException)
            TaskCanceledException =>
                AgentStatusErrors.api_timeout,

            JsonException =>
                AgentStatusErrors.data_parse_error,

            OperationCanceledException =>
                AgentStatusErrors.agent_canceled,

            _ =>
                AgentStatusErrors.unknown
        };
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

    // summary: Агент интеграции UzStandart. Выполняет запросы к API с пагинацией, логирует результаты
    //          и публикует явное состояние коннекта к API через EventBus (ok/error) с нормализованными кодами.
    //          Также выполняет DB healthcheck через integration_pack.get_db_name и пишет результат в runtime_state.
}
