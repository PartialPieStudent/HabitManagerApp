using Habits.Shared;

namespace Habits.Mobile.Services;
public interface IHabitService
{
    Task<IReadOnlyList<HabitDto>> GetHabitsAsync(bool includeArchived = false);
    Task<HabitDto?> GetHabitAsync(Guid habitId);

    Task<IReadOnlyList<HabitCompletionDto>> GetCompletionsAsync(Guid habitId);

    Task<HabitDto> CreateAsync(CreateHabitDto dto);

    Task<HabitDto> UpdateAsync(UpdateHabitDto dto);

    Task SetArchivedAsync(Guid habitId, bool archived);

    Task MarkCompletedAsync(MarkHabitCompletedDto dto);

    Task UnmarkCompletedAsync(MarkHabitCompletedDto dto);
}
