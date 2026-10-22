using NetWatch.Core.Analysis;
using NetWatch.Core.Models;
using NetWatch.Core.Providers;
using NetWatch.Core.Services;
using NetWatch.Core.Settings;

namespace NetWatch.Core;

/// <summary>
/// "Двигатель" программы: создаёт и связывает между собой все части ядра.
/// И консоль, и GUI работают только с этим классом — так у них одинаковое поведение.
///
///   Провайдер ОС ──► ConnectionMonitor ──► событие Updated ──► HistoryStore (SQLite)
///                                                         └─► интерфейс (консоль / окно)
///   Пользователь ──► AnalyzeAsync ──► IConnectionAnalyzer (OpenTIP, ИИ) ──► вердикты в соединения и историю
/// </summary>
public sealed class NetWatchEngine : IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HttpClient _http;
    private readonly List<string> _startupWarnings = new();
    private Task? _monitorLoop;

    public AppSettings Settings { get; }
    public ConnectionMonitor Monitor { get; }

    /// <summary>История. Может быть null, если базу открыть не удалось (тогда программа работает без истории).</summary>
    public HistoryStore? History { get; }

    /// <summary>Все доступные анализаторы. Хотите добавить свой — допишите его сюда.</summary>
    public IReadOnlyList<IConnectionAnalyzer> Analyzers { get; }

    /// <summary>Предупреждения, возникшие при запуске (нет settings.json, не открылась база и т.п.).</summary>
    public IReadOnlyList<string> StartupWarnings => _startupWarnings;

    /// <summary>Сообщения для журнала (ошибки записи в базу и т.п.).</summary>
    public event Action<string>? Log;

    public NetWatchEngine(AppSettings settings)
    {
        Settings = settings;

        if (settings.LoadedFrom is null)
            _startupWarnings.Add("settings.json не найден — используются настройки по умолчанию, анализаторы без ключей отключены.");

        // 1. Источник данных для текущей ОС
        IConnectionProvider provider = ConnectionProviderFactory.Create();

        // 2. Монитор
        Monitor = new ConnectionMonitor(
            provider,
            new ProcessResolver(),
            settings.ResolveDns ? new DnsResolver() : null,
            TimeSpan.FromSeconds(Math.Max(0.5, settings.PollIntervalSeconds)));

        // 3. История
        try
        {
            History = new HistoryStore(settings.GetDatabaseFullPath());
        }
        catch (Exception ex)
        {
            _startupWarnings.Add($"Не удалось открыть базу истории ({ex.Message}) — история не сохраняется.");
        }

        // 4. Анализаторы. Один HttpClient на всю программу — так рекомендует Microsoft
        //    (создание нового HttpClient на каждый запрос может исчерпать сетевые сокеты).
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("NetWatch/0.1");

        Analyzers =
        [
            new KasperskyOpenTipAnalyzer(_http, settings.OpenTip),
            new LlmAnalyzer(_http, settings.Llm)
            // сюда можно добавить свои: new VirusTotalAnalyzer(_http, ...), ...
        ];

        // 5. Подписываемся на изменения, чтобы писать историю.
        //    Подписка происходит ДО того, как на монитор подпишется интерфейс, поэтому
        //    к моменту, когда интерфейс получит событие, у соединений уже будет HistoryId.
        Monitor.Updated += SaveToHistory;
    }

    /// <summary>Запустить фоновый опрос соединений.</summary>
    public void Start()
    {
        // ??= — присвоить, только если ещё null (защита от двойного запуска)
        _monitorLoop ??= Task.Run(() => Monitor.RunAsync(_shutdown.Token));
    }

    /// <summary>
    /// Проанализировать соединения выбранным анализатором.
    /// Результаты записываются в сами соединения (Risk, Verdict) и в историю.
    /// </summary>
    public async Task<IReadOnlyList<AnalysisResult>> AnalyzeAsync(
        IConnectionAnalyzer analyzer, IReadOnlyList<ConnectionInfo> connections, CancellationToken cancellationToken = default)
    {
        if (!analyzer.IsConfigured)
            throw new InvalidOperationException($"{analyzer.Name}: не настроен — укажите API-ключ в settings.json.");

        IReadOnlyList<AnalysisResult> results = await analyzer.AnalyzeAsync(connections, cancellationToken);

        foreach (AnalysisResult result in results)
        {
            result.Connection.Risk = result.Risk;
            result.Connection.Verdict = $"[{result.Analyzer}] {result.Summary}";
        }

        try
        {
            History?.SaveVerdicts(results);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Не удалось сохранить вердикты в историю: {ex.Message}");
        }

        return results;
    }

    private void SaveToHistory(MonitorUpdate update)
    {
        if (History is null) return;
        try
        {
            History.RecordOpened(update.Opened);
            History.RecordUpdated(update.Closed);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Ошибка записи в историю: {ex.Message}");
        }
    }

    /// <summary>
    /// Корректное завершение: остановить опрос, дописать в историю последнее состояние активных соединений.
    /// </summary>
    public void Dispose()
    {
        _shutdown.Cancel();
        try
        {
            _monitorLoop?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // цикл уже завершается — игнорируем
        }

        try
        {
            History?.RecordUpdated(Monitor.GetSnapshot());
        }
        catch
        {
            // при выходе не страшно
        }

        History?.Dispose();
        _http.Dispose();
        _shutdown.Dispose();
    }
}
