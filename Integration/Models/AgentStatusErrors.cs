namespace Integration.Models;

public enum AgentStatusErrors
{
    none = 0,

    // --- API / сеть ---
    api_timeout,              // timeout / no response
    api_connection_failed,    // не удалось установить соединение (DNS, refused, host unreachable)
    api_http_error,           // HTTP статус != 2xx
    api_invalid_response,     // ответ не соответствует ожидаемому формату
    api_success_false,        // success=false в payload (бизнес-ошибка API)
    api_unauthorized,         // 401 / 403
    api_rate_limited,         // 429
    api_server_error,         // 5xx

    // --- DB / Oracle ---
    timeout,                  // DB timeout / network (используется DbErrorMapper)
    connection_failed,        // DB connection failed (используется DbErrorMapper)
    unauthorized,             // DB invalid credentials (используется DbErrorMapper)
    server_error,             // DB server-side error (используется DbErrorMapper)

    // --- Данные ---
    data_empty,               // ответ корректный, но данных нет
    data_parse_error,         // JSON/XML не парсится
    data_validation_error,    // данные не прошли валидацию

    // --- Инфраструктура агента ---
    agent_canceled,           // тик отменён (Stop/Cancel)
    agent_misconfigured,      // ошибка конфигурации (parameters.json)
    agent_internal_error,     // необработанная ошибка агента

    // --- Не классифицировано ---
    unknown
}

// summary: Нормализованные коды ошибок для UI/runtime.
//          Включает домены API, DB(Oracle), данные и внутренние ошибки агента.
//          ВАЖНО: timeout/connection_failed/unauthorized/server_error используются DbErrorMapper.