namespace Habits.Shared;

public sealed record MarkHabitCompletedDto(
    Guid HabitId,
    DateOnly Date);
