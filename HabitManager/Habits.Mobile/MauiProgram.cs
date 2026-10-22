using Habits.Mobile.Services;
using Habits.Mobile.Services.Api;
using Habits.Mobile.ViewModels;
using Habits.Mobile.Views;
using Microsoft.Extensions.Logging;
using Habits.Mobile.Services.Api;

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
        builder.Services.AddSingleton<ICurrentUserService, DemoUserService>();

        // md 3 (dati glabājas RAMā) :
        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(ApiSettings.BaseUrl),
            Timeout = ApiSettings.Timeout
        });
        builder.Services.AddSingleton<IHabitService, ApiHabitService>();

        // Šīs bija md 2:
        // builder.Services.AddSingleton<IHabitService>(sp =>
        //     new LocalHabitService(
        //         sp.GetRequiredService<ICurrentUserService>(),
        //         Path.Combine(FileSystem.AppDataDirectory, "habits.json")));


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
