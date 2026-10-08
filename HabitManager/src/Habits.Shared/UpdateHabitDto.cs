namespace Habits.Shared;

public sealed record UpdateHabitDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsActive,
    RecurrenceFrequencyDto Frequency,
    IReadOnlyCollection<DayOfWeek>? DaysOfWeek = null);
