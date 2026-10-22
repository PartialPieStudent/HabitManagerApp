using Habits.Domain.Entities;
using Habits.Domain.ValueObjects;
using Habits.Shared;

namespace Habits.Application.Mapping;

public static class HabitDtoMappings
{
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

    public static RecurrenceRule ToRecurrenceRule(RecurrenceFrequencyDto frequency, IEnumerable<DayOfWeek>? days) =>
        frequency switch
        {
            RecurrenceFrequencyDto.Daily => RecurrenceRule.Daily(),
            RecurrenceFrequencyDto.Weekly => RecurrenceRule.Weekly(days ?? Array.Empty<DayOfWeek>()),
            _ => throw new ArgumentException($"Unknown frequency '{frequency}'.", nameof(frequency))
        };
}