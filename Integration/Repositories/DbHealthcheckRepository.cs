using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using Integration.Services;
using UzEx.Dapper.Oracle;
using UzEx.Dapper.Oracle.Enums;


namespace Integration.Repositories;

/// <summary>
/// Repository for lightweight DB healthcheck operations.
/// Uses UzEx.Dapper.Oracle to call integration_pack procedures.
/// </summary>
public sealed class DbHealthcheckRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public DbHealthcheckRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Executes DB healthcheck and returns resolved DB name/info string.
    /// Calls integration_pack.get_db_name.
    /// </summary>
    public async Task<string> GetDbNameAsync(string profileKey)
    {
        //Начало изменений
        var parameters = new OracleDynamicParameters();
        parameters.Add("o_db_name", null, OracleMappingType.Varchar2, ParameterDirection.Output, size: 4000);

        await using (var con = _connectionFactory.Create(profileKey))
        {
            await con.OpenAsync().ConfigureAwait(false);

            await con.ExecuteAsync(
                "integration_pack.get_db_name",
                parameters,
                commandType: CommandType.StoredProcedure
            ).ConfigureAwait(false);

            return parameters.Get<string>("o_db_name");
        }
        //Конец изменений
    }

    /// <summary>
    /// Explicit ping-style healthcheck.
    /// Semantically same as GetDbNameAsync but kept separate for clarity/future extension.
    /// Calls integration_pack.ping.
    /// </summary>
    public async Task<string> PingAsync(string profileKey)
    {
        //Начало изменений
        var parameters = new OracleDynamicParameters();
        parameters.Add("o_db_name", null, OracleMappingType.Varchar2, ParameterDirection.Output, size: 4000);

        await using (var con = _connectionFactory.Create(profileKey))
        {
            await con.OpenAsync().ConfigureAwait(false);

            await con.ExecuteAsync(
                "integration_pack.ping",
                parameters,
                commandType: CommandType.StoredProcedure
            ).ConfigureAwait(false);

            return parameters.Get<string>("o_db_name");
        }
        //Конец изменений
    }

    // summary: DbHealthcheckRepository — лёгкий репозиторий для healthcheck БД.
    //          Стиль вызова процедур приведён к единому виду: параметры → using(con) → ExecuteAsync().
}
