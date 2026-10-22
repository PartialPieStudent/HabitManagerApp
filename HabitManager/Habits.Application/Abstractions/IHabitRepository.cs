using Habits.Domain.Entities;

namespace Habits.Application.Abstractions;

//CancellationToken cancellationToken = default <- Zelta likums kā sapratu
//Lietotājs pazuda darbības vidū -> servers pārtrauc darbības ar to saistītas
public interface IHabitRepository
{
    //Iegūstam lietotāja ieradumus pēc tā ID
    Task<IReadOnlyList<Habit>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
    //Iegūstam konkrētu ieradumu pēc tā Guid
    Task<Habit?> GetByIdAsync(Guid habitId, CancellationToken cancellationToken = default);
    //Jauns objekts Habit
    Task AddAsync(Habit habit, CancellationToken cancellationToken = default);
    //apdejto objektu
    Task UpdateAsync(Habit habit, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid habitId, CancellationToken cancellationToken = default);
}