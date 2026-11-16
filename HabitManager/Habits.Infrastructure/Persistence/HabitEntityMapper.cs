using Habits.Domain.Entities;
using Habits.Domain.ValueObjects;
using Habits.Infrastructure.Persistence.Entities;

namespace Habits.Infrastructure.Persistence;

//Pārvērš rindas no DB uz domena objektiem Converts between database rows (HabitEntity) and domain objects (Habit).
// Lādējas caur Habit.Restore tātad nosaukums/lietotājs tiek revalidēts
// Domens nezina par EF Core.
internal static class HabitEntityMapper
{
    public static Habit ToDomain(HabitEntity entity) => Habit.Restore(
        entity.Id,
        entity.UserId,
        entity.Title,
        entity.Description,
        entity.IsActive,
        entity.CreatedAt,
        entity.UpdatedAt,
        ToRecurrenceRule(entity.Frequency, entity.DaysOfWeek),
        entity.Completions.Select(c => (c.Id, c.Date)));

    public static HabitEntity ToEntity(Habit habit)
    {
        var entity = new HabitEntity
        {
            Id = habit.Id,
            CreatedAt = habit.CreatedAt
        };

        CopyDetails(habit, entity);

        entity.Completions = habit.Completions
            .Select(c => new HabitCompletionEntity { Id = c.Id, HabitId = habit.Id, Date = c.Date })
            .ToList();

        return entity;
    }

    // Pārkopē visu izņemot Id, CreatedAt, completions.
    public static void CopyDetails(Habit habit, HabitEntity entity)
    {
        entity.UserId = habit.UserId;
        entity.Title = habit.Title;
        entity.Description = habit.Description;
        entity.IsActive = habit.IsActive;
        entity.Frequency = habit.RecurrenceRule.Frequency;
        entity.DaysOfWeek = string.Join(",", habit.RecurrenceRule.DaysOfWeek); // "Monday,Friday"
        entity.UpdatedAt = habit.UpdatedAt;
    }

    private static RecurrenceRule ToRecurrenceRule(RecurrenceFrequency frequency, string daysOfWeek)
    {
        if (frequency != RecurrenceFrequency.Weekly)
        {
            return RecurrenceRule.Daily();
        }

        var days = daysOfWeek
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => Enum.Parse<DayOfWeek>(d));

        return RecurrenceRule.Weekly(days);
    }
}
