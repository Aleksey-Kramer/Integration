using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Integration.Agents.UzStandart;

/// <summary>
/// Тело запроса в UzStandart.
/// Для циклического обновления используем только page + per_page.
/// Остальные поля оставляем, чтобы потом включить фильтры (Postman совместимость).
/// </summary>
public sealed class UzStandartRequest
{
    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    // optional filters (пока не используем в цикле)
    [JsonPropertyName("date_from")]
    public string? DateFrom { get; set; }

    [JsonPropertyName("date_to")]
    public string? DateTo { get; set; }

    [JsonPropertyName("tin")]
    public string? Tin { get; set; }

    [JsonPropertyName("blank_number")]
    public long? BlankNumber { get; set; }

    // В ТЗ ты написал registeredGov (camelCase) — оставляем как есть.
    // Если API фактически ждёт registered_gov, потом просто добавим второе поле или поменяем имя.
    [JsonPropertyName("registeredGov")]
    public long? RegisteredGov { get; set; }
}

public sealed class UzStandartResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("total_counts")]
    public long TotalCounts { get; set; }

    [JsonPropertyName("page_total")]
    public int PageTotal { get; set; }

    // description — это "словарь описаний полей", он не обязателен для логики
    [JsonPropertyName("description")]
    public Dictionary<string, string>? Description { get; set; }

    [JsonPropertyName("data")]
    public List<UzStandartCertificate>? Data { get; set; }
}

public sealed class UzStandartCertificate
{
    [JsonPropertyName("blank_number")]
    public string? BlankNumber { get; set; }

    [JsonPropertyName("registered_gov")]
    public string? RegisteredGov { get; set; }

    [JsonPropertyName("full_registered")]
    public string? FullRegistered { get; set; }

    [JsonPropertyName("issued_date")]
    public string? IssuedDate { get; set; }

    [JsonPropertyName("deadline")]
    public string? Deadline { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("organ_name")]
    public string? OrganName { get; set; }

    [JsonPropertyName("organ_inn")]
    public string? OrganInn { get; set; }

    [JsonPropertyName("applicant_name")]
    public string? ApplicantName { get; set; }

    [JsonPropertyName("applicant_inn")]
    public string? ApplicantInn { get; set; }

    [JsonPropertyName("code_tn_ved")]
    public string? CodeTnVed { get; set; }

    [JsonPropertyName("manufact_country")]
    public string? ManufactCountry { get; set; }

    [JsonPropertyName("cert_file_link")]
    public string? CertFileLink { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}
