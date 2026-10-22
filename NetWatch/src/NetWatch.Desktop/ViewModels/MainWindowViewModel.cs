using System.Collections.ObjectModel;
using System.Net;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetWatch.Core;
using NetWatch.Core.Analysis;
using NetWatch.Core.Models;
using NetWatch.Core.Services;

namespace NetWatch.Desktop.ViewModels;

/// <summary>
/// Модель представления главного окна (паттерн MVVM: Model — View — ViewModel).
///
///   Model     — данные и логика (NetWatch.Core);
///   View      — разметка окна (MainWindow.axaml), в ней нет логики;
///   ViewModel — этот класс: готовит данные для отображения и обрабатывает действия пользователя.
///
/// View "привязывается" к свойствам ViewModel: {Binding FilterText}, {Binding AnalyzeAiCommand} и т.д.
///
/// partial обязателен: MVVM Toolkit дописывает к классу сгенерированный код.
///   [ObservableProperty] private string _filterText;  →  генерирует свойство FilterText с уведомлением об изменении
///   [RelayCommand] private Task AnalyzeAiAsync()      →  генерирует команду AnalyzeAiCommand для кнопки
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int MaxLogLines = 500;

    private readonly NetWatchEngine _engine;

    // ВСЕ активные строки (по ключу соединения). В таблице показываются только прошедшие фильтр.
    private readonly Dictionary<string, ConnectionRowViewModel> _allRows = new();

    // ---------------- Коллекции для таблиц ----------------
    // ObservableCollection сама сообщает интерфейсу о добавлении/удалении элементов.

    /// <summary>Строки, видимые во вкладке "Активные" (после фильтра).</summary>
    public ObservableCollection<ConnectionRowViewModel> Connections { get; } = new();

    /// <summary>Записи вкладки "История".</summary>
    public ObservableCollection<HistoryRecord> History { get; } = new();

    /// <summary>Строки вкладки "Журнал" (новые сверху).</summary>
    public ObservableCollection<string> Log { get; } = new();

    // ---------------- Свойства, привязанные к элементам окна ----------------

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private bool _hideLoopback = true;
    [ObservableProperty] private bool _hideListening;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _analyzeSelectedOnly;
    [ObservableProperty] private bool _isAnalyzing;
    [ObservableProperty] private ConnectionRowViewModel? _selectedConnection;
    [ObservableProperty] private string _statusText = "Запуск...";

    /// <summary>Подробности о выделенном соединении — показываются в панели под таблицей.</summary>
    public string SelectedDetails => SelectedConnection is not { } row
        ? "Выделите строку, чтобы увидеть подробности."
        : $"{row.ProcessName} (PID {row.Pid})   {row.ProcessPath}\n" +
          $"{row.LocalEndPoint} → {row.RemoteEndPoint} {row.RemoteHost}   {row.StateText}, {row.DirectionText}, с {row.FirstSeenText}\n" +
          $"Вердикт: {(row.Verdict == "" ? "не проверялось" : row.Verdict)}";

    // ---------------- "partial void On...Changed" — вызываются MVVM Toolkit при изменении свойства ----------------

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnHideLoopbackChanged(bool value) => ApplyFilter();
    partial void OnHideListeningChanged(bool value) => ApplyFilter();
    partial void OnIsPausedChanged(bool value) => _engine.Monitor.IsPaused = value;
    partial void OnSelectedConnectionChanged(ConnectionRowViewModel? value) => OnPropertyChanged(nameof(SelectedDetails));

    partial void OnIsAnalyzingChanged(bool value)
    {
        // Кнопки анализа должны выключаться, пока идёт анализ (см. CanAnalyze)
        AnalyzeKasperskyCommand.NotifyCanExecuteChanged();
        AnalyzeAiCommand.NotifyCanExecuteChanged();
    }

    // ---------------- Конструктор ----------------

    public MainWindowViewModel(NetWatchEngine engine)
    {
        _engine = engine;

        // ВАЖНО: события монитора приходят из ФОНОВОГО потока, а менять элементы интерфейса
        // (в том числе ObservableCollection, к которой привязана таблица) можно только из UI-потока.
        // Dispatcher.UIThread.Post — "выполни этот код в UI-потоке, когда будет возможность".
        _engine.Monitor.Updated += update => Dispatcher.UIThread.Post(() => OnMonitorUpdated(update));
        _engine.Monitor.Error += ex => Dispatcher.UIThread.Post(() => AddLog($"Ошибка опроса: {ex.Message}"));
        _engine.Log += message => Dispatcher.UIThread.Post(() => AddLog(message));

        foreach (string warning in _engine.StartupWarnings)
            AddLog($"Внимание: {warning}");

        if (_engine.Settings.LoadedFrom is { } path)
            AddLog($"Настройки загружены из {path}");

        if (!OperatingSystem.IsWindows() && Environment.UserName != "root")
            AddLog("Подсказка: без root видны PID только ваших процессов. Для полной картины запустите через sudo.");

        _engine.Start();
        RefreshHistory();
    }

    // ---------------- Обработка обновлений монитора ----------------

    private void OnMonitorUpdated(MonitorUpdate update)
    {
        // 1. Удаляем закрытые
        foreach (ConnectionInfo info in update.Closed)
        {
            if (_allRows.Remove(info.Key, out ConnectionRowViewModel? row))
                Connections.Remove(row);

            if (!update.IsInitial)
                AddLog($"[-] {info.ClosedAt:HH:mm:ss} {info.ProcessName} {info.LocalEndPoint} → {info.RemoteEndPoint} (жило {Formatting.Duration(info.Duration)})");
        }

        // 2. Добавляем новые
        foreach (ConnectionInfo info in update.Opened)
        {
            var row = new ConnectionRowViewModel(info);
            _allRows[info.Key] = row;

            if (PassesFilter(row))
                Connections.Add(row);

            if (!update.IsInitial)
                AddLog($"[+] {info.FirstSeen:HH:mm:ss} {info.ProcessName} {info.LocalEndPoint} → {info.RemoteEndPoint}");
        }

        // 3. Обновляем "живые" поля у всех (длительность, состояние, имя хоста)
        foreach (ConnectionRowViewModel row in _allRows.Values)
            row.Refresh();

        OnPropertyChanged(nameof(SelectedDetails));

        if (update.IsInitial)
            AddLog($"На момент запуска найдено соединений: {update.Opened.Count}");

        UpdateStatus();
    }

    // ---------------- Фильтр ----------------

    /// <summary>Пересобирает видимую коллекцию Connections по текущему фильтру.</summary>
    private void ApplyFilter()
    {
        var visible = _allRows.Values.Where(PassesFilter).ToHashSet();

        // Удаляем лишние (идём с конца, чтобы индексы не сдвигались)
        for (int i = Connections.Count - 1; i >= 0; i--)
        {
            if (!visible.Contains(Connections[i]))
                Connections.RemoveAt(i);
        }

        // Добавляем недостающие
        var present = Connections.ToHashSet();
        foreach (ConnectionRowViewModel row in visible)
        {
            if (!present.Contains(row))
                Connections.Add(row);
        }

        UpdateStatus();
    }

    private bool PassesFilter(ConnectionRowViewModel row)
    {
        ConnectionInfo c = row.Info;

        if (HideLoopback && IPAddress.IsLoopback(c.LocalAddress))
            return false;

        if (HideListening && c.Direction == ConnectionDirection.Listening)
            return false;

        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        // Ищем подстроку в нескольких полях без учёта регистра
        string text = FilterText.Trim();
        return Contains(c.ProcessName, text) ||
               Contains(c.LocalEndPoint, text) ||
               Contains(c.RemoteEndPoint, text) ||
               Contains(c.RemoteHost, text) ||
               Contains(c.Pid.ToString(), text) ||
               Contains(c.State.ToDisplayString(), text);

        static bool Contains(string? source, string value) =>
            source is not null && source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------- Команды (кнопки) ----------------

    private bool CanAnalyze() => !IsAnalyzing;

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private Task AnalyzeKasperskyAsync() =>
        RunAnalysisAsync(_engine.Analyzers.OfType<KasperskyOpenTipAnalyzer>().First());

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private Task AnalyzeAiAsync() =>
        RunAnalysisAsync(_engine.Analyzers.OfType<LlmAnalyzer>().First());

    [RelayCommand]
    private void RefreshHistory()
    {
        History.Clear();
        if (_engine.History is null)
            return;

        foreach (HistoryRecord record in _engine.History.GetRecent(1000, FilterText))
            History.Add(record);
    }

    [RelayCommand]
    private void ClearLog() => Log.Clear();

    /// <summary>
    /// Общая логика анализа для любого анализатора.
    /// async/await: пока ждём ответа из интернета, окно не "зависает".
    /// </summary>
    private async Task RunAnalysisAsync(IConnectionAnalyzer analyzer)
    {
        if (!analyzer.IsConfigured)
        {
            AddLog($"{analyzer.Name}: не настроен — укажите ключ в settings.json (см. settings.example.json).");
            return;
        }

        // Что проверяем: выделенное соединение или все видимые в таблице
        List<ConnectionInfo> targets = AnalyzeSelectedOnly && SelectedConnection is { } selected
            ? [selected.Info]
            : Connections.Select(r => r.Info).ToList();

        if (targets.Count == 0)
        {
            AddLog($"{analyzer.Name}: нет соединений для проверки.");
            return;
        }

        IsAnalyzing = true;
        StatusText = $"{analyzer.Name}: проверяю {targets.Count} соединений...";

        try
        {
            // Task.Run — уводим работу в фоновый поток, чтобы точно не блокировать интерфейс
            IReadOnlyList<AnalysisResult> results = await Task.Run(() => _engine.AnalyzeAsync(analyzer, targets));

            // После await мы снова в UI-потоке (Avalonia это обеспечивает), можно трогать интерфейс
            foreach (ConnectionRowViewModel row in _allRows.Values)
                row.Refresh();
            OnPropertyChanged(nameof(SelectedDetails));

            int dangerous = results.Count(r => r.Risk >= RiskLevel.Suspicious);
            AddLog($"{analyzer.Name}: оценено {results.Count} соединений, подозрительных/опасных: {dangerous}.");

            foreach (AnalysisResult r in results.Where(r => r.Risk >= RiskLevel.Suspicious))
                AddLog($"   ⚠ {Formatting.Risk(r.Risk)}: {r.Connection.ProcessName} → {r.Connection.RemoteEndPoint} — {r.Summary}");

            if (results.Count == 0)
                AddLog($"{analyzer.Name}: среди выбранных нет соединений с публичными адресами — проверять нечего.");
        }
        catch (Exception ex)
        {
            AddLog($"{analyzer.Name}: ошибка — {ex.Message}");
        }
        finally
        {
            IsAnalyzing = false;
            UpdateStatus();
        }
    }

    // ---------------- Помощники ----------------

    private void AddLog(string message)
    {
        Log.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (Log.Count > MaxLogLines)
            Log.RemoveAt(Log.Count - 1);
    }

    private void UpdateStatus()
    {
        if (IsAnalyzing) return;
        StatusText = $"Активных соединений: {_allRows.Count}   показано: {Connections.Count}   " +
                     $"обновлено: {DateTime.Now:HH:mm:ss}" + (IsPaused ? "   (ПАУЗА)" : "");
    }

    public void Dispose() => _engine.Dispose();
}
