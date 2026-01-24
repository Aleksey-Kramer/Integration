using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Integration.Services;

public sealed class RuntimeStateStore
{
    //Начало изменений
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };
    //Конец изменений

    private readonly object _lock = new();

    private RuntimeStateSnapshot _snapshot = new();

    public string FilePath { get; }

    public RuntimeStateStore(string fileName = "runtime_state.json")
    {
        FilePath = Path.Combine(AppContext.BaseDirectory, fileName);
    }

    // ===== Load / Save =====

    public RuntimeStateSnapshot Reload()
    {
        lock (_lock)
        {
            if (!File.Exists(FilePath))
            {
                _snapshot = RuntimeStateSnapshot.CreateEmpty();
                return _snapshot;
            }

            var json = File.ReadAllText(FilePath);

            _snapshot = JsonSerializer.Deserialize<RuntimeStateSnapshot>(json, JsonOptions)
                        ?? RuntimeStateSnapshot.CreateEmpty();

            return _snapshot;
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            //Начало изменений
            _snapshot.Updated_At_Utc = DateTimeOffset.UtcNow;
            //Конец изменений

            var json = JsonSerializer.Serialize(_snapshot, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
    }

    // ===== Access API =====

    public AgentRuntimeState GetAgent(string agentId)
    {
        lock (_lock)
        {
            if (!_snapshot.Agents.TryGetValue(agentId, out var state))
            {
                state = AgentRuntimeState.CreateEmpty();
                _snapshot.Agents[agentId] = state;
            }

            return state;
        }
    }

    public void UpdateAgent(string agentId, Action<AgentRuntimeState> mutate)
    {
        lock (_lock)
        {
            if (!_snapshot.Agents.TryGetValue(agentId, out var state))
            {
                state = AgentRuntimeState.CreateEmpty();
                _snapshot.Agents[agentId] = state;
            }

            mutate(state);
        }
    }

    // summary: In-memory хранилище runtime-состояния агентов.
    //          Поддерживает schema_version, updated_at_utc и string-enum JSON.
    //          Все enum сериализуются в человекочитаемом виде (unknown/ok/error).
}
