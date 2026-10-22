using Habits.Domain.ValueObjects;

namespace Habits.Mobile.Services.Storage;

public sealed record HabitStorageModel(
    Guid Id,
    string UserId,
    string Title,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    RecurrenceFrequency Frequency,
    List<DayOfWeek> DaysOfWeek,
    List<CompletionStorageModel> Completions);

public sealed record CompletionStorageModel(Guid Id, DateOnly Date);
