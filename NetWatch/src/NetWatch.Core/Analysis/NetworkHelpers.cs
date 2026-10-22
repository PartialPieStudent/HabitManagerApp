using System.Net;
using System.Net.Sockets;

namespace NetWatch.Core.Analysis;

/// <summary>
/// Сетевые помощники.
/// </summary>
public static class NetworkHelpers
{
    /// <summary>
    /// true, если адрес "публичный" — то есть находится в интернете.
    ///
    /// Зачем это нужно: локальные адреса (ваш роутер 192.168.1.1, localhost и т.п.) нет смысла
    /// проверять в облачных базах угроз — данных о них там нет, а отправлять их наружу незачем
    /// (это лишняя утечка информации о вашей сети).
    /// </summary>
    public static bool IsPublic(IPAddress address)
    {
        // ::ffff:8.8.8.8 → 8.8.8.8
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any))
            return false;

        byte[] b = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(
                b[0] == 0 ||                                   // 0.0.0.0/8      — "этот хост"
                b[0] == 10 ||                                  // 10.0.0.0/8     — частная сеть
                (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||  // 100.64.0.0/10  — CGNAT провайдера
                (b[0] == 169 && b[1] == 254) ||                // 169.254.0.0/16 — link-local
                (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||   // 172.16.0.0/12  — частная сеть
                (b[0] == 192 && b[1] == 168) ||                // 192.168.0.0/16 — частная сеть
                b[0] >= 224);                                  // мультикаст и зарезервированные
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(
                address.IsIPv6LinkLocal ||   // fe80::/10
                address.IsIPv6SiteLocal ||   // fec0::/10 (устаревшие)
                address.IsIPv6Multicast ||   // ff00::/8
                (b[0] & 0xFE) == 0xFC);      // fc00::/7 — уникальные локальные адреса (аналог 192.168.x.x)
        }

        return false;
    }
}
