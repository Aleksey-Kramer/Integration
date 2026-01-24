using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Integration.Agents.Template;

public sealed class TemplateClient
{
    private readonly HttpClient _http;

    public TemplateClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <summary>
    /// Отправка доменного запроса агента (шаблон).
    /// Клиент сам преобразует модель в HTTP.
    /// </summary>
    public async Task<TemplateModels.TemplateResponse> SendAsync(
        TemplateModels.TemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        // 1. Преобразование доменной модели → HTTP
        var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "/template/endpoint") // заглушка
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json")
            };

        // 2. HTTP-вызов
        using var response = await _http
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        // 3. Преобразование ответа → доменная модель
        var body = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TemplateModels.TemplateResponse
        {
            Success = response.IsSuccessStatusCode,
            Message = body
        };
    }

    // summary:
    // TemplateClient — HTTP-адаптер агента.
    // Принимает доменные модели и инкапсулирует HTTP-логику.
}