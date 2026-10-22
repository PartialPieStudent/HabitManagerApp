using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using NetWatch.Core.Models;
using NetWatch.Core.Settings;

namespace NetWatch.Core.Analysis;

/// <summary>
/// Проверка IP-адресов через Kaspersky Threat Intelligence Portal (OpenTIP).
///
/// Как это работает:
///   GET https://opentip.kaspersky.com/api/v1/search/ip?request=1.2.3.4
///   Заголовок: x-api-key: ВАШ_КЛЮЧ
/// В ответе JSON с полем "Zone" — цветовая оценка "Лаборатории Касперского":
///   Red    — вредоносный
///   Orange — подозрительный / нежелательный
///   Yellow — рекламный, потенциально нежелательный
///   Grey   — нет данных
///   Green  — чистый
/// а также блок "IpGeneralInfo" со страной, категориями и т.п.
///
/// Формат API может со временем меняться — актуальное описание в разделе "Help → API" на сайте OpenTIP.
/// У бесплатного ключа есть суточный лимит запросов, поэтому:
///   - проверяем каждый уникальный IP один раз (даже если к нему 20 соединений);
///   - кэшируем ответы на час;
///   - ограничиваем число запросов за один запуск (MaxRequestsPerRun).
/// </summary>
public sealed class KasperskyOpenTipAnalyzer : IConnectionAnalyzer
{
    private const string Endpoint = "https://opentip.kaspersky.com/api/v1/search/ip";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private readonly HttpClient _http;
    private readonly OpenTipSettings _settings;
    private readonly ConcurrentDictionary<string, CachedVerdict> _cache = new();

    private sealed record CachedVerdict(RiskLevel Risk, string Summary, DateTime CachedAt);

    public KasperskyOpenTipAnalyzer(HttpClient http, OpenTipSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public string Name => "Kaspersky OpenTIP";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public async Task<IReadOnlyList<AnalysisResult>> AnalyzeAsync(
        IReadOnlyList<ConnectionInfo> connections, CancellationToken cancellationToken)
    {
        var results = new List<AnalysisResult>();
        int requestsMade = 0;

        // Группируем соединения по удалённому IP, отбрасывая локальные адреса
        var groups = connections
            .Where(c => c.HasRemote && NetworkHelpers.IsPublic(c.RemoteAddress))
            .GroupBy(c => c.RemoteAddress.ToString());

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string ip = group.Key;
            (RiskLevel risk, string summary) verdict;

            if (_cache.TryGetValue(ip, out var cached) && DateTime.Now - cached.CachedAt < CacheLifetime)
            {
                verdict = (cached.Risk, cached.Summary);
            }
            else if (requestsMade >= _settings.MaxRequestsPerRun)
            {
                verdict = (RiskLevel.Unknown, $"Пропущено: лимит {_settings.MaxRequestsPerRun} запросов за запуск");
            }
            else
            {
                verdict = await LookupAsync(ip, cancellationToken);
                requestsMade++;

                // Ошибки (нет сети, неверный ключ) не кэшируем — пусть в следующий раз попробует снова
                if (!verdict.summary.StartsWith("Ошибка", StringComparison.Ordinal))
                    _cache[ip] = new CachedVerdict(verdict.risk, verdict.summary, DateTime.Now);

                // Небольшая пауза между запросами, чтобы не упереться в ограничение частоты
                await Task.Delay(200, cancellationToken);
            }

            // Один и тот же вердикт — всем соединениям с этим IP
            foreach (ConnectionInfo connection in group)
                results.Add(new AnalysisResult(connection, Name, verdict.risk, verdict.summary));
        }

        return results;
    }

    /// <summary>Один запрос к OpenTIP.</summary>
    private async Task<(RiskLevel Risk, string Summary)> LookupAsync(string ip, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Endpoint}?request={Uri.EscapeDataString(ip)}");
            request.Headers.Add("x-api-key", _settings.ApiKey);

            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);

            // Разбираем "особые" коды ответа
            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return (RiskLevel.Unknown, "Нет данных об этом IP");
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    return (RiskLevel.Unknown, "Ошибка: ключ OpenTIP неверный или без доступа к API");
                case HttpStatusCode.TooManyRequests:
                    return (RiskLevel.Unknown, "Ошибка: превышен лимит запросов OpenTIP, попробуйте позже");
            }

            if (!response.IsSuccessStatusCode)
                return (RiskLevel.Unknown, $"Ошибка: OpenTIP вернул код {(int)response.StatusCode}");

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseResponse(json.RootElement);
        }
        catch (HttpRequestException ex)
        {
            return (RiskLevel.Unknown, $"Ошибка сети: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Таймаут HttpClient (а не отмена пользователем)
            return (RiskLevel.Unknown, "Ошибка: OpenTIP не ответил вовремя");
        }
    }

    /// <summary>
    /// Достаём из JSON нужные поля. TryGetProperty — безопасный способ:
    /// если поля нет, не будет исключения, мы просто его пропустим.
    /// </summary>
    private static (RiskLevel, string) ParseResponse(JsonElement root)
    {
        string zone = root.TryGetProperty("Zone", out JsonElement zoneElement)
            ? zoneElement.GetString() ?? "Grey"
            : "Grey";

        var details = new List<string> { $"зона {zone}" };

        if (root.TryGetProperty("IpGeneralInfo", out JsonElement general))
        {
            if (general.TryGetProperty("CountryCode", out JsonElement country) && country.ValueKind == JsonValueKind.String)
                details.Add($"страна {country.GetString()}");

            if (general.TryGetProperty("Categories", out JsonElement categories) && categories.ValueKind == JsonValueKind.Array)
            {
                // Категории могут прийти строками или объектами — на всякий случай обрабатываем оба варианта
                var names = categories.EnumerateArray()
                    .Select(c => c.ValueKind == JsonValueKind.String ? c.GetString() : c.ToString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (names.Count > 0)
                    details.Add($"категории: {string.Join(", ", names)}");
            }
        }

        RiskLevel risk = zone switch
        {
            "Red" => RiskLevel.Malicious,
            "Orange" => RiskLevel.Suspicious,
            "Yellow" => RiskLevel.Low,
            "Green" => RiskLevel.Clean,
            _ => RiskLevel.Unknown
        };

        return (risk, string.Join("; ", details));
    }
}
