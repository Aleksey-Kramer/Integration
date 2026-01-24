using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Integration.Services;

// summary: Загрузка/хранение parameters.json. Даёт потокобезопасный snapshot (GetSnapshot),
//          поддерживает Reload(), выполняет базовую валидацию структуры конфигурации.
public sealed class ParametersStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly object _lock = new();

    private ParametersSnapshot _snapshot;

    public string FileName { get; }
    public string FilePath { get; }

    public ParametersStore(string fileName = "parameters.json")
    {
        FileName = fileName;
        FilePath = Path.Combine(AppContext.BaseDirectory, fileName);

        _snapshot = LoadFromDisk(FilePath);
    }

    public ParametersSnapshot GetSnapshot()
    {
        lock (_lock)
            return _snapshot;
    }

    public ParametersSnapshot Reload()
    {
        var fresh = LoadFromDisk(FilePath);

        lock (_lock)
            _snapshot = fresh;

        return fresh;
    }

    private static ParametersSnapshot LoadFromDisk(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Parameters file not found: {path}", path);

        var json = File.ReadAllText(path);

        var model = JsonSerializer.Deserialize<ParametersSnapshot>(json, JsonOptions);
        if (model is null)
            throw new InvalidOperationException($"Parameters file '{path}' cannot be deserialized.");

        // Минимальная валидация (чтобы падать сразу и понятно)
        if (model.Services is null || model.Agents is null)
            throw new InvalidOperationException("parameters.json must contain 'services' and 'agents' sections.");

        return model;
    }
}

/// <summary>
/// Полный снимок parameters.json (минимальный набор полей под текущие задачи).
/// </summary>
// summary: DTO snapshot для parameters.json. Содержит секции app/http/logging, services и agents.
public sealed class ParametersSnapshot
{
    public AppBlock? App { get; init; }
    public HttpBlock? Http { get; init; }
    public LoggingBlock? Logging { get; init; }

    public Dictionary<string, ServiceBlock> Services { get; init; } = new();
    public Dictionary<string, AgentBlock> Agents { get; init; } = new();
}

// summary: Секция app (окружение и таймзона приложения).
public sealed class AppBlock
{
    public string? Env { get; init; }
    public string? Timezone { get; init; }
}

// summary: Секция http (общие таймауты для HttpClient).
public sealed class HttpBlock
{
    public int Timeout_Seconds { get; init; } = 30;
}

// summary: Секция logging (путь к файлу логов).
public sealed class LoggingBlock
{
    public string? File_Path { get; init; }
}

// summary: Универсальный блок сервиса из services.<key>. Используется для API-сервисов и DB-конфигурации.
public sealed class ServiceBlock
{
    // общие поля под разные сервисы; конкретику достанем через наследников/спец-модели позже
    public string? Base_Url { get; init; }
    public string? Endpoint { get; init; }
    public string? Auth_Bearer { get; init; }

    public int? Http_Timeout_Seconds { get; init; }

    // timv
    public string? Uuid { get; init; }
    public string? Key { get; init; }
    public string? Default_Method { get; init; }
    public string? Default_Table { get; init; }
    public int? Poll_Minutes { get; init; }
    public int? Is_Live { get; init; }

    // pharm agency
    public string? Gxp_Base_Url { get; init; }
    public string? Login { get; init; }
    public string? Password { get; init; }

    //Начало изменений
    // ===== DB (services.db) =====

    /// <summary>
    /// Шаблон строки подключения (используется, если profile.connection_string не задан).
    /// Пример: "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST={Address})(PORT={Port}))(CONNECT_DATA=(SERVICE_NAME={ServiceName})));User Id={Login};Password={Password}"
    /// </summary>
    public string? Connection_String_Template { get; init; }

    /// <summary>
    /// Профили подключений к БД (ключ -> набор параметров/override).
    /// </summary>
    public Dictionary<string, DbProfileBlock>? Profiles { get; init; }

    /// <summary>
    /// Таймаут по умолчанию для DB-операций (секунды), если нужно использовать в репозиториях.
    /// </summary>
    public int? Default_Timeout_Seconds { get; init; }
    //Конец изменений
}

// summary: Настройки одного агента из секции agents.<id>.
public sealed class AgentBlock
{
    public bool Enabled { get; init; } = true;
    public string? Service { get; init; }
    public string? Display_Name { get; init; }

    //Начало изменений
    /// <summary>
    /// Ключ профиля БД из services.db.profiles (например "eko_test").
    /// </summary>
    public string? Db_Profile { get; init; }
    //Конец изменений

    public ScheduleBlock? Schedule { get; init; }
    public PagingBlock? Paging { get; init; }
}

// summary: Настройки расписания агента. Интерпретация приоритета — в Scheduling-слое.
public sealed class ScheduleBlock
{
    // Один из вариантов (приоритет будет обрабатываться в Scheduling-слое):
    // - every_seconds
    // - every_minutes
    // - every_hours
    // - daily_at (HH:mm)

    public int? Every_Seconds { get; init; } = null;
    public int? Every_Minutes { get; init; } = null;
    public int? Every_Hours { get; init; } = null;

    /// <summary>
    /// Ежедневный запуск в локальном времени (формат "HH:mm", например "22:00")
    /// </summary>
    public string? Daily_At { get; init; } = null;
}

// summary: Настройки пагинации для агента (старт/размер страницы/лимит страниц на тик).
public sealed class PagingBlock
{
    public int Start_Page { get; init; } = 1;
    public int Per_Page { get; init; } = 10;
    public int Max_Pages_Per_Tick { get; init; } = 1;
}

//Начало изменений
// summary: Профиль подключения к Oracle. Может содержать либо готовый connection_string,
//          либо набор частей для сборки по Connection_String_Template.
public sealed class DbProfileBlock
{
    /// <summary>
    /// Отображаемое имя профиля (например "Eko").
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Уровень/окружение (например "test" / "real") — для отображения в UI.
    /// </summary>
    public string? Lvl { get; init; }

    /// <summary>
    /// Готовая строка подключения (если задана — используется напрямую).
    /// </summary>
    public string? Connection_String { get; init; }

    // parts for template build
    public string? Address { get; init; }
    public int? Port { get; init; }
    public string? ServiceName { get; init; }
    public string? Login { get; init; }
    public string? Password { get; init; }
}
//Конец изменений
