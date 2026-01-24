using System;
using System.Collections.Generic;

namespace Integration.Services;

//Начало изменений
/// <summary>
/// Root snapshot of runtime_state.json.
/// Contains schema metadata and per-agent runtime blocks.
/// </summary>
//Конец изменений
public sealed class RuntimeStateSnapshot
{
    //Начало изменений
    /// <summary>
    /// Schema version of runtime_state.json.
    /// </summary>
    public int Schema_Version { get; set; } = 1;

    /// <summary>
    /// When snapshot was last updated (UTC).
    /// </summary>
    public DateTimeOffset Updated_At_Utc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Agents runtime blocks by agentId key.
    /// </summary>
    //Конец изменений
    public Dictionary<string, AgentRuntimeState> Agents { get; init; } = new();

    //Начало изменений
    public static RuntimeStateSnapshot CreateEmpty()
        => new()
        {
            Schema_Version = 1,
            Updated_At_Utc = DateTimeOffset.UtcNow,
            Agents = new Dictionary<string, AgentRuntimeState>()
        };
    //Конец изменений

    // summary: Корневой snapshot runtime_state.json.
    //          Хранит schema_version, updated_at_utc и словарь runtime-состояний агентов (agents[agentId]).
}