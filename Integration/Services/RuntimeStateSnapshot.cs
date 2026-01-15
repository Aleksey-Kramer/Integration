using System.Collections.Generic;

namespace Integration.Services;

public sealed class RuntimeStateSnapshot
{
    public Dictionary<string, AgentRuntimeState> Agents { get; init; } = new();
}