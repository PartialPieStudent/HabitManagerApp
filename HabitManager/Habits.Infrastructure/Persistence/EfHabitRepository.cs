using Habits.Application.Abstractions;
using Habits.Domain.Entities;
using Habits.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Habits.Infrastructure.Persistence;

//IHabitRepository implementācija bet jau caur DB (EF Core +
// SQL Server).Aizvieto InMemoryHabitRepository bez izmaiņām aplikācijas slānī,mobīlās lietotnes API
public sealed class EfHabitRepository : IHabitRepository
{
    private readonly HabitsDbContext _db;

    public EfHabitRepository(HabitsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Habit>> GetByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entities = await _db.Habits
            .AsNoTracking()                 // read-only: EF nerdz izmaiņas
            .Include(h => h.Completions)    // ielādē arī saistītus izpildes ierakstus
            .Where(h => h.UserId == userId)
            .ToListAsync(cancellationToken);

        return entities.Select(HabitEntityMapper.ToDomain).ToList();
    }

    public async Task<Habit?> GetByIdAsync(Guid habitId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Habits
            .AsNoTracking()
            .Include(h => h.Completions)
            .FirstOrDefaultAsync(h => h.Id == habitId, cancellationToken);

        return entity is null ? null : HabitEntityMapper.ToDomain(entity);
    }

    public async Task AddAsync(Habit habit, CancellationToken cancellationToken = default)
    {
        _db.Habits.Add(HabitEntityMapper.ToEntity(habit));
        await SaveAsync(cancellationToken);
    }

    public async Task UpdateAsync(Habit habit, CancellationToken cancellationToken = default)
    {
        // Ielāde pašlaik esošo rindiņu (Vēro šo laiku, so EF redz kas ir mainijies).
        var entity = await _db.Habits
            .Include(h => h.Completions)
            .FirstOrDefaultAsync(h => h.Id == habit.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Habit {habit.Id} no longer exists.");

        HabitEntityMapper.CopyDetails(habit, entity);

        // Completions: dzēš rindinās kuras vairs nepastāv domenā
        var domainIds = habit.Completions.Select(c => c.Id).ToHashSet();
        var removed = entity.Completions.Where(c => !domainIds.Contains(c.Id)).ToList();
        _db.HabitCompletions.RemoveRange(removed);

        // un ievieto jaunas
        var storedIds = entity.Completions.Select(c => c.Id).ToHashSet();
        foreach (var completion in habit.Completions.Where(c => !storedIds.Contains(c.Id)))
        {
            _db.HabitCompletions.Add(new HabitCompletionEntity
            {
                Id = completion.Id,
                HabitId = habit.Id,
                Date = completion.Date
            });
        }

        await SaveAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid habitId, CancellationToken cancellationToken = default)
    {
        // viens DELETE apgalvojums (DB pati dzēš visus izpilduma irekastus - cascade delete).
        await _db.Habits
            .Where(h => h.Id == habitId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    
    // Saglabā izmaiņas.Unikāla indeksa "pārkāpums" (divi paralēli pieprasījumi vienlaikus) kļūst InvalidOperationException, kuru API pārvērš par 409 kļūdu
    
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException(
                "The change conflicts with existing data (for example, the habit is already completed on that date).",
                ex);
        }
    }

    // 2601 / 2627 = SQL servera "duplicate key" kļūdas
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
