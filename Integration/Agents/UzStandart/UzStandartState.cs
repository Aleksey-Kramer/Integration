namespace Integration.Agents.UzStandart;

/// <summary>
/// Runtime-состояние агента UzStandart.
/// Хранится в памяти, используется между тиками.
/// </summary>
public sealed class UzStandartState
{
    /// <summary>
    /// Текущая страница, с которой будет выполнен следующий тик.
    /// </summary>
    public int CurrentPage { get; private set; }

    /// <summary>
    /// Сколько записей запрашивать на страницу.
    /// </summary>
    public int PerPage { get; private set; }

    /// <summary>
    /// Общее количество страниц (становится известно после первого ответа).
    /// </summary>
    public int? PageTotal { get; private set; }

    /// <summary>
    /// Максимальное количество страниц, обрабатываемых за один тик.
    /// Для MVP = 1.
    /// </summary>
    public int MaxPagesPerTick { get; private set; }

    /// <summary>
    /// Последняя успешно обработанная страница.
    /// </summary>
    public int? LastProcessedPage { get; private set; }

    /// <summary>
    /// Последняя ошибка (если была).
    /// </summary>
    public string? LastError { get; private set; }

    public UzStandartState(int startPage, int perPage, int maxPagesPerTick)
    {
        CurrentPage = startPage;
        PerPage = perPage;
        MaxPagesPerTick = maxPagesPerTick;
    }

    /// <summary>
    /// Обновить информацию о количестве страниц из ответа API.
    /// </summary>
    public void UpdatePageTotal(int pageTotal)
    {
        if (pageTotal > 0)
            PageTotal = pageTotal;
    }

    /// <summary>
    /// Зафиксировать успешную обработку страницы и перейти к следующей.
    /// </summary>
    public void MarkPageProcessed(int page)
    {
        LastProcessedPage = page;
        CurrentPage = page + 1;
        LastError = null;
    }

    /// <summary>
    /// Зафиксировать ошибку (страница не считается обработанной).
    /// </summary>
    public void MarkError(string error)
    {
        LastError = error;
    }

    /// <summary>
    /// Сброс состояния (например, при Stop/Restart).
    /// </summary>
    public void Reset(int startPage)
    {
        CurrentPage = startPage;
        LastProcessedPage = null;
        PageTotal = null;
        LastError = null;
    }
}
