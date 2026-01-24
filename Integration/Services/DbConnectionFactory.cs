using System;
using System.Collections.Generic;
using Oracle.ManagedDataAccess.Client;

namespace Integration.Services;

/// <summary>
/// Factory for creating Oracle DB connections based on parameters.json.
/// 
/// Responsibilities:
/// - Resolve DB profile by key (services["db"])
/// - Build connection string (raw override OR template + parts)
/// - Return a NEW OracleConnection per call (pooling is handled by ODP.NET)
/// 
/// IMPORTANT:
/// - Connections must NOT be cached as OracleConnection instances
/// - Always create/open connection inside method usage
/// </summary>
// summary: Фабрика OracleConnection. При старте собирает и кеширует только строки подключения (profileKey->connectionString).
//          Create() каждый раз возвращает новый OracleConnection (пул соединений делает это дешёвым).
public sealed class DbConnectionFactory
{
    private readonly ParametersStore _parameters;

    // cache of resolved connection strings (profileKey -> connectionString)
    private readonly Dictionary<string, string> _connectionStrings = new();

    public DbConnectionFactory(ParametersStore parameters)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        BuildConnectionStrings();
    }

    /// <summary>
    /// Create a new OracleConnection for given DB profile.
    /// Caller is responsible for Open() and Dispose().
    /// </summary>
    public OracleConnection Create(string profileKey)
    {
        if (string.IsNullOrWhiteSpace(profileKey))
            throw new ArgumentException("DB profile key is empty.", nameof(profileKey));

        if (!_connectionStrings.TryGetValue(profileKey, out var connectionString))
            throw new InvalidOperationException($"DB profile '{profileKey}' not found.");

        return new OracleConnection(connectionString);
    }

    /// <summary>
    /// Resolve and cache all connection strings at startup.
    /// </summary>
    private void BuildConnectionStrings()
    {
        //Начало изменений
        var snapshot = _parameters.GetSnapshot();

        if (!snapshot.Services.TryGetValue("db", out var db) || db is null)
            throw new InvalidOperationException("services.db section is missing in parameters.json.");

        if (db.Profiles is null || db.Profiles.Count == 0)
            throw new InvalidOperationException("services.db.profiles section is missing or empty in parameters.json.");
        //Конец изменений

        foreach (var (profileKey, profile) in db.Profiles)
        {
            string connectionString;

            // 1) raw override
            //Начало изменений
            if (!string.IsNullOrWhiteSpace(profile.Connection_String))
            {
                connectionString = profile.Connection_String;
            }
            // 2) build from template + parts
            else
            {
                if (string.IsNullOrWhiteSpace(db.Connection_String_Template))
                    throw new InvalidOperationException("services.db.connection_string_template is not defined.");

                if (string.IsNullOrWhiteSpace(profile.Address) ||
                    !profile.Port.HasValue ||
                    string.IsNullOrWhiteSpace(profile.Login) ||
                    string.IsNullOrWhiteSpace(profile.Password))
                {
                    throw new InvalidOperationException(
                        $"services.db.profiles['{profileKey}'] is missing required parts for template build.");
                }

                connectionString = db.Connection_String_Template
                    .Replace("{Address}", profile.Address)
                    .Replace("{Port}", profile.Port.Value.ToString())
                    .Replace("{Login}", profile.Login)
                    .Replace("{Password}", profile.Password);
            }
            //Конец изменений

            _connectionStrings[profileKey] = connectionString;
        }
    }
}
