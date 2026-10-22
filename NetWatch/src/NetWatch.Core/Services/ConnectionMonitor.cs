using NetWatch.Core.Analysis;
using NetWatch.Core.Models;
using NetWatch.Core.Providers;

namespace NetWatch.Core.Services;

/// <summary>
/// Результат одного опроса: что открылось, что закрылось и полный список активных соединений.
/// </summary>
/// <param name="Opened">Новые соединения (которых не было в прошлый раз).</param>
/// <param name="Closed">Исчезнувшие соединения (у них заполнено ClosedAt).</param>
/// <param name="Active">Все соединения, активные на данный момент.</param>
/// <param name="IsInitial">true для самого первого опроса — тогда Opened содержит ВСЁ, что было до запуска.</param>
public sealed record MonitorUpdate(
    IReadOnlyList<ConnectionInfo> Opened,
    IReadOnlyList<ConnectionInfo> Closed,
    IReadOnlyList<ConnectionInfo> Active,
    bool IsInitial);

/// <summary>
/// Сердце программы. Периодически запрашивает у провайдера таблицу соединений,
/// сравнивает с предыдущей и сообщает (через событие Updated), что изменилось.
///
/// Как получается "время подключения":
/// ОС не хранит время открытия соединения в доступном виде, поэтому мы фиксируем момент,
/// когда впервые увидели соединение (FirstSeen). Точность — интервал опроса (по умолчанию 2 с).
/// Очень короткие соединения (живущие меньше интервала) можно пропустить —
/// для их отлова нужен ETW в Windows или eBPF в Linux (см. GUIDE.md, этап развития).
/// </summary>
public sealed class ConnectionMonitor
{
    private readonly IConnectionProvider _provider;
    private readonly ProcessResolver _processes;
    private readonly DnsResolver? _dns;

    // Активные соединения по ключу. Доступ — только под блокировкой _lock,
    // потому что опрос идёт в фоновом потоке, а GetSnapshot() могут вызвать из другого.
    private readonly Dictionary<string, ConnectionInfo> _active = new();
    private readonly object _lock = new();
    private bool _isFirstPoll = true;

    /// <summary>Вызывается после каждого опроса. ВНИМАНИЕ: из фонового потока!</summary>
    public event Action<MonitorUpdate>? Updated;

    /// <summary>Ошибка при опросе (сам цикл опроса при этом продолжает работать).</summary>
    public event Action<Exception>? Error;

    /// <summary>Интервал между опросами.</summary>
    public TimeSpan Interval { get; }

    /// <summary>Если true — опрос временно не выполняется (кнопка "Пауза").</summary>
    public bool IsPaused { get; set; }

    public ConnectionMonitor(IConnectionProvider provider, ProcessResolver processes, DnsResolver? dns, TimeSpan interval)
    {
        _provider = provider;
        _processes = processes;
        _dns = dns;
        Interval = interval;
    }

    /// <summary>Копия списка активных соединений на текущий момент.</summary>
    public IReadOnlyList<ConnectionInfo> GetSnapshot()
    {
        lock (_lock)
            return _active.Values.ToList();
    }

    /// <summary>
    /// Бесконечный цикл опроса. Завершается, когда отменят cancellationToken.
    /// PeriodicTimer — современный таймер для async-кода: "жди следующего тика".
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                if (!IsPaused)
                {
                    try
                    {
                        PollOnce();
                    }
                    catch (Exception ex)
                    {
                        Error?.Invoke(ex);
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение: программу закрывают
        }
    }

    /// <summary>
    /// Один опрос: получить таблицу, сравнить с предыдущей, разослать изменения.
    /// Публичный — чтобы консольная версия могла сделать разовый снимок (--once).
    /// </summary>
    public void PollOnce()
    {
        IReadOnlyList<RawConnection> raw = _provider.GetConnections();
        DateTime now = DateTime.Now;

        // Порты, на которых что-то "слушает" (сервер). Если наше соединение использует такой
        // локальный порт — значит, это к нам подключились (входящее).
        HashSet<int> listeningPorts = raw
            .Where(r => r.Protocol == Protocol.Tcp && r.State == ConnectionState.Listen)
            .Select(r => r.LocalPort)
            .ToHashSet();

        var opened = new List<ConnectionInfo>();
        var closed = new List<ConnectionInfo>();
        List<ConnectionInfo> active;
        bool isInitial;

        lock (_lock)
        {
            isInitial = _isFirstPoll;
            _isFirstPoll = false;

            var seenKeys = new HashSet<string>();

            foreach (RawConnection r in raw)
            {
                string key = r.Key;
                if (!seenKeys.Add(key))
                    continue; // в редких случаях ОС отдаёт дубликаты — пропускаем

                if (_active.TryGetValue(key, out ConnectionInfo? existing))
                {
                    // Уже знакомое соединение — обновляем "живые" поля
                    existing.LastSeen = now;
                    existing.State = r.State;

                    // PID мог поменяться (или стать известным). PID = 0 игнорируем — см. RawConnection.Key
                    if (r.Pid != 0 && r.Pid != existing.Pid)
                    {
                        existing.Pid = r.Pid;
                        (existing.ProcessName, existing.ProcessPath) = _processes.Resolve(r.Pid);
                    }
                }
                else
                {
                    // Новое соединение
                    var (processName, processPath) = _processes.Resolve(r.Pid);

                    var info = new ConnectionInfo
                    {
                        Key = key,
                        Protocol = r.Protocol,
                        LocalAddress = r.LocalAddress,
                        LocalPort = r.LocalPort,
                        RemoteAddress = r.RemoteAddress,
                        RemotePort = r.RemotePort,
                        State = r.State,
                        Pid = r.Pid,
                        ProcessName = processName,
                        ProcessPath = processPath,
                        FirstSeen = now,
                        LastSeen = now,
                        ExistedAtStart = isInitial,
                        Direction = DetectDirection(r, listeningPorts)
                    };

                    _active[key] = info;
                    opened.Add(info);
                }
            }

            // Всё, что было раньше, но не встретилось сейчас, — закрылось
            foreach (string key in _active.Keys.Where(k => !seenKeys.Contains(k)).ToList())
            {
                ConnectionInfo info = _active[key];
                info.ClosedAt = now;
                _active.Remove(key);
                closed.Add(info);
            }

            active = _active.Values.ToList();
        }

        // Имена хостов узнаём асинхронно, чтобы не тормозить опрос.
        // "_ =" — явно показываем, что не ждём завершения задачи.
        if (_dns is not null)
        {
            foreach (ConnectionInfo info in opened)
            {
                if (info.HasRemote && NetworkHelpers.IsPublic(info.RemoteAddress))
                    _ = ResolveHostAsync(info);
            }
        }

        // Событие вызываем ВНЕ блокировки — иначе подписчик, вызвавший GetSnapshot(), мог бы зависнуть.
        Updated?.Invoke(new MonitorUpdate(opened, closed, active, isInitial));
    }

    private async Task ResolveHostAsync(ConnectionInfo info)
    {
        info.RemoteHost = await _dns!.ResolveAsync(info.RemoteAddress);
    }

    /// <summary>
    /// Эвристика направления:
    ///  - TCP LISTEN или UDP без удалённой стороны → "слушает";
    ///  - локальный порт совпадает со слушающим портом → к нам подключились ("входящее");
    ///  - иначе → мы подключились к кому-то ("исходящее").
    /// </summary>
    private static ConnectionDirection DetectDirection(RawConnection r, HashSet<int> listeningPorts)
    {
        if (r.Protocol == Protocol.Udp)
            return r.RemotePort == 0 ? ConnectionDirection.Listening : ConnectionDirection.Outgoing;

        if (r.State == ConnectionState.Listen)
            return ConnectionDirection.Listening;

        return listeningPorts.Contains(r.LocalPort) ? ConnectionDirection.Incoming : ConnectionDirection.Outgoing;
    }
}
