using System.Net;

namespace NetWatch.Core.Models;

/// <summary>
/// "Сырая" строка из таблицы соединений операционной системы — ровно то, что отдаёт ОС,
/// без имени процесса, времени и прочих дополнений.
///
/// record — это класс, у которого компилятор сам генерирует конструктор, свойства,
/// сравнение по значению и ToString(). Идеально для простых "контейнеров данных".
/// </summary>
/// <param name="Protocol">TCP или UDP.</param>
/// <param name="LocalAddress">Наш IP-адрес (на каком сетевом интерфейсе открыт сокет).</param>
/// <param name="LocalPort">Наш порт.</param>
/// <param name="RemoteAddress">IP удалённой стороны (для слушающих сокетов — 0.0.0.0 или ::).</param>
/// <param name="RemotePort">Порт удалённой стороны (0, если её нет).</param>
/// <param name="State">Состояние соединения.</param>
/// <param name="Pid">Идентификатор процесса-владельца (0 — неизвестен или системный).</param>
public sealed record RawConnection(
    Protocol Protocol,
    IPAddress LocalAddress,
    int LocalPort,
    IPAddress RemoteAddress,
    int RemotePort,
    ConnectionState State,
    int Pid)
{
    /// <summary>
    /// Уникальный ключ соединения. По нему мы узнаём "то же самое" соединение при следующем опросе.
    /// PID в ключ НЕ входит: когда TCP-соединение уходит в TIME_WAIT, Windows показывает PID = 0,
    /// и без этого мы бы ошибочно посчитали это новым соединением.
    /// </summary>
    public string Key => $"{Protocol}|{LocalAddress}:{LocalPort}|{RemoteAddress}:{RemotePort}";
}
