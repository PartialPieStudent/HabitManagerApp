namespace Habits.Shared;
public sealed record HabitDto(
    Guid Id,
    string UserId,
    string Title,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    RecurrenceFrequencyDto Frequency,
    IReadOnlyCollection<DayOfWeek> DaysOfWeek,
    bool IsCompletedToday);
