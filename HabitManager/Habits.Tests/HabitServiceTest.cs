using Habits.Application.Exceptions;
using Habits.Application.Services;
using Habits.Domain.Entities;
using Habits.Tests.Fakes;
using Xunit;

namespace Habits.Tests;

/// <summary>
/// Unit tests of the application layer - the use cases behind the API
/// endpoints. The database is replaced with FakeHabitRepository, so only
/// HabitService itself is tested. xUnit creates a new instance of this
/// class for every test, so every test starts with an empty repository.
/// </summary>
///

/*
DB vietā mums fakeHabitRepository
tiek testēts tikai HabitService
xUnit izveido šīs klases instanci katram testam
tāpēc katrs tests sākas ar tukšu repositoriju.
 */
public class HabitServiceTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    // Fiksēts datums: testi sniedz vienādi rezultāti jebkurā dienā.
    private static readonly DateOnly Today = new(2026, 11, 16);

    private readonly FakeHabitRepository _repository = new();
    private readonly HabitService _service;

    public HabitServiceTests()
    {
        _service = new HabitService(_repository);
    }

    [Fact]
    public async Task GetHabitAsync_HabitOfAnotherUser_ThrowsNotFound()
    {
        // Ieradums kas pieder citam lietotājam
        var foreignHabit = Habit.Create(OtherUserId, "Private habit", null);
        _repository.Seed(foreignHabit);

        // user-1 mēģina piekļūt user-2 ieradumam bet redz tikai 404
        await Assert.ThrowsAsync<HabitNotFoundException>(
            () => _service.GetHabitAsync(UserId, foreignHabit.Id, Today));
    }

    [Fact]
    public async Task MarkCompletedAsync_ArchivedHabit_ThrowsAndSavesNothing()
    {
        //Ieradums kas ir arhivēts
        var habit = Habit.Create(UserId, "Learn guitar", null);
        habit.Archive();
        _repository.Seed(habit);

        // API mēģina atzīmēt ieradumu kā pabeigtu, bet saņem kļūdu
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.MarkCompletedAsync(UserId, habit.Id, Today));

        // Ieradums netiek saglabāts kā izpildīts
        Assert.Empty(_repository.Find(habit.Id)!.Completions);
    }

    [Fact]
    public async Task GetHabitAsync_IsCompletedToday_UsesDateSentByClient()
    {
        // Ieradums kas ir pabeigts šodien, bet tests nosaka datumu, lai rezultāts būtu vienāds jebkurā dienā
        var habit = Habit.Create(UserId, "Meditate", null);
        habit.MarkCompleted(Today);
        _repository.Seed(habit);

        // Jautājam klienta pusei kad ieradums bija pabeigts
        var onClientsToday = await _service.GetHabitAsync(UserId, habit.Id, Today);
        var dayBefore = await _service.GetHabitAsync(UserId, habit.Id, Today.AddDays(-1));

        // Rezultāts ir atkarīgs no datuma, ko nosūtīja klients
        Assert.True(onClientsToday.IsCompletedToday);
        Assert.False(dayBefore.IsCompletedToday);
    }
}
