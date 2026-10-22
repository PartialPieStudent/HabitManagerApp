using System.Text.Json;

namespace NetWatch.Core.Settings;

/// <summary>
/// Настройки программы. Загружаются из settings.json (пример — settings.example.json в корне репозитория).
/// Имена свойств совпадают с ключами в JSON.
/// </summary>
public sealed class AppSettings
{
    public double PollIntervalSeconds { get; set; } = 2;
    public bool ResolveDns { get; set; } = true;
    public string DatabasePath { get; set; } = "netwatch.db";
    public OpenTipSettings OpenTip { get; set; } = new();
    public LlmSettings Llm { get; set; } = new();

    /// <summary>Путь к файлу, из которого загружены настройки (null — файл не найден, используются значения по умолчанию).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? LoadedFrom { get; private set; }

    /// <summary>Полный путь к базе. Относительный путь считается от папки с программой.</summary>
    public string GetDatabaseFullPath() =>
        Path.IsPathRooted(DatabasePath) ? DatabasePath : Path.Combine(AppContext.BaseDirectory, DatabasePath);

    /// <summary>
    /// Ищет settings.json:
    ///   1) рядом с программой (bin/Debug/net10.0/);
    ///   2) в текущей папке;
    ///   3) в родительских папках (удобно при разработке: положили settings.json в корень репозитория —
    ///      и его видят и консоль, и GUI, без копирования).
    /// Затем применяет переменные окружения (они важнее файла).
    /// </summary>
    public static AppSettings Load()
    {
        AppSettings settings = new();
        string? path = FindSettingsFile();

        if (path is not null)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,           // "apikey" == "ApiKey"
                ReadCommentHandling = JsonCommentHandling.Skip, // разрешаем // комментарии в JSON
                AllowTrailingCommas = true                     // и запятую после последнего элемента
            };
            settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), options) ?? new AppSettings();
            settings.LoadedFrom = path;
        }

        // Переменные окружения — удобный способ не хранить ключи в файлах
        string? openTipKey = Environment.GetEnvironmentVariable("NETWATCH_OPENTIP_KEY");
        if (!string.IsNullOrWhiteSpace(openTipKey))
            settings.OpenTip.ApiKey = openTipKey;

        string? anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(settings.Llm.ApiKey) && !string.IsNullOrWhiteSpace(anthropicKey))
            settings.Llm.ApiKey = anthropicKey;

        return settings;
    }

    private static string? FindSettingsFile()
    {
        const string fileName = "settings.json";

        string candidate = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(candidate)) return candidate;

        candidate = Path.Combine(Environment.CurrentDirectory, fileName);
        if (File.Exists(candidate)) return candidate;

        // Поднимаемся вверх по папкам (не больше 6 уровней)
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory).Parent;
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}

/// <summary>Настройки Kaspersky OpenTIP.</summary>
public sealed class OpenTipSettings
{
    public string? ApiKey { get; set; }
    public int MaxRequestsPerRun { get; set; } = 50;
}

/// <summary>Настройки ИИ-анализатора.</summary>
public sealed class LlmSettings
{
    /// <summary>"Anthropic" (Claude API, облако) или "Ollama" (локальная модель).</summary>
    public string Provider { get; set; } = "Anthropic";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-haiku-5-5";
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.1";
    public int MaxConnections { get; set; } = 80;

    public bool IsOllama => Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase);
}
