using Habits.Domain.Entities;
using Habits.Domain.TestData;

var today = DateOnly.FromDateTime(DateTime.UtcNow);
var habits = TestDataGenerator.GenerateHabits();

PrintHeader("Habit Manager — Test Data Verification");


Console.WriteLine($"Total habits: {habits.Count}");
Console.WriteLine();

//user IDs saraksts ar ieradumiem
var userIds = habits.Select(h => h.UserId).Distinct().OrderBy(id => id).ToList();
Console.WriteLine("User IDs with habits:");
foreach (var userId in userIds)
{
    Console.WriteLine($"  - {userId}");
}
Console.WriteLine();

//Ieradumu sadalijums pēc user, active vs archived.
PrintHeader("Habits by user");
foreach (var userId in userIds)
{
    Console.WriteLine($"{userId}:");
    foreach (var habit in habits.Where(h => h.UserId == userId))
    {
        var status = habit.IsActive ? "ACTIVE  " : "ARCHIVED";
        var completedToday = habit.IsCompletedOn(today) ? "completed today" : "not completed today";
        Console.WriteLine($"  [{status}] {habit.Title,-22} ({habit.RecurrenceRule}) — {completedToday}");
    }
    Console.WriteLine();
}

// Ieradumi izpildīti šodien
PrintHeader("Completed today");
var completedTodayHabits = habits.Where(h => h.IsCompletedOn(today)).ToList();
if (completedTodayHabits.Count == 0)
{
    Console.WriteLine("(none)");
}
else
{
    foreach (var habit in completedTodayHabits)
    {
        Console.WriteLine($"  - {habit.Title} (user: {habit.UserId})");
    }
}
Console.WriteLine();

//Domenu metodes palaišana
PrintHeader("Domain method demonstration");

var activeHabit = habits.First(h => h.IsActive && !h.IsCompletedOn(today));
Console.WriteLine($"IsCompletedOn(today) for '{activeHabit.Title}' before marking: {activeHabit.IsCompletedOn(today)}");

var newCompletion = activeHabit.MarkCompleted(today);
Console.WriteLine($"Called MarkCompleted(today) -> created completion {newCompletion.Id}");
Console.WriteLine($"IsCompletedOn(today) for '{activeHabit.Title}' after marking:  {activeHabit.IsCompletedOn(today)}");
Console.WriteLine();

Console.WriteLine("Business rule check — marking the same habit for the same date again (duplicate):");
TryAndReport(() => activeHabit.MarkCompleted(today));
Console.WriteLine();

Console.WriteLine("Business rule check — marking an archived habit as completed:");
var archivedHabit = habits.First(h => !h.IsActive);
TryAndReport(() => archivedHabit.MarkCompleted(today));

return;

static void PrintHeader(string title)
{
    Console.WriteLine($"=== {title} ===");
}

static void TryAndReport(Func<HabitCompletion> action)
{
    try
    {
        action();
        Console.WriteLine("  Unexpectedly succeeded.");
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"  Rejected as expected: {ex.Message}");
    }
}
