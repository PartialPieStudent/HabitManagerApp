using System.Net;
using System.Net.Sockets;
using NetWatch.Core.Analysis;

namespace NetWatch.Core.Models;

/// <summary>
/// Полная информация о соединении: данные от ОС + то, что мы вычислили сами
/// (процесс, время появления, имя хоста, вердикт анализатора).
///
/// Это обычный изменяемый класс (не record), потому что часть полей обновляется со временем:
/// LastSeen — при каждом опросе, RemoteHost — когда придёт ответ DNS, Risk — после анализа.
///
/// Замечание про потоки: поля меняются из фонового потока монитора и потока анализа,
/// а читаются интерфейсом. Для учебного проекта это допустимо (значения просто "чуть запаздывают"),
/// в серьёзном приложении стоит делать неизменяемые снимки или блокировки.
/// </summary>
public sealed class ConnectionInfo
{
    // ---- Неизменяемые данные (init — можно задать только при создании объекта) ----

    /// <summary>Ключ соединения (см. RawConnection.Key).</summary>
    public required string Key { get; init; }

    public required Protocol Protocol { get; init; }
    public required IPAddress LocalAddress { get; init; }
    public required int LocalPort { get; init; }
    public required IPAddress RemoteAddress { get; init; }
    public required int RemotePort { get; init; }

    /// <summary>Когда программа впервые увидела это соединение.</summary>
    public required DateTime FirstSeen { get; init; }

    /// <summary>
    /// true — соединение уже существовало, когда программа запустилась.
    /// Тогда настоящее время открытия неизвестно, а FirstSeen = время запуска программы.
    /// </summary>
    public bool ExistedAtStart { get; init; }

    // ---- Изменяемые данные ----

    public ConnectionState State { get; set; }
    public ConnectionDirection Direction { get; set; }

    /// <summary>ID процесса-владельца.</summary>
    public int Pid { get; set; }

    /// <summary>Имя процесса (например, chrome или firefox).</summary>
    public string ProcessName { get; set; } = "?";

    /// <summary>Полный путь к исполняемому файлу процесса (если удалось узнать).</summary>
    public string? ProcessPath { get; set; }

    /// <summary>Имя удалённого хоста по reverse DNS (например, lb-140-82-112-4-iad.github.com).</summary>
    public string? RemoteHost { get; set; }

    /// <summary>Когда соединение было в последний раз замечено в таблице ОС.</summary>
    public DateTime LastSeen { get; set; }

    /// <summary>Когда соединение исчезло из таблицы (null — ещё активно).</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Уровень риска от последнего анализатора (null — ещё не проверялось).</summary>
    public RiskLevel? Risk { get; set; }

    /// <summary>Текстовый вердикт последнего анализатора.</summary>
    public string? Verdict { get; set; }

    /// <summary>ID строки в базе истории (SQLite). Нужен, чтобы потом обновить запись при закрытии.</summary>
    public long? HistoryId { get; set; }

    // ---- Вычисляемые свойства (=> означает "свойство только для чтения, считается каждый раз") ----

    /// <summary>Сколько соединение живёт (или жило).</summary>
    public TimeSpan Duration => (ClosedAt ?? LastSeen) - FirstSeen;

    /// <summary>Локальный адрес в виде "IP:порт".</summary>
    public string LocalEndPoint => FormatEndPoint(LocalAddress, LocalPort);

    /// <summary>
    /// Удалённый адрес в виде "IP:порт" или "*", если удалённой стороны нет
    /// (слушающий TCP-сокет или обычный UDP-сокет).
    /// </summary>
    public string RemoteEndPoint =>
        RemotePort == 0 && IsUnspecified(RemoteAddress) ? "*" : FormatEndPoint(RemoteAddress, RemotePort);

    /// <summary>Есть ли у соединения реальная удалённая сторона.</summary>
    public bool HasRemote => !(RemotePort == 0 && IsUnspecified(RemoteAddress));

    /// <summary>IPv6-адреса принято писать в квадратных скобках перед портом: [::1]:443.</summary>
    private static string FormatEndPoint(IPAddress address, int port) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]:{port}" : $"{address}:{port}";

    /// <summary>0.0.0.0 или :: — "любой адрес".</summary>
    private static bool IsUnspecified(IPAddress address) =>
        address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
}
