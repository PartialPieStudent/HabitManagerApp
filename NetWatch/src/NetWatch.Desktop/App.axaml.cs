using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NetWatch.Core;
using NetWatch.Core.Settings;
using NetWatch.Desktop.ViewModels;
using NetWatch.Desktop.Views;

namespace NetWatch.Desktop;

/// <summary>
/// "Код за разметкой" (code-behind) для App.axaml.
/// partial — вторая часть класса генерируется из XAML автоматически.
/// </summary>
public partial class App : Application
{
    /// <summary>Загружает разметку из App.axaml.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Вызывается, когда Avalonia готова. Здесь мы создаём ядро, модель представления (ViewModel)
    /// и главное окно, и связываем их.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        // ApplicationLifetime для обычного десктопного приложения — IClassicDesktopStyleApplicationLifetime
        // (для мобильных/браузерных платформ был бы другой тип).
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var engine = new NetWatchEngine(AppSettings.Load());
            var viewModel = new MainWindowViewModel(engine);

            // DataContext — объект, к свойствам которого "привязываются" элементы окна через {Binding ...}
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // При выходе — корректно остановить мониторинг и закрыть базу
            desktop.Exit += (_, _) => viewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
