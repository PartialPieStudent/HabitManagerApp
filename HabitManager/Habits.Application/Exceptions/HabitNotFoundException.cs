namespace Habits.Application.Exceptions;

//standarts 404, ja ieradums neeksistē/nepieder lietotājam
public sealed class HabitNotFoundException : Exception
{
    public HabitNotFoundException(Guid habitId)
        : base($"Habit {habitId} was not found.")
    {
        HabitId = habitId;
    }

    public Guid HabitId { get; }
}