using Habits.Application.Abstractions;
using Habits.Application.Exceptions;
using Habits.Application.Mapping;
using Habits.Domain.Entities;
using Habits.Shared;

namespace Habits.Application.Services;

/*
    Lietotnes slānis:
    Šeit tiek definēti lietotnes gadījumi (use cases) ieradumu pārvaldībai.
    Kontrolleri tikai izsauc šīs metodes; biznesa noteikumi paliek domēnā (Habit),
    un datu glabāšana tiek veikta tikai caur IHabitRepository.
*/

public sealed class HabitService
{
    private readonly IHabitRepository _repository;

    public HabitService(IHabitRepository repository)
    {
        _repository = repository;
    }

    // Šodien, no servera puses
    //karoče nevajag šo
    //private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // ---------------- Queries ----------------

    public async Task<IReadOnlyList<HabitDto>> GetHabitsAsync(
        string userId, bool includeArchived, DateOnly today, CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);

        var habits = await _repository.GetByUserAsync(userId, cancellationToken);

        return habits
            .Where(h => includeArchived || h.IsActive)
            .OrderByDescending(h => h.IsActive)
            .ThenBy(h => h.Title)
            .Select(h => h.ToDto(today))
            .ToList();
    }

    public async Task<HabitDto> GetHabitAsync(
        string userId, Guid habitId, DateOnly today, CancellationToken cancellationToken = default)
    {
        var habit = await GetOwnedAsync(userId, habitId, cancellationToken);
        return habit.ToDto(today);
    }

    public async Task<IReadOnlyList<HabitCompletionDto>> GetCompletionsAsync(
        string userId, Guid habitId, CancellationToken cancellationToken = default)
    {
        var habit = await GetOwnedAsync(userId, habitId, cancellationToken);

        return habit.Completions
            .OrderByDescending(c => c.Date)
            .Select(c => c.ToDto())
            .ToList();
    }

    // ---------------- Commands ----------------

    public async Task<HabitDto> CreateAsync(
        string userId, CreateHabitDto dto, DateOnly today, CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);

        var habit = Habit.Create(
            userId,
            dto.Title,
            dto.Description,
            HabitDtoMappings.ToRecurrenceRule(dto.Frequency, dto.DaysOfWeek));

        await _repository.AddAsync(habit, cancellationToken);
        return habit.ToDto(today);
    }

    public async Task<HabitDto> UpdateAsync(
        string userId, Guid habitId, UpdateHabitDto dto, DateOnly today, CancellationToken cancellationToken = default)
    {
        var habit = await GetOwnedAsync(userId, habitId, cancellationToken);

        habit.UpdateDetails(
            dto.Title,
            dto.Description,
            HabitDtoMappings.ToRecurrenceRule(dto.Frequency, dto.DaysOfWeek));

        if (dto.IsActive) habit.Activate();
        else habit.Archive();

        await _repository.UpdateAsync(habit, cancellationToken);
        return habit.ToDto(today);
    }

    public async Task SetArchivedAsync(
        string userId, Guid habitId, bool archived, CancellationToken cancellationToken = default)
    {
        var habit = await GetOwnedAsync(userId, habitId, cancellationToken);

        if (archived) habit.Archive();
        else habit.Activate();

        await _repository.UpdateAsync(habit, cancellationToken);
    }

    public async Task DeleteAsync(
        string userId, Guid habitId, CancellationToken cancellationToken = default)
    {
        // Pārbauda: Piederumu un eksistenci.
        await GetOwnedAsync(userId, habitId, cancellationToken);
        await _repository.DeleteAsync(habitId, cancellationToken);
    }

    public async Task<HabitCompletionDto> MarkCompletedAsync(
        string userId, Guid habitId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var habit = await GetOwnedAsync(userId, habitId, cancellationToken);

        // Domain rules
        var completion = habit.MarkCompleted(date);

        await _repository.UpdateAsync(habit, cancellationToken);
        return completion.ToDto();
    }

    public async Task UnmarkCompletedAsync(
        string userId, Guid habitId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var habit = await GetOwnedAsync(userId, habitId, cancellationToken);

        if (habit.UnmarkCompleted(date))
        {
            await _repository.UpdateAsync(habit, cancellationToken);
        }
    }

    // ---------------- Helpers ----------------

    private async Task<Habit> GetOwnedAsync(string userId, Guid habitId, CancellationToken cancellationToken)
    {
        EnsureUser(userId);

        var habit = await _repository.GetByIdAsync(habitId, cancellationToken);

        // Nerādam svešos ieradumus vispār, jo cienam privātumu, vnk 404 izmetam.
        if (habit is null || habit.UserId != userId)
        {
            throw new HabitNotFoundException(habitId);
        }

        return habit;
    }

    private static void EnsureUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }
    }
}