namespace NetWatch.Core.Models;

/// <summary>
/// Транспортный протокол соединения.
/// TCP — "звонок" с установкой соединения (веб, почта, SSH...).
/// UDP — отправка пакетов без установки соединения (DNS, игры, видеозвонки...).
/// </summary>
public enum Protocol
{
    Tcp,
    Udp
}

/// <summary>
/// Состояние TCP-соединения (то же, что показывает команда netstat).
/// Для UDP используются только Bound (сокет открыт) и Established (сокет "привязан" к удалённому адресу).
/// </summary>
public enum ConnectionState
{
    Unknown,
    Listen,       // программа ждёт входящих подключений (это сервер)
    SynSent,      // мы отправили запрос на подключение и ждём ответа
    SynReceived,  // к нам пришёл запрос на подключение
    Established,  // соединение установлено, идёт обмен данными
    FinWait1,     // дальше — разные стадии закрытия соединения
    FinWait2,
    CloseWait,
    Closing,
    LastAck,
    TimeWait,     // соединение закрыто, но система ещё немного "держит" его
    Closed,
    DeleteTcb,
    Bound         // только для UDP: сокет открыт и слушает порт
}

/// <summary>
/// Направление соединения — определяется программой по эвристике (см. ConnectionMonitor.DetectDirection).
/// </summary>
public enum ConnectionDirection
{
    Unknown,
    Listening, // порт открыт и ждёт подключений
    Incoming,  // кто-то подключился К нам
    Outgoing   // мы подключились к кому-то
}

/// <summary>
/// Вспомогательные методы для красивого вывода перечислений.
/// "this" в первом параметре делает метод "расширением": можно писать state.ToDisplayString().
/// </summary>
public static class EnumDisplayExtensions
{
    /// <summary>Название состояния в стиле netstat (ESTABLISHED, LISTEN...).</summary>
    public static string ToDisplayString(this ConnectionState state) => state switch
    {
        ConnectionState.Listen => "LISTEN",
        ConnectionState.SynSent => "SYN_SENT",
        ConnectionState.SynReceived => "SYN_RECV",
        ConnectionState.Established => "ESTABLISHED",
        ConnectionState.FinWait1 => "FIN_WAIT1",
        ConnectionState.FinWait2 => "FIN_WAIT2",
        ConnectionState.CloseWait => "CLOSE_WAIT",
        ConnectionState.Closing => "CLOSING",
        ConnectionState.LastAck => "LAST_ACK",
        ConnectionState.TimeWait => "TIME_WAIT",
        ConnectionState.Closed => "CLOSED",
        ConnectionState.DeleteTcb => "DELETE_TCB",
        ConnectionState.Bound => "BOUND",
        _ => "?"
    };

    /// <summary>Направление по-русски.</summary>
    public static string ToDisplayString(this ConnectionDirection direction) => direction switch
    {
        ConnectionDirection.Listening => "Слушает",
        ConnectionDirection.Incoming => "← Входящее",
        ConnectionDirection.Outgoing => "→ Исходящее",
        _ => "?"
    };
}
