using Habits.Domain.Entities;
using Habits.Domain.ValueObjects;
using Habits.Mobile.Services.Storage;
using Habits.Shared;

namespace Habits.Mobile.Services.Mapping;

public static class HabitMappings
{
    // ---------- Domain -> DTO ----------

    public static HabitDto ToDto(this Habit habit, DateOnly today) => new(
        habit.Id,
        habit.UserId,
        habit.Title,
        habit.Description,
        habit.IsActive,
        habit.CreatedAt,
        habit.UpdatedAt,
        habit.RecurrenceRule.Frequency.ToDto(),
        habit.RecurrenceRule.DaysOfWeek.ToList(),
        habit.IsCompletedOn(today));

    public static HabitCompletionDto ToDto(this HabitCompletion completion) =>
        new(completion.Id, completion.HabitId, completion.Date);

    public static RecurrenceFrequencyDto ToDto(this RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Daily => RecurrenceFrequencyDto.Daily,
        RecurrenceFrequency.Weekly => RecurrenceFrequencyDto.Weekly,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unknown frequency.")
    };

    // ---------- DTO -> Domain ----------

    public static RecurrenceRule ToRecurrenceRule(RecurrenceFrequencyDto frequency, IEnumerable<DayOfWeek>? days) =>
        frequency switch
        {
            RecurrenceFrequencyDto.Daily => RecurrenceRule.Daily(),
            RecurrenceFrequencyDto.Weekly => RecurrenceRule.Weekly(days ?? Array.Empty<DayOfWeek>()),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unknown frequency.")
        };

    // ---------- Domain <-> Storage ----------

    public static HabitStorageModel ToStorageModel(this Habit habit) => new(
        habit.Id,
        habit.UserId,
        habit.Title,
        habit.Description,
        habit.IsActive,
        habit.CreatedAt,
        habit.UpdatedAt,
        habit.RecurrenceRule.Frequency,
        habit.RecurrenceRule.DaysOfWeek.ToList(),
        habit.Completions.Select(c => new CompletionStorageModel(c.Id, c.Date)).ToList());

    public static Habit ToDomain(this HabitStorageModel model) => Habit.Restore(
        model.Id,
        model.UserId,
        model.Title,
        model.Description,
        model.IsActive,
        model.CreatedAt,
        model.UpdatedAt,
        model.Frequency == RecurrenceFrequency.Weekly
            ? RecurrenceRule.Weekly(model.DaysOfWeek)
            : RecurrenceRule.Daily(),
        model.Completions.Select(c => (c.Id, c.Date)));
}
