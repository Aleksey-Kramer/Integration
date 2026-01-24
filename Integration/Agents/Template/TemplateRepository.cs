using System;
using System.Threading;
using System.Threading.Tasks;

namespace Integration.Agents.Template;

/// <summary>
/// Репозиторий шаблонного агента.
/// Отвечает за сохранение и загрузку данных,
/// полученных в ходе выполнения тиков.
/// 
/// В шаблоне реализован как заглушка:
/// при создании реального агента сюда
/// добавляется доступ к БД / файлам / API.
/// </summary>
public sealed class TemplateRepository
{
    public TemplateRepository()
    {
    }

    public Task SaveResultAsync(
        TemplateModels.TemplateResult result,
        CancellationToken cancellationToken)
    {
        if (result is null)
            throw new ArgumentNullException(nameof(result));

        // Заглушка:
        // здесь может быть INSERT/UPDATE в БД,
        // запись в файл или отправка в другое хранилище.
        return Task.CompletedTask;
    }

    // summary:
    // Репозиторий шаблонного агента.
    // Используется TemplateService для изоляции
    // бизнес-логики от инфраструктуры хранения данных.
}