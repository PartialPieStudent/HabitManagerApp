using NetWatch.Core.Models;

namespace NetWatch.Core.Analysis;

/// <summary>
/// Уровень риска. Числа идут по возрастанию "опасности", поэтому уровни можно сравнивать:
/// risk >= RiskLevel.Suspicious — "подозрительно или хуже".
/// </summary>
public enum RiskLevel
{
    Clean = 0,       // проверено, всё хорошо
    Unknown = 1,     // данных нет
    Low = 2,         // небольшие сомнения
    Suspicious = 3,  // подозрительно
    Malicious = 4    // известный вредоносный адрес
}

/// <summary>
/// Результат проверки одного соединения одним анализатором.
/// </summary>
/// <param name="Connection">Какое соединение проверяли.</param>
/// <param name="Analyzer">Название анализатора (например, "Kaspersky OpenTIP").</param>
/// <param name="Risk">Оценка риска.</param>
/// <param name="Summary">Короткое пояснение для человека.</param>
public sealed record AnalysisResult(ConnectionInfo Connection, string Analyzer, RiskLevel Risk, string Summary);

/// <summary>
/// Интерфейс "анализатора" — подключаемого модуля, который оценивает соединения.
///
/// Чтобы добавить новый сервис (VirusTotal, AbuseIPDB, другую нейросеть...),
/// достаточно написать класс, реализующий этот интерфейс, и добавить его в список Analyzers
/// в NetWatchEngine. Консольная версия подхватит его сразу; в окне программы нужно будет
/// добавить кнопку. Как сделать "настоящие" плагины, загружаемые из DLL, — см. GUIDE.md.
///
/// Анализатор получает сразу СПИСОК соединений: облачные сервисы удобнее опрашивать по уникальным IP,
/// а нейросети выгоднее отправить всё одним запросом.
/// </summary>
public interface IConnectionAnalyzer
{
    /// <summary>Название для интерфейса и журнала.</summary>
    string Name { get; }

    /// <summary>Готов ли анализатор к работе (например, указан ли API-ключ).</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Проверить соединения. Возвращает результаты только для тех соединений, которые удалось оценить
    /// (например, локальные адреса 192.168.x.x в облако не отправляются и в результат не попадают).
    /// </summary>
    Task<IReadOnlyList<AnalysisResult>> AnalyzeAsync(IReadOnlyList<ConnectionInfo> connections, CancellationToken cancellationToken);
}
