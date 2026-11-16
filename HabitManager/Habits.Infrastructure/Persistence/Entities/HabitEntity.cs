using Habits.Domain.ValueObjects;

namespace Habits.Infrastructure.Persistence.Entities;

// Viena rindiņa no "Habits" tabulas. Plakanā datu klase domāta tikai EF Core.
// business rules paliek domain Habit, kurš tiek atjaunots no s''is rindiņas caur Habit.Restore (Domenam nau ne jausmas par db).
public sealed class HabitEntity
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    //Status: true = active, false = archived.
    public bool IsActive { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    //Dienas no iknedēļas grafika (Monday,Wednesday,Friday), daily paliek tukšs
    public string DaysOfWeek { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    //Ieraduma izpildes ieraksti (viens pret daudzi relācija)
    public List<HabitCompletionEntity> Completions { get; set; } = new();
}
