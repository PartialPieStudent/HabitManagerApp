using NetWatch.Core.Models;

namespace NetWatch.Core.Providers;

/// <summary>
/// Интерфейс "источника соединений". У каждой ОС своя реализация:
///   - WindowsConnectionProvider — через функции WinAPI из iphlpapi.dll;
///   - LinuxConnectionProvider   — через файлы /proc/net/tcp, /proc/net/udp и т.д.
///
/// Остальной код работает только с этим интерфейсом и не знает, на какой ОС запущен.
/// Если захотите добавить macOS — достаточно написать ещё одну реализацию.
/// </summary>
public interface IConnectionProvider
{
    /// <summary>Возвращает текущий снимок всех TCP и UDP сокетов (IPv4 и IPv6).</summary>
    IReadOnlyList<RawConnection> GetConnections();
}

/// <summary>
/// "Фабрика" — выбирает нужную реализацию в зависимости от операционной системы.
/// </summary>
public static class ConnectionProviderFactory
{
    public static IConnectionProvider Create()
    {
        // OperatingSystem.IsWindows()/IsLinux() — встроенные проверки .NET.
        // Анализатор кода понимает их и не ругается на вызов Windows-only кода внутри if.
        if (OperatingSystem.IsWindows())
            return new WindowsConnectionProvider();

        if (OperatingSystem.IsLinux())
            return new LinuxConnectionProvider();

        throw new PlatformNotSupportedException(
            "Поддерживаются только Windows и Linux. Для macOS нужно написать свой провайдер (например, через вызов 'lsof -i' или 'netstat').");
    }
}
