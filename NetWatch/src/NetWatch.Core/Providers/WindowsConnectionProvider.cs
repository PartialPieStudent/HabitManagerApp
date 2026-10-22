using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NetWatch.Core.Models;

namespace NetWatch.Core.Providers;

/// <summary>
/// Получение таблицы соединений в Windows через WinAPI.
///
/// Почему не стандартный IPGlobalProperties.GetActiveTcpConnections()?
/// Потому что он НЕ возвращает PID процесса — а без него не узнать, какая программа держит соединение.
/// Функции GetExtendedTcpTable / GetExtendedUdpTable из системной библиотеки iphlpapi.dll умеют это.
///
/// Вызов функций из нативных DLL называется P/Invoke (Platform Invoke).
/// Атрибут [SupportedOSPlatform("windows")] говорит компилятору, что класс работает только в Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsConnectionProvider : IConnectionProvider
{
    // ---------------- Константы из заголовочных файлов Windows SDK ----------------

    private const int AF_INET = 2;   // семейство адресов IPv4
    private const int AF_INET6 = 23; // семейство адресов IPv6

    // Какую таблицу просим: "все TCP-соединения + PID владельца" (TCP_TABLE_OWNER_PID_ALL = 5)
    private const int TCP_TABLE_OWNER_PID_ALL = 5;
    // "все UDP-сокеты + PID владельца" (UDP_TABLE_OWNER_PID = 1)
    private const int UDP_TABLE_OWNER_PID = 1;

    private const uint NO_ERROR = 0;
    private const uint ERROR_INSUFFICIENT_BUFFER = 122; // "буфер мал" — надо выделить больше памяти

    // ---------------- Объявления нативных функций ----------------
    // Сигнатура повторяет C-функцию:
    // DWORD GetExtendedTcpTable(PVOID pTcpTable, PDWORD pdwSize, BOOL bOrder, ULONG ulAf, TCP_TABLE_CLASS TableClass, ULONG Reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

    /// <summary>Тип-делегат, под который подходят обе функции выше. Нужен, чтобы не дублировать код чтения.</summary>
    private delegate uint TableFunction(
        IntPtr buffer, ref int size, bool order, int addressFamily, int tableClass, uint reserved);

    public IReadOnlyList<RawConnection> GetConnections()
    {
        var result = new List<RawConnection>(512);

        ReadTable(GetExtendedTcpTable, AF_INET, TCP_TABLE_OWNER_PID_ALL, (rows, count) => ReadTcp4(rows, count, result));
        ReadTable(GetExtendedTcpTable, AF_INET6, TCP_TABLE_OWNER_PID_ALL, (rows, count) => ReadTcp6(rows, count, result));
        ReadTable(GetExtendedUdpTable, AF_INET, UDP_TABLE_OWNER_PID, (rows, count) => ReadUdp4(rows, count, result));
        ReadTable(GetExtendedUdpTable, AF_INET6, UDP_TABLE_OWNER_PID, (rows, count) => ReadUdp6(rows, count, result));

        return result;
    }

    /// <summary>
    /// Общая логика вызова GetExtended*Table:
    /// 1) вызываем с пустым буфером — функция сообщает, сколько байт нужно;
    /// 2) выделяем память и вызываем снова;
    /// 3) если за это время появились новые соединения и памяти не хватило — повторяем.
    ///
    /// Формат результата в памяти:  [DWORD количество строк][строка 0][строка 1]...
    /// </summary>
    private static void ReadTable(TableFunction function, int addressFamily, int tableClass, Action<IntPtr, int> readRows)
    {
        int size = 0;
        function(IntPtr.Zero, ref size, false, addressFamily, tableClass, 0);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            // Marshal.AllocHGlobal выделяет "неуправляемую" память — сборщик мусора её не трогает,
            // поэтому её ОБЯЗАТЕЛЬНО надо освободить вручную (делаем в finally).
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                uint error = function(buffer, ref size, false, addressFamily, tableClass, 0);

                if (error == NO_ERROR)
                {
                    int rowCount = Marshal.ReadInt32(buffer);  // первые 4 байта — число строк
                    readRows(buffer + 4, rowCount);            // строки идут сразу после
                    return;
                }

                if (error != ERROR_INSUFFICIENT_BUFFER)
                    throw new Win32Exception((int)error); // превратит код ошибки Windows в понятный текст

                // иначе size уже обновлён функцией — просто пробуем ещё раз
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    // ---------------- Разбор строк таблиц ----------------
    // Вместо объявления структур с массивами мы читаем поля по смещениям (offset) —
    // так нагляднее видно, как устроена память. Все поля — 4-байтовые DWORD, кроме IPv6-адресов (16 байт).

    /// <summary>
    /// MIB_TCPROW_OWNER_PID (24 байта):
    ///   0  dwState
    ///   4  dwLocalAddr
    ///   8  dwLocalPort
    ///   12 dwRemoteAddr
    ///   16 dwRemotePort
    ///   20 dwOwningPid
    /// </summary>
    private static void ReadTcp4(IntPtr rows, int count, List<RawConnection> output)
    {
        const int rowSize = 24;
        for (int i = 0; i < count; i++)
        {
            IntPtr row = rows + i * rowSize;
            output.Add(new RawConnection(
                Protocol.Tcp,
                ReadIPv4(row, 4), ReadPort(row, 8),
                ReadIPv4(row, 12), ReadPort(row, 16),
                MapTcpState(Marshal.ReadInt32(row, 0)),
                Marshal.ReadInt32(row, 20)));
        }
    }

    /// <summary>
    /// MIB_TCP6ROW_OWNER_PID (56 байт):
    ///   0  ucLocalAddr[16]
    ///   16 dwLocalScopeId
    ///   20 dwLocalPort
    ///   24 ucRemoteAddr[16]
    ///   40 dwRemoteScopeId
    ///   44 dwRemotePort
    ///   48 dwState
    ///   52 dwOwningPid
    /// </summary>
    private static void ReadTcp6(IntPtr rows, int count, List<RawConnection> output)
    {
        const int rowSize = 56;
        for (int i = 0; i < count; i++)
        {
            IntPtr row = rows + i * rowSize;
            output.Add(new RawConnection(
                Protocol.Tcp,
                ReadIPv6(row, 0, 16), ReadPort(row, 20),
                ReadIPv6(row, 24, 40), ReadPort(row, 44),
                MapTcpState(Marshal.ReadInt32(row, 48)),
                Marshal.ReadInt32(row, 52)));
        }
    }

    /// <summary>
    /// MIB_UDPROW_OWNER_PID (12 байт):
    ///   0 dwLocalAddr, 4 dwLocalPort, 8 dwOwningPid
    /// У UDP нет "соединения", поэтому удалённого адреса в таблице нет.
    /// </summary>
    private static void ReadUdp4(IntPtr rows, int count, List<RawConnection> output)
    {
        const int rowSize = 12;
        for (int i = 0; i < count; i++)
        {
            IntPtr row = rows + i * rowSize;
            output.Add(new RawConnection(
                Protocol.Udp,
                ReadIPv4(row, 0), ReadPort(row, 4),
                IPAddress.Any, 0,
                ConnectionState.Bound,
                Marshal.ReadInt32(row, 8)));
        }
    }

    /// <summary>
    /// MIB_UDP6ROW_OWNER_PID (28 байт):
    ///   0 ucLocalAddr[16], 16 dwLocalScopeId, 20 dwLocalPort, 24 dwOwningPid
    /// </summary>
    private static void ReadUdp6(IntPtr rows, int count, List<RawConnection> output)
    {
        const int rowSize = 28;
        for (int i = 0; i < count; i++)
        {
            IntPtr row = rows + i * rowSize;
            output.Add(new RawConnection(
                Protocol.Udp,
                ReadIPv6(row, 0, 16), ReadPort(row, 20),
                IPAddress.IPv6Any, 0,
                ConnectionState.Bound,
                Marshal.ReadInt32(row, 24)));
        }
    }

    // ---------------- Мелкие помощники ----------------

    /// <summary>
    /// IPv4-адрес хранится как 4 байта в сетевом порядке. Конструктор IPAddress(long)
    /// на x86/x64 (little-endian) интерпретирует число именно так, поэтому просто передаём его.
    /// (uint) — чтобы адреса вроде 200.x.x.x не превратились в отрицательные числа.
    /// </summary>
    private static IPAddress ReadIPv4(IntPtr row, int offset) =>
        new((uint)Marshal.ReadInt32(row, offset));

    /// <summary>IPv6: 16 байт адреса + отдельный ScopeId (номер интерфейса для link-local адресов).</summary>
    private static IPAddress ReadIPv6(IntPtr row, int addressOffset, int scopeOffset)
    {
        var bytes = new byte[16];
        Marshal.Copy(row + addressOffset, bytes, 0, 16);
        uint scopeId = (uint)Marshal.ReadInt32(row, scopeOffset);
        return new IPAddress(bytes, scopeId);
    }

    /// <summary>
    /// Порт хранится в младших 2 байтах DWORD в СЕТЕВОМ порядке (big-endian),
    /// поэтому меняем байты местами: 0x5000 → 0x0050 (=80).
    /// </summary>
    private static int ReadPort(IntPtr row, int offset)
    {
        uint raw = (uint)Marshal.ReadInt32(row, offset);
        return (int)(((raw & 0xFF) << 8) | ((raw >> 8) & 0xFF));
    }

    /// <summary>Значения MIB_TCP_STATE из Windows SDK.</summary>
    private static ConnectionState MapTcpState(int state) => state switch
    {
        1 => ConnectionState.Closed,
        2 => ConnectionState.Listen,
        3 => ConnectionState.SynSent,
        4 => ConnectionState.SynReceived,
        5 => ConnectionState.Established,
        6 => ConnectionState.FinWait1,
        7 => ConnectionState.FinWait2,
        8 => ConnectionState.CloseWait,
        9 => ConnectionState.Closing,
        10 => ConnectionState.LastAck,
        11 => ConnectionState.TimeWait,
        12 => ConnectionState.DeleteTcb,
        _ => ConnectionState.Unknown
    };
}
