using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using Integration.Services;

namespace Integration.Services;

public sealed class HttpClientProvider
{
    private readonly ParametersStore _parameters;
    private readonly ConcurrentDictionary<string, HttpClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public HttpClientProvider(ParametersStore parameters)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>
    /// Получить HttpClient для сервиса (uzstandart / timv / pharm_agency и т.п.)
    /// </summary>
    public HttpClient GetClient(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("serviceName is required.", nameof(serviceName));

        return _clients.GetOrAdd(serviceName, CreateClient);
    }

    private HttpClient CreateClient(string serviceName)
    {
        var snapshot = _parameters.GetSnapshot();

        if (!snapshot.Services.TryGetValue(serviceName, out var service))
            throw new InvalidOperationException($"Service '{serviceName}' not found in parameters.json");

        var timeoutSeconds =
            service.Http_Timeout_Seconds
            ?? snapshot.Http?.Timeout_Seconds
            ?? 30;

        var client = new HttpClient
        {
            BaseAddress = service.Base_Url is not null
                ? new Uri(service.Base_Url, UriKind.Absolute)
                : null,

            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        // Authorization: Bearer ...
        if (!string.IsNullOrWhiteSpace(service.Auth_Bearer))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", service.Auth_Bearer);
        }

        // Общие заголовки
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    /// <summary>
    /// Очистить все HttpClient'ы (например, при Reload parameters).
    /// </summary>
    public void Clear()
    {
        foreach (var kv in _clients)
        {
            try { kv.Value.Dispose(); } catch { /* ignore */ }
        }

        _clients.Clear();
    }
}
