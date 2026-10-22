namespace Habits.Domain.Entities;


//Rāda vienu rekord kas bija pabeigts attiecīgajā datumā
public sealed class HabitCompletion
{
    public Guid Id { get; }
    public Guid HabitId { get; }
    public DateOnly Date { get; }

    private HabitCompletion(Guid id, Guid habitId, DateOnly date)
    {
        Id = id;
        HabitId = habitId;
        Date = date;
    }


    internal static HabitCompletion Create(Guid habitId, DateOnly date)
    {
        if (habitId == Guid.Empty)
        {
            throw new ArgumentException("A habit completion must reference a valid habit.", nameof(habitId));
        }

        return new HabitCompletion(Guid.NewGuid(), habitId, date);
    }

    //Atjauno jau esošu ierakstu (piem., no lokālās krātuves vai servera), saglabājot tā sākotnējo Id
    internal static HabitCompletion Restore(Guid id, Guid habitId, DateOnly date)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A stored completion must have a valid Id.", nameof(id));
        }

        if (habitId == Guid.Empty)
        {
            throw new ArgumentException("A habit completion must reference a valid habit.", nameof(habitId));
        }

        return new HabitCompletion(id, habitId, date);
    }
}

