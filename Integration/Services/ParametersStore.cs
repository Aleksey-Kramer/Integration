using System;
using System.IO;
using System.Text.Json;

namespace Integration.Services;

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
public sealed class ParametersSnapshot
{
    public AppBlock? App { get; init; }
    public HttpBlock? Http { get; init; }
    public LoggingBlock? Logging { get; init; }

    public Dictionary<string, ServiceBlock> Services { get; init; } = new();
    public Dictionary<string, AgentBlock> Agents { get; init; } = new();
}

public sealed class AppBlock
{
    public string? Env { get; init; }
    public string? Timezone { get; init; }
}

public sealed class HttpBlock
{
    public int Timeout_Seconds { get; init; } = 30;
}

public sealed class LoggingBlock
{
    public string? File_Path { get; init; }
}

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
}

public sealed class AgentBlock
{
    public bool Enabled { get; init; } = true;
    public string? Service { get; init; }
    public string? Display_Name { get; init; }

    public ScheduleBlock? Schedule { get; init; }
    public PagingBlock? Paging { get; init; }
}

public sealed class ScheduleBlock
{
    public int Every_Seconds { get; init; } = 600;
}

public sealed class PagingBlock
{
    public int Start_Page { get; init; } = 1;
    public int Per_Page { get; init; } = 10;
    public int Max_Pages_Per_Tick { get; init; } = 1;
}
