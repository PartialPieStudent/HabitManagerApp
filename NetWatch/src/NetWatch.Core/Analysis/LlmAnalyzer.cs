using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NetWatch.Core.Models;
using NetWatch.Core.Settings;

namespace NetWatch.Core.Analysis;

/// <summary>
/// Анализ соединений с помощью языковой модели (ИИ).
///
/// Идея: собираем компактную таблицу "процесс → куда подключается" и просим модель
/// найти подозрительное (незнакомые процессы, странные порты, системные утилиты, лезущие в интернет,
/// процессы из временных папок и т.п.). Модель отвечает JSON-массивом, который мы разбираем.
///
/// Поддерживаются два "бэкенда":
///   - Anthropic (Claude API) — облако, нужен API-ключ, данные о соединениях уходят в облако;
///   - Ollama — локальная модель на вашем ПК, всё остаётся у вас (качество зависит от модели).
///
/// Важно: ИИ может ошибаться в обе стороны. Это помощник для "второго взгляда", а не антивирус.
/// </summary>
public sealed class LlmAnalyzer : IConnectionAnalyzer
{
    private const string AnthropicUrl = "https://api.anthropic.com/v1/messages";

    // Инструкция для модели (system prompt). Чем точнее описан формат ответа, тем надёжнее разбор.
    private const string SystemPrompt =
        """
        Ты — специалист по сетевой безопасности. Тебе дают список сетевых соединений компьютера пользователя.
        Оцени каждое соединение: насколько оно похоже на нормальную активность или на вредоносную
        (майнеры, трояны удалённого доступа, бэкдоры, утечка данных, нежелательное ПО).
        Учитывай: имя и путь процесса (подозрительны запуски из Temp/AppData/Downloads и /tmp),
        удалённый порт (нестандартные порты), имя хоста, направление, открытые для всех порты (Слушает на 0.0.0.0).
        Ответь ТОЛЬКО JSON-массивом, без пояснений до и после, в формате:
        [{"id": 0, "risk": "clean|low|suspicious|malicious|unknown", "reason": "кратко по-русски, до 20 слов"}]
        Включи в ответ каждое соединение из списка.
        """;

    private readonly HttpClient _http;
    private readonly LlmSettings _settings;

    public LlmAnalyzer(HttpClient http, LlmSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public string Name => _settings.IsOllama
        ? $"ИИ (Ollama: {_settings.OllamaModel})"
        : $"ИИ (Claude: {_settings.Model})";

    // Ollama работает локально и ключа не требует
    public bool IsConfigured => _settings.IsOllama || !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public async Task<IReadOnlyList<AnalysisResult>> AnalyzeAsync(
        IReadOnlyList<ConnectionInfo> connections, CancellationToken cancellationToken)
    {
        // Отправляем только интересное: соединения с интернетом и открытые порты.
        // Локальные соединения 127.0.0.1 ↔ 127.0.0.1 ИИ почти ничего не скажут, а место в запросе съедят.
        List<ConnectionInfo> selected = connections
            .Where(c => c.Direction == ConnectionDirection.Listening ||
                        (c.HasRemote && NetworkHelpers.IsPublic(c.RemoteAddress)))
            .Take(_settings.MaxConnections)
            .ToList();

        if (selected.Count == 0)
            return [];

        string prompt = BuildPrompt(selected);

        string answer = _settings.IsOllama
            ? await AskOllamaAsync(prompt, cancellationToken)
            : await AskClaudeAsync(prompt, cancellationToken);

        return ParseAnswer(answer, selected);
    }

    /// <summary>
    /// Строим текстовую таблицу. У каждой строки — номер (id), по которому модель
    /// будет ссылаться на соединение в ответе.
    /// </summary>
    private static string BuildPrompt(List<ConnectionInfo> connections)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ОС: {Environment.OSVersion}");
        sb.AppendLine("Соединения (id | процесс | путь | направление | протокол | локальный адрес | удалённый адрес | хост | состояние):");

        for (int i = 0; i < connections.Count; i++)
        {
            ConnectionInfo c = connections[i];
            sb.AppendLine(string.Join(" | ",
                i,
                c.ProcessName,
                c.ProcessPath ?? "?",
                c.Direction.ToDisplayString(),
                c.Protocol,
                c.LocalEndPoint,
                c.RemoteEndPoint,
                c.RemoteHost ?? "-",
                c.State.ToDisplayString()));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Запрос к Claude (Messages API).
    /// Документация: https://docs.claude.com — раздел Messages API.
    /// </summary>
    private async Task<string> AskClaudeAsync(string prompt, CancellationToken cancellationToken)
    {
        // Анонимный объект → JSON. Имена свойств совпадают с тем, что ждёт API.
        var body = new
        {
            model = _settings.Model,
            max_tokens = 4096,
            system = SystemPrompt,
            messages = new[] { new { role = "user", content = prompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, AnthropicUrl)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("x-api-key", _settings.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        string text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Claude API вернул {(int)response.StatusCode}: {Formatting.Truncate(text, 300)}");

        // Ответ: { "content": [ { "type": "text", "text": "..." }, ... ], ... }
        using JsonDocument json = JsonDocument.Parse(text);
        var result = new StringBuilder();
        foreach (JsonElement block in json.RootElement.GetProperty("content").EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text")
                result.Append(block.GetProperty("text").GetString());
        }
        return result.ToString();
    }

    /// <summary>
    /// Запрос к локальной Ollama (https://ollama.com).
    /// Перед этим: установить Ollama и скачать модель, например: ollama pull llama3.1
    /// </summary>
    private async Task<string> AskOllamaAsync(string prompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = _settings.OllamaModel,
            stream = false, // хотим получить ответ целиком, а не по кусочкам
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = prompt }
            }
        };

        string url = _settings.OllamaUrl.TrimEnd('/') + "/api/chat";
        using HttpResponseMessage response = await _http.PostAsJsonAsync(url, body, cancellationToken);
        string text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama вернула {(int)response.StatusCode}: {Formatting.Truncate(text, 300)}");

        // Ответ: { "message": { "role": "assistant", "content": "..." }, ... }
        using JsonDocument json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    /// <summary>
    /// Разбираем ответ модели. Модели иногда добавляют текст вокруг JSON или оборачивают его в ```json,
    /// поэтому вырезаем всё от первой '[' до последней ']'.
    /// </summary>
    private IReadOnlyList<AnalysisResult> ParseAnswer(string answer, List<ConnectionInfo> connections)
    {
        int start = answer.IndexOf('[');
        int end = answer.LastIndexOf(']');
        if (start < 0 || end <= start)
            throw new InvalidOperationException($"ИИ ответил не в формате JSON: {Formatting.Truncate(answer, 300)}");

        var results = new List<AnalysisResult>();

        using JsonDocument json = JsonDocument.Parse(answer[start..(end + 1)]);
        foreach (JsonElement item in json.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out JsonElement idElement) || !idElement.TryGetInt32(out int id))
                continue;
            if (id < 0 || id >= connections.Count)
                continue; // модель "придумала" несуществующий id

            string riskText = item.TryGetProperty("risk", out var r) ? r.GetString() ?? "" : "";
            string reason = item.TryGetProperty("reason", out var why) ? why.GetString() ?? "" : "";

            RiskLevel risk = riskText.Trim().ToLowerInvariant() switch
            {
                "clean" => RiskLevel.Clean,
                "low" => RiskLevel.Low,
                "suspicious" => RiskLevel.Suspicious,
                "malicious" => RiskLevel.Malicious,
                _ => RiskLevel.Unknown
            };

            results.Add(new AnalysisResult(connections[id], Name, risk, reason));
        }

        return results;
    }
}
