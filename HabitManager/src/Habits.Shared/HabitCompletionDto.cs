namespace Habits.Shared;
public sealed record HabitCompletionDto(
    Guid Id,
    Guid HabitId,
    DateOnly Date);
