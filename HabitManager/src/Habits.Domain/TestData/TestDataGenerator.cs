using Habits.Domain.Entities;
using Habits.Domain.ValueObjects;

namespace Habits.Domain.TestData;

public static class TestDataGenerator
{
    public const string UserOne = "demo-user-1";
    public const string UserTwo = "demo-user-2";

    public static IReadOnlyList<Habit> GenerateHabits()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        //demo-user-1
        var drinkWater = Habit.Create(
            UserOne,
            "Drink 2L of water",
            "Stay hydrated throughout the day.",
            RecurrenceRule.Daily());

        var morningRun = Habit.Create(
            UserOne,
            "Morning run",
            "30 minutes jog before breakfast.",
            RecurrenceRule.Weekly(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }));

        var readBook = Habit.Create(
            UserOne,
            "Read a book",
            null,
            RecurrenceRule.Daily());

        var learnGuitar = Habit.Create(
            UserOne,
            "Learn guitar",
            "Practice chords for 15 minutes.",
            RecurrenceRule.Daily());
        learnGuitar.Archive(); //Viens prasiits archived

        //demo-user-2
        var meditate = Habit.Create(
            UserTwo,
            "Meditate",
            "10 minutes of mindfulness meditation.",
            RecurrenceRule.Daily());

        var noSugar = Habit.Create(
            UserTwo,
            "No added sugar",
            "Avoid added sugar in meals and drinks on weekdays.",
            RecurrenceRule.Weekly(new[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
            }));

        //Dažadu darbdienu izpildišana
        drinkWater.MarkCompleted(today.AddDays(-3));
        drinkWater.MarkCompleted(today.AddDays(-2));
        drinkWater.MarkCompleted(today.AddDays(-1));
        drinkWater.MarkCompleted(today);

        morningRun.MarkCompleted(today.AddDays(-2));

        readBook.MarkCompleted(today);

        meditate.MarkCompleted(today.AddDays(-1));
        meditate.MarkCompleted(today);

        noSugar.MarkCompleted(today.AddDays(-4));

        return new List<Habit>
        {
            drinkWater,
            morningRun,
            readBook,
            learnGuitar,
            meditate,
            noSugar
        };
    }
}
