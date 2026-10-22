// =====================================================================================
//  NetWatch — консольная версия.
//
//  Использование:
//    netwatch                — режим наблюдения: печатает появление и закрытие соединений
//    netwatch --once         — один снимок таблицы соединений и выход
//    netwatch --analyze      — снимок + проверка всеми настроенными анализаторами
//    netwatch --help         — справка
//
//  Запуск из исходников (из корня репозитория):
//    dotnet run --project src/NetWatch.Cli -- --once
//  (всё после "--" передаётся самой программе, а не команде dotnet)
//
//  Это файл с "операторами верхнего уровня" (top-level statements): код пишется сразу,
//  без class Program и static void Main — компилятор сгенерирует их сам.
// =====================================================================================

using System.Text;
using NetWatch.Core;
using NetWatch.Core.Analysis;
using NetWatch.Core.Models;
using NetWatch.Core.Services;
using NetWatch.Core.Settings;

// В консоли Windows по умолчанию может быть старая кодировка — русские буквы превратятся в "кракозябры"
Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return;
}

AppSettings settings = AppSettings.Load();

// using var — engine.Dispose() вызовется автоматически при выходе из программы
using var engine = new NetWatchEngine(settings);

foreach (string warning in engine.StartupWarnings)
    WriteColored($"! {warning}", ConsoleColor.Yellow);

engine.Log += message => WriteColored($"! {message}", ConsoleColor.Yellow);

// ---------------- Режим снимка (--once / --analyze) ----------------
if (args.Contains("--once") || args.Contains("--analyze"))
{
    engine.Monitor.PollOnce();
    IReadOnlyList<ConnectionInfo> snapshot = engine.Monitor.GetSnapshot();
    PrintTable(snapshot);

    if (args.Contains("--analyze"))
    {
        // Даём пару секунд на обратный DNS, чтобы анализаторы видели имена хостов
        await Task.Delay(TimeSpan.FromSeconds(2));
        await AnalyzeAllAsync(engine, snapshot);
    }
    return;
}

// ---------------- Режим наблюдения ----------------
// CancellationTokenSource — "пульт" для отмены. Ctrl+C нажимает на нём кнопку Cancel.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // не убивать процесс сразу — мы сами корректно завершимся
    cts.Cancel();
};

engine.Monitor.Updated += update =>
{
    if (update.IsInitial)
    {
        Console.WriteLine($"На момент запуска найдено соединений: {update.Opened.Count}. Наблюдаю за изменениями... (Ctrl+C — выход)");
        Console.WriteLine();
        return;
    }

    foreach (ConnectionInfo c in update.Opened)
        WriteColored($"[+] {c.FirstSeen:HH:mm:ss}  {Describe(c)}", ConsoleColor.Green);

    foreach (ConnectionInfo c in update.Closed)
        WriteColored($"[-] {c.ClosedAt:HH:mm:ss}  {Describe(c)}  (жило {Formatting.Duration(c.Duration)})", ConsoleColor.DarkGray);
};

engine.Monitor.Error += ex => WriteColored($"Ошибка опроса: {ex.Message}", ConsoleColor.Red);

Console.WriteLine($"NetWatch: опрос каждые {engine.Monitor.Interval.TotalSeconds} с. История: {engine.History?.FilePath ?? "отключена"}");
engine.Start();

try
{
    // Просто ждём, пока не нажмут Ctrl+C. Вся работа идёт в фоне.
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Завершение...");
}

return;

// =====================================================================================
//  Локальные функции (объявлены после основного кода — так можно в top-level programs)
// =====================================================================================

static string Describe(ConnectionInfo c)
{
    string host = c.RemoteHost is null ? "" : $" ({c.RemoteHost})";
    return $"{c.Protocol,-3} {c.ProcessName}[{c.Pid}]  {c.LocalEndPoint} → {c.RemoteEndPoint}{host}  {c.State.ToDisplayString()}";
}

static void PrintTable(IReadOnlyList<ConnectionInfo> connections)
{
    // Сортируем: сначала установленные соединения, потом по имени процесса
    var ordered = connections
        .OrderBy(c => c.Direction == ConnectionDirection.Listening)
        .ThenBy(c => c.ProcessName, StringComparer.OrdinalIgnoreCase);

    // {значение,-N} — выравнивание по левому краю в поле шириной N символов
    Console.WriteLine($"{"Протокол",-8} {"Направление",-13} {"Процесс",-22} {"PID",7} {"Локальный адрес",-28} {"Удалённый адрес",-28} {"Состояние",-12}");
    Console.WriteLine(new string('-', 124));

    foreach (ConnectionInfo c in ordered)
    {
        Console.WriteLine(
            $"{c.Protocol,-8} {c.Direction.ToDisplayString(),-13} {Formatting.Truncate(c.ProcessName, 22),-22} {c.Pid,7} " +
            $"{Formatting.Truncate(c.LocalEndPoint, 28),-28} {Formatting.Truncate(c.RemoteEndPoint, 28),-28} {c.State.ToDisplayString(),-12}");
    }

    Console.WriteLine();
    Console.WriteLine($"Всего: {connections.Count}");
}

static async Task AnalyzeAllAsync(NetWatchEngine engine, IReadOnlyList<ConnectionInfo> snapshot)
{
    foreach (IConnectionAnalyzer analyzer in engine.Analyzers)
    {
        Console.WriteLine();
        if (!analyzer.IsConfigured)
        {
            WriteColored($"{analyzer.Name}: пропущен — нет API-ключа в settings.json", ConsoleColor.Yellow);
            continue;
        }

        Console.WriteLine($"=== {analyzer.Name} ===");
        try
        {
            IReadOnlyList<AnalysisResult> results = await engine.AnalyzeAsync(analyzer, snapshot);

            // Показываем по одной строке на удалённый адрес, самые опасные — сверху
            var rows = results
                .GroupBy(r => (r.Connection.ProcessName, r.Connection.RemoteEndPoint))
                .Select(g => g.First())
                .OrderByDescending(r => r.Risk);

            foreach (AnalysisResult r in rows)
            {
                ConsoleColor color = r.Risk switch
                {
                    RiskLevel.Malicious => ConsoleColor.Red,
                    RiskLevel.Suspicious => ConsoleColor.Magenta,
                    RiskLevel.Low => ConsoleColor.Yellow,
                    RiskLevel.Clean => ConsoleColor.Green,
                    _ => ConsoleColor.Gray
                };
                WriteColored($"{Formatting.Risk(r.Risk),-14} {r.Connection.ProcessName,-20} {r.Connection.RemoteEndPoint,-28} {r.Summary}", color);
            }

            if (results.Count == 0)
                Console.WriteLine("Нечего проверять (нет соединений с публичными адресами).");
        }
        catch (Exception ex)
        {
            WriteColored($"Ошибка: {ex.Message}", ConsoleColor.Red);
        }
    }
}

static void WriteColored(string text, ConsoleColor color)
{
    ConsoleColor previous = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ForegroundColor = previous;
}

static void PrintHelp()
{
    Console.WriteLine("""
        NetWatch — монитор сетевых подключений

        Использование:
          netwatch              наблюдать за появлением и закрытием соединений (Ctrl+C — выход)
          netwatch --once       показать таблицу текущих соединений и выйти
          netwatch --analyze    таблица + проверка через Kaspersky OpenTIP и ИИ (нужны ключи в settings.json)
          netwatch --help       эта справка

        Совет: для полной информации о процессах запускайте от администратора (Windows) или через sudo (Linux).
        """);
}
