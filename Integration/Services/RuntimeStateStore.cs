using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Integration.Services;

public sealed class RuntimeStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

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
                _snapshot = new RuntimeStateSnapshot();
                return _snapshot;
            }

            var json = File.ReadAllText(FilePath);
            _snapshot = JsonSerializer.Deserialize<RuntimeStateSnapshot>(json, JsonOptions)
                        ?? new RuntimeStateSnapshot();

            return _snapshot;
        }
    }

    public void Save()
    {
        lock (_lock)
        {
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
                state = new AgentRuntimeState();
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
                state = new AgentRuntimeState();
                _snapshot.Agents[agentId] = state;
            }

            mutate(state);
        }
    }
}
