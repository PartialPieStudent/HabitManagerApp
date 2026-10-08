namespace Habits.Shared;
public sealed record CreateHabitDto(
    string UserId,
    string Title,
    string? Description,
    RecurrenceFrequencyDto Frequency,
    IReadOnlyCollection<DayOfWeek>? DaysOfWeek = null);
