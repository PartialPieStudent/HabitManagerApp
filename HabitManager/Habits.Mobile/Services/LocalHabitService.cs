using System.Text.Json;
using System.Text.Json.Serialization;
using Habits.Domain.Entities;
using Habits.Domain.TestData;
using Habits.Mobile.Services.Mapping;
using Habits.Mobile.Services.Storage;
using Habits.Shared;

namespace Habits.Mobile.Services;

public sealed class LocalHabitService : IHabitService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ICurrentUserService _currentUser;
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Habit>? _habits;

    public LocalHabitService(ICurrentUserService currentUser, string filePath)
    {
        _currentUser = currentUser;
        _filePath = filePath;
    }
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    //Queries

    public Task<IReadOnlyList<HabitDto>> GetHabitsAsync(bool includeArchived = false)
    {
        return ReadAsync<IReadOnlyList<HabitDto>>(habits =>
        {
            var userHabits = habits.Where(h => h.UserId == _currentUser.UserId);

            if (!includeArchived)
            {
                userHabits = userHabits.Where(h => h.IsActive);
            }

            var sortedHabits = userHabits
                .OrderByDescending(h => h.IsActive)
                .ThenBy(h => h.Title);

            return sortedHabits.Select(h => h.ToDto(Today)).ToList();
        });
    }


    public Task<HabitDto?> GetHabitAsync(Guid habitId)
    {
        return ReadAsync(habits =>
        {
            var habit = habits.FirstOrDefault(h => h.Id == habitId && h.UserId == _currentUser.UserId);

            return habit?.ToDto(Today);   // null, ja ieradums nav atrasts
        });
    }

    public Task<IReadOnlyList<HabitCompletionDto>> GetCompletionsAsync(Guid habitId)
    {
        return ReadAsync<IReadOnlyList<HabitCompletionDto>>(habits =>
        {
            var habit = FindOwned(habits, habitId);

            return habit.Completions
                .OrderByDescending(c => c.Date)   // jaunākie ieraksti augšā
                .Select(c => c.ToDto())
                .ToList();
        });
    }

    // ---------------- Commands ----------------

    public Task<HabitDto> CreateAsync(CreateHabitDto dto) => WriteAsync(habits =>
    {
        var habit = Habit.Create(
            _currentUser.UserId,
            dto.Title,
            dto.Description,
            HabitMappings.ToRecurrenceRule(dto.Frequency, dto.DaysOfWeek));

        habits.Add(habit);
        return habit.ToDto(Today);
    });

    public Task<HabitDto> UpdateAsync(UpdateHabitDto dto) => WriteAsync(habits =>
    {
        var habit = FindOwned(habits, dto.Id);
        habit.UpdateDetails(
            dto.Title,
            dto.Description,
            HabitMappings.ToRecurrenceRule(dto.Frequency, dto.DaysOfWeek));

        if (dto.IsActive) habit.Activate();
        else habit.Archive();

        return habit.ToDto(Today);
    });

    public Task SetArchivedAsync(Guid habitId, bool archived) => WriteAsync(habits =>
    {
        var habit = FindOwned(habits, habitId);
        if (archived) habit.Archive();
        else habit.Activate();
    });

    public Task MarkCompletedAsync(MarkHabitCompletedDto dto) => WriteAsync(habits =>
    {
        FindOwned(habits, dto.HabitId).MarkCompleted(dto.Date);
    });

    public Task UnmarkCompletedAsync(MarkHabitCompletedDto dto) => WriteAsync(habits =>
    {
        FindOwned(habits, dto.HabitId).UnmarkCompleted(dto.Date);
    });

    // ---------------- Helpers ----------------

    private Habit FindOwned(List<Habit> habits, Guid habitId) =>
        habits.FirstOrDefault(h => h.Id == habitId && h.UserId == _currentUser.UserId)  //Ja šīs ir aplams
        ?? throw new KeyNotFoundException($"Habit {habitId} was not found.");           //Tad izmetīs kļūdu

    private async Task<T> ReadAsync<T>(Func<List<Habit>, T> query)
    {
        await _lock.WaitAsync();
        try
        {
            return query(await EnsureLoadedAsync());
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<T> WriteAsync<T>(Func<List<Habit>, T> change)
    {
        await _lock.WaitAsync();
        try
        {
            var habits = await EnsureLoadedAsync();
            var result = change(habits); // domain mēdz izmest šeit kļūdu
            await SaveAsync(habits);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task WriteAsync(Action<List<Habit>> change) =>
        WriteAsync(habits =>
        {
            change(habits);
            return true;
        });

    private async Task<List<Habit>> EnsureLoadedAsync()
    {
        if (_habits is not null) return _habits;

        if (File.Exists(_filePath))
        {
            await using var stream = File.OpenRead(_filePath);
            var stored = await JsonSerializer.DeserializeAsync<List<HabitStorageModel>>(stream, JsonOptions)
                         ?? new List<HabitStorageModel>();
            _habits = stored.Select(s => s.ToDomain()).ToList();
        }
        else
        {
            _habits = TestDataGenerator.GenerateHabits().ToList();
            await SaveAsync(_habits);
        }

        return _habits;
    }

    private async Task SaveAsync(List<Habit> habits)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        //Rakstam kopija, lai nebutu shaizes
        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            var models = habits.Select(h => h.ToStorageModel()).ToList();
            await JsonSerializer.SerializeAsync(stream, models, JsonOptions);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
