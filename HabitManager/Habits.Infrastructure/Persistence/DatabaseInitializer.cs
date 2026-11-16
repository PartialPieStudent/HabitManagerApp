using Habits.Domain.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Habits.Infrastructure.Persistence;


/*
Sagatavo db uz API startu: izveido to,
ja nepieciešams, pielieto visas EF Core migrācijas 
(lai vienāda struktūra būtu uz jebkura datora) un,
ja datubāze ir tukša, tad ģenerē testu datus no 1. uzdevuma.
*/
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // Tas ir nepieciešams, jo DbContext ir scoped, un šeit mēs esam ārpus HTTP pieprasījuma konteksta.
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HabitsDbContext>();

        //Izveido datubāzi, ja tā neeksistē, un pielieto visas migrācijas.
        await db.Database.MigrateAsync(cancellationToken);

        // Ja datubāze jau satur ierakstus, tad neizpilda testu datu ģenerēšanu.
        if (await db.Habits.AnyAsync(cancellationToken))
        {
            return;
        }

        var repository = new EfHabitRepository(db);
        foreach (var habit in TestDataGenerator.GenerateHabits())
        {
            await repository.AddAsync(habit, cancellationToken);
        }
    }
}
