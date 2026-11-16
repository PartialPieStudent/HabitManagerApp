using Habits.Application.Abstractions;
using Habits.Domain.Entities;

namespace Habits.Tests.Fakes;


/*
Testa dati glabājas atmiņā nevis DB, glabā kopijas, servis redz tikai saglabātas izmaiņas

 */
internal sealed class FakeHabitRepository : IHabitRepository
{
    private readonly Dictionary<Guid, Habit> _store = new();

    //Ieliekam ieradumus, lai testos varētu tos atrast
    public void Seed(params Habit[] habits)
    {
        foreach (var habit in habits)
        {
            _store[habit.Id] = Copy(habit);
        }
    }

    //Kas ir glabāts šolaik
    public Habit? Find(Guid habitId) =>
        _store.TryGetValue(habitId, out var habit) ? Copy(habit) : null;

    public Task<IReadOnlyList<Habit>> GetByUserAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Habit>>(
            _store.Values.Where(h => h.UserId == userId).Select(Copy).ToList());

    public Task<Habit?> GetByIdAsync(Guid habitId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Find(habitId));

    public Task AddAsync(Habit habit, CancellationToken cancellationToken = default)
    {
        _store[habit.Id] = Copy(habit);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Habit habit, CancellationToken cancellationToken = default)
    {
        _store[habit.Id] = Copy(habit);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid habitId, CancellationToken cancellationToken = default)
    {
        _store.Remove(habitId);
        return Task.CompletedTask;
    }

    private static Habit Copy(Habit habit) => Habit.Restore(
        habit.Id,
        habit.UserId,
        habit.Title,
        habit.Description,
        habit.IsActive,
        habit.CreatedAt,
        habit.UpdatedAt,
        habit.RecurrenceRule,
        habit.Completions.Select(c => (c.Id, c.Date)));
}
