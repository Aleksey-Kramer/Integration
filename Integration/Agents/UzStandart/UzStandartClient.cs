using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Integration.Services;

namespace Integration.Agents.UzStandart;

public sealed class UzStandartClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly HttpClientProvider _http;

    public UzStandartClient(HttpClientProvider http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>
    /// Получить сертификаты одной страницы.
    /// Возвращает одновременно и распарсенный ответ, и сырой JSON (для логов в details).
    /// </summary>
    public async Task<(UzStandartResponse Response, string RawJson)> GetCertificatesAsync(
        int page,
        int perPage,
        CancellationToken ct)
    {
        if (page <= 0) throw new ArgumentOutOfRangeException(nameof(page), "page must be >= 1");
        if (perPage <= 0) throw new ArgumentOutOfRangeException(nameof(perPage), "perPage must be >= 1");

        var client = _http.GetClient("uzstandart");

        // endpoint может быть в BaseAddress (base_url) + относительный путь
        // Если у тебя endpoint хранится в parameters.json, то в агенте будем передавать его сюда (следующим шагом).
        // Сейчас по умолчанию используем относительный путь, совместимый с твоей конфигурацией.
        var url = "/api/default/certificate";

        var body = new UzStandartRequest
        {
            Page = page,
            PerPage = perPage
            // остальные фильтры не заполняем в цикле
        };

        var json = JsonSerializer.Serialize(body, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);

        var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Сохраняем сырой ответ в тексте исключения (обрезать не будем, потому что это MVP и надо дебажить)
            throw new HttpRequestException(
                $"UzStandart HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {raw}",
                null,
                response.StatusCode);
        }

        var model = JsonSerializer.Deserialize<UzStandartResponse>(raw, JsonOptions);
        if (model is null)
            throw new InvalidOperationException("UzStandart response deserialized to null.");

        return (model, raw);
    }
}
