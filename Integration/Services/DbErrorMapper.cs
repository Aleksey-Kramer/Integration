using System;
using Oracle.ManagedDataAccess.Client;
using Integration.Models;

namespace Integration.Services;

/// <summary>
/// Maps Oracle / DB related exceptions to AgentStatusErrors
/// so UI and runtime state receive meaningful error codes.
/// </summary>
// summary: Нормализация Oracle/DB исключений в AgentStatusErrors (DB-домена).
//          Используется для заполнения DbConnectionState и отображения в UI.
public static class DbErrorMapper
{
    public static AgentStatusErrors Map(Exception ex)
    {
        if (ex is null)
            return AgentStatusErrors.unknown;

        //Начало изменений
        // unwrap до корневого исключения
        var e = ex;
        while (e.InnerException is not null)
            e = e.InnerException;

        if (e is OracleException ora)
            return MapOracleException(ora);
        //Конец изменений

        return AgentStatusErrors.unknown;
    }

    private static AgentStatusErrors MapOracleException(OracleException ex)
    {
        // Common Oracle error codes
        return ex.Number switch
        {
            // invalid username/password
            1017 => AgentStatusErrors.unauthorized,

            // TNS / connection issues
            12154 or // TNS: could not resolve the connect identifier specified
                12514 or // TNS: listener does not currently know of service requested
                12541 or // TNS: no listener
                12543 or // TNS: destination host unreachable
                12545 or // TNS: host or object does not exist
                12560 => AgentStatusErrors.connection_failed,

            // timeout / network
            12170 => AgentStatusErrors.timeout,

            // everything else Oracle-side
            _ => AgentStatusErrors.server_error
        };
    }
}