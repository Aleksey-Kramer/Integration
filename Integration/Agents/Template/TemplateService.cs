using System;
using System.Threading;
using System.Threading.Tasks;
using Integration.Core;

namespace Integration.Agents.Template;

/// <summary>
/// Сервис бизнес-логики шаблонного агента.
/// Инкапсулирует сценарий одного тика:
///  - подготовка запроса
///  - вызов клиента
///  - обработка результата
/// Не содержит UI-логики и прямого доступа к хранилищам.
/// </summary>
public sealed class TemplateService
{
    private readonly TemplateClient _client;
    private readonly TemplateRepository _repository;
    private readonly IEventBus _bus;

    public TemplateService(
        TemplateClient client,
        TemplateRepository repository,
        IEventBus bus)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    public async Task<TemplateModels.TemplateResult> ExecuteAsync(
        TemplateModels.TemplateRequest request,
        CancellationToken cancellationToken)
    {
        _bus.PublishGlobal(new LogEntry(
            DateTimeOffset.Now,
            LogLevel.info,
            "TemplateService: execution started."));

        // 1. Вызов внешнего клиента (заглушка)
        var response = await _client.SendAsync(request, cancellationToken)
                                    .ConfigureAwait(false);

        // 2. Преобразование ответа в результат
        var result = new TemplateModels.TemplateResult
        {
            IsProcessed = response.Success,
            ProcessedAt = DateTimeOffset.UtcNow,
            Details = response.Message
        };

        // 3. Сохранение результата (пример)
        await _repository.SaveResultAsync(result, cancellationToken)
                         .ConfigureAwait(false);

        _bus.PublishGlobal(new LogEntry(
            DateTimeOffset.Now,
            LogLevel.info,
            "TemplateService: execution finished."));

        return result;
    }

    // summary:
    // Основной сервис шаблонного агента.
    // Используется TemplateAgent внутри ExecuteTickAsync
    // как единая точка бизнес-логики.
}
