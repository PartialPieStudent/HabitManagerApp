using Avalonia;

namespace NetWatch.Desktop;

/// <summary>
/// Точка входа графического приложения. Этот файл почти не меняется —
/// он такой же, как в стандартном шаблоне Avalonia.
/// </summary>
internal static class Program
{
    // [STAThread] — требование Windows для UI-потока (буфер обмена, диалоги и т.п.)
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Настройка приложения. Этот метод также использует дизайнер (превью XAML в IDE),
    /// поэтому его нельзя удалять или переименовывать.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()   // App — наш класс из App.axaml.cs
            .UsePlatformDetect()      // сам выберет Win32 / X11 / macOS
            .WithInterFont()          // шрифт Inter
            .LogToTrace();            // ошибки Avalonia — в отладочный вывод IDE
}
