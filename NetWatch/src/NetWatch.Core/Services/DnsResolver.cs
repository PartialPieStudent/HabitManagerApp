using System.Collections.Concurrent;
using System.Net;

namespace NetWatch.Core.Services;

/// <summary>
/// Обратный DNS (reverse DNS, PTR-запись): по IP-адресу узнаём имя хоста.
/// Например, 140.82.112.4 → lb-140-82-112-4-iad.github.com — сразу понятно, что это GitHub.
///
/// У многих адресов PTR-записи нет, тогда возвращается null.
/// Результаты кэшируются навсегда (на время работы программы): адресов немного, а DNS-запросы медленные.
/// </summary>
public sealed class DnsResolver
{
    // Храним не строку, а Task<string?>: если два соединения к одному IP появились одновременно,
    // второй вызов получит ту же задачу и не будет делать повторный запрос.
    private readonly ConcurrentDictionary<string, Task<string?>> _cache = new();

    public Task<string?> ResolveAsync(IPAddress address) =>
        _cache.GetOrAdd(address.ToString(), static (key) => LookupAsync(key));

    private static async Task<string?> LookupAsync(string ip)
    {
        try
        {
            // Ограничиваем время ожидания: некоторые DNS-серверы отвечают очень долго
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            // Если передать в GetHostEntryAsync строку с IP, .NET сделает именно обратный запрос
            IPHostEntry entry = await Dns.GetHostEntryAsync(ip, timeout.Token);

            // Иногда в ответ возвращается сам IP — это значит, что имени нет
            return string.IsNullOrEmpty(entry.HostName) || entry.HostName == ip ? null : entry.HostName;
        }
        catch
        {
            // Нет PTR-записи, таймаут, нет сети — во всех случаях просто "имени нет"
            return null;
        }
    }
}
