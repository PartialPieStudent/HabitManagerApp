using System.Collections.Concurrent;
using System.Diagnostics;

namespace NetWatch.Core.Services;

/// <summary>
/// Определяет имя и путь процесса по его PID.
///
/// Запрос информации о процессе — относительно медленная операция, а один процесс (например, браузер)
/// может держать сотни соединений. Поэтому результаты кэшируются на короткое время.
/// Кэш с ограниченным временем жизни нужен потому, что ОС может переиспользовать PID:
/// процесс завершился, а его номер достался другому.
/// </summary>
public sealed class ProcessResolver
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    // ConcurrentDictionary — потокобезопасный словарь (можно читать/писать из разных потоков).
    private readonly ConcurrentDictionary<int, CachedProcess> _cache = new();

    private sealed record CachedProcess(string Name, string? Path, DateTime CachedAt);

    /// <summary>Возвращает (имя, путь) процесса. Путь может быть null, если нет прав его узнать.</summary>
    public (string Name, string? Path) Resolve(int pid)
    {
        // PID 0: в Windows это "System Idle Process" (так помечены, например, соединения в TIME_WAIT),
        // в Linux — значит, что владельца найти не удалось (обычно не хватает прав).
        if (pid <= 0)
            return (OperatingSystem.IsWindows() ? "[система]" : "[нет доступа]", null);

        if (_cache.TryGetValue(pid, out var cached) && DateTime.Now - cached.CachedAt < CacheLifetime)
            return (cached.Name, cached.Path);

        string name = "?";
        string? path = null;

        try
        {
            // using — объект Process будет корректно освобождён после использования
            using var process = Process.GetProcessById(pid);
            name = process.ProcessName;

            try
            {
                // MainModule часто недоступен без прав администратора (например, для системных служб)
                path = process.MainModule?.FileName;
            }
            catch
            {
                // Нет доступа — ничего страшного, просто путь будет пустым
            }
        }
        catch (ArgumentException)
        {
            // Процесса с таким PID уже нет
            name = "[завершён]";
        }
        catch (InvalidOperationException)
        {
            // Процесс завершился, пока мы читали его данные
            name = "[завершён]";
        }

        // В Linux путь можно узнать из ссылки /proc/PID/exe (если есть права)
        if (path is null && OperatingSystem.IsLinux())
        {
            try { path = new FileInfo($"/proc/{pid}/exe").LinkTarget; }
            catch { /* нет прав */ }
        }

        _cache[pid] = new CachedProcess(name, path, DateTime.Now);
        return (name, path);
    }
}
