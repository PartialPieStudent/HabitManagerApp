namespace Habits.Infrastructure.Persistence.Entities;

//Viena rindiņa no "HabitCompletions" tabulas: a habit completed on a date.
public sealed class HabitCompletionEntity
{
    public Guid Id { get; set; }

    //FK to Habits.Id.
    public Guid HabitId { get; set; }

    public DateOnly Date { get; set; }

    public HabitEntity? Habit { get; set; }
}
