using Habits.Mobile.Services;
using Habits.Mobile.ViewModels;
using Habits.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace Habits.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ---------- Services (dati un šoreizējajs lietotājs) ----------
        // Šeit būs ApiHabitService reģistrācija drusku vēlāk
        builder.Services.AddSingleton<ICurrentUserService, DemoUserService>();
        builder.Services.AddSingleton<IHabitService>(sp =>
            new LocalHabitService(
                sp.GetRequiredService<ICurrentUserService>(),
                Path.Combine(FileSystem.AppDataDirectory, "habits.json")));
        

        // ---------- UI palīgi ----------
        builder.Services.AddSingleton<IDialogService, ShellDialogService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();

        // ---------- Lapas un view models ----------
        builder.Services.AddTransient<HabitsViewModel>();
        builder.Services.AddTransient<HabitsPage>();

        builder.Services.AddTransient<HabitDetailViewModel>();
        builder.Services.AddTransient<HabitDetailPage>();

        builder.Services.AddTransient<HabitEditViewModel>();
        builder.Services.AddTransient<HabitEditPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
