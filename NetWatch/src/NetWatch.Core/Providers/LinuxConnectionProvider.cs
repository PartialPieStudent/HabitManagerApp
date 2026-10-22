using System.Globalization;
using System.Net;
using System.Runtime.Versioning;
using NetWatch.Core.Models;

namespace NetWatch.Core.Providers;

/// <summary>
/// Получение таблицы соединений в Linux.
///
/// В Linux ядро "показывает" таблицы сокетов как текстовые файлы:
///   /proc/net/tcp   /proc/net/tcp6   /proc/net/udp   /proc/net/udp6
/// Пример строки из /proc/net/tcp:
///   sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode
///    0: 0100007F:0277 00000000:0000 0A 00000000:00000000 00:00000000 00000000     0        0 23456 ...
///
/// В этих файлах НЕТ PID процесса — только номер inode сокета.
/// Чтобы найти процесс, перебираем /proc/[pid]/fd/* — это ссылки на открытые файлы процесса;
/// у сокетов ссылка выглядит как "socket:[23456]". Совпал inode — нашли владельца.
///
/// ВАЖНО: без root видны только ваши собственные процессы. Чтобы видеть PID всех процессов,
/// запускайте программу через sudo (подробнее в GUIDE.md).
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxConnectionProvider : IConnectionProvider
{
    public IReadOnlyList<RawConnection> GetConnections()
    {
        // Сначала строим словарь "inode сокета → PID", потом читаем таблицы.
        Dictionary<long, int> inodeToPid = BuildInodeToPidMap();

        var result = new List<RawConnection>(512);
        ParseFile("/proc/net/tcp", Protocol.Tcp, isIPv6: false, inodeToPid, result);
        ParseFile("/proc/net/tcp6", Protocol.Tcp, isIPv6: true, inodeToPid, result);
        ParseFile("/proc/net/udp", Protocol.Udp, isIPv6: false, inodeToPid, result);
        ParseFile("/proc/net/udp6", Protocol.Udp, isIPv6: true, inodeToPid, result);
        return result;
    }

    /// <summary>Разбирает один файл /proc/net/*.</summary>
    private static void ParseFile(string path, Protocol protocol, bool isIPv6,
        Dictionary<long, int> inodeToPid, List<RawConnection> output)
    {
        // tcp6/udp6 может не быть, если IPv6 отключён
        if (!File.Exists(path))
            return;

        // Skip(1) — пропускаем строку-заголовок
        foreach (string line in File.ReadLines(path).Skip(1))
        {
            // Колонки разделены переменным числом пробелов
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 10)
                continue;

            try
            {
                var (localAddress, localPort) = ParseEndPoint(parts[1], isIPv6);
                var (remoteAddress, remotePort) = ParseEndPoint(parts[2], isIPv6);
                int rawState = int.Parse(parts[3], NumberStyles.HexNumber);
                long inode = long.Parse(parts[9], CultureInfo.InvariantCulture);

                ConnectionState state = protocol == Protocol.Tcp
                    ? MapTcpState(rawState)
                    // UDP: если указан удалённый порт — сокет "подключён" (connect()), иначе просто открыт
                    : remotePort != 0 ? ConnectionState.Established : ConnectionState.Bound;

                // Если PID не нашли (нет прав или процесс уже завершился) — 0
                int pid = inodeToPid.TryGetValue(inode, out int found) ? found : 0;

                output.Add(new RawConnection(protocol, localAddress, localPort, remoteAddress, remotePort, state, pid));
            }
            catch (FormatException)
            {
                // Неожиданный формат строки — просто пропускаем её
            }
        }
    }

    /// <summary>
    /// Разбор "адрес:порт" в шестнадцатеричном виде.
    ///   IPv4: "0100007F:0277"  → 127.0.0.1:631
    ///   IPv6: 32 hex-символа (четыре 32-битных слова) + ":порт"
    ///
    /// Ядро печатает адрес как числа в порядке байт процессора (на x86/ARM — little-endian),
    /// поэтому, разобрав hex в uint и взяв его байты через BitConverter, мы получаем адрес
    /// в нормальном сетевом порядке. Порт же печатается обычным числом.
    /// </summary>
    private static (IPAddress Address, int Port) ParseEndPoint(string text, bool isIPv6)
    {
        int colon = text.IndexOf(':');
        string addressHex = text[..colon];
        int port = int.Parse(text[(colon + 1)..], NumberStyles.HexNumber);

        if (!isIPv6)
        {
            uint value = uint.Parse(addressHex, NumberStyles.HexNumber);
            return (new IPAddress(value), port);
        }

        var bytes = new byte[16];
        for (int word = 0; word < 4; word++)
        {
            uint value = uint.Parse(addressHex.AsSpan(word * 8, 8), NumberStyles.HexNumber);
            BitConverter.GetBytes(value).CopyTo(bytes, word * 4);
        }

        var address = new IPAddress(bytes);

        // Ядро показывает IPv4-соединения на IPv6-сокетах как ::ffff:1.2.3.4 — превращаем в обычный IPv4
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return (address, port);
    }

    /// <summary>
    /// Перебирает все процессы и их открытые файлы, собирая соответствие inode сокета → PID.
    /// Это самая "дорогая" операция провайдера; при опросе раз в 1–2 секунды это нормально.
    /// </summary>
    private static Dictionary<long, int> BuildInodeToPidMap()
    {
        var map = new Dictionary<long, int>();

        foreach (string processDir in Directory.EnumerateDirectories("/proc"))
        {
            // Папки процессов называются числами (PID). Остальные (/proc/net, /proc/sys...) пропускаем.
            if (!int.TryParse(Path.GetFileName(processDir), out int pid))
                continue;

            try
            {
                foreach (string fd in Directory.EnumerateFileSystemEntries(Path.Combine(processDir, "fd")))
                {
                    // LinkTarget — куда указывает символическая ссылка, например "socket:[23456]"
                    string? target = new FileInfo(fd).LinkTarget;
                    if (target is null || !target.StartsWith("socket:[", StringComparison.Ordinal))
                        continue;

                    // Вырезаем число между "socket:[" и "]"
                    ReadOnlySpan<char> number = target.AsSpan(8, target.Length - 9);
                    if (long.TryParse(number, out long inode))
                        map.TryAdd(inode, pid);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Нет прав на чужой процесс или процесс завершился, пока мы его читали — это нормально.
                // (DirectoryNotFoundException — наследник IOException, поэтому тоже ловится здесь.)
            }
        }

        return map;
    }

    /// <summary>Коды состояний TCP из исходников ядра Linux (include/net/tcp_states.h).</summary>
    private static ConnectionState MapTcpState(int state) => state switch
    {
        0x01 => ConnectionState.Established,
        0x02 => ConnectionState.SynSent,
        0x03 => ConnectionState.SynReceived,
        0x04 => ConnectionState.FinWait1,
        0x05 => ConnectionState.FinWait2,
        0x06 => ConnectionState.TimeWait,
        0x07 => ConnectionState.Closed,
        0x08 => ConnectionState.CloseWait,
        0x09 => ConnectionState.LastAck,
        0x0A => ConnectionState.Listen,
        0x0B => ConnectionState.Closing,
        _ => ConnectionState.Unknown
    };
}
