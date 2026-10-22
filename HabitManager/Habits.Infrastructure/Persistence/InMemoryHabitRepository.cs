using Habits.Application.Abstractions;
using Habits.Domain.Entities;
using Habits.Domain.TestData;

namespace Habits.Infrastructure.Persistence;

public sealed class InMemoryHabitRepository : IHabitRepository
{
    private readonly Dictionary<Guid, Habit> _habits = new();
    private readonly object _lock = new();

    public InMemoryHabitRepository()
    {
        foreach (var habit in TestDataGenerator.GenerateHabits())
        {
            _habits[habit.Id] = Clone(habit);
        }
    }

    public Task<IReadOnlyList<Habit>> GetByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<Habit> result = _habits.Values
                .Where(h => h.UserId == userId)
                .Select(Clone)
                .ToList();

            return Task.FromResult(result);
        }
    }

    public Task<Habit?> GetByIdAsync(Guid habitId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var habit = _habits.TryGetValue(habitId, out var stored) ? Clone(stored) : null;
            return Task.FromResult<Habit?>(habit);
        }
    }

    public Task AddAsync(Habit habit, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_habits.TryAdd(habit.Id, Clone(habit)))
            {
                throw new InvalidOperationException($"Habit {habit.Id} already exists.");
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Habit habit, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_habits.ContainsKey(habit.Id))
            {
                throw new InvalidOperationException($"Habit {habit.Id} no longer exists.");
            }

            _habits[habit.Id] = Clone(habit);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid habitId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _habits.Remove(habitId);
        }

        return Task.CompletedTask;

    }

    private static Habit Clone(Habit habit) => Habit.Restore(
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
