# Habit Manager — Assignment 1

Domain model, shared DTOs, and a test-data/console verification harness for
a habit-tracking app, targeting .NET 9 (compiles equally well on .NET 9 —
just bump `<TargetFramework>` in the three `.csproj` files to `net9.0`).

## Solution layout

```
HabitManager.sln
src/
  Habits.Domain/          Class library — entities, value objects, rules, test data
    Entities/
      Habit.cs             Aggregate root: title/user validation, archive/activate,
                            MarkCompleted (enforces "no archived" + "no duplicate date")
      HabitCompletion.cs    Completion record; only creatable via Habit.MarkCompleted
    ValueObjects/
      RecurrenceRule.cs     Daily / Weekly(days) schedule value object
    TestData/
      TestDataGenerator.cs  Builds the in-memory demo data set
  Habits.Shared/           Class library — DTOs for a future API/UI/mobile client
                            (deliberately has no reference to Habits.Domain)
    HabitDto.cs
    CreateHabitDto.cs
    UpdateHabitDto.cs
    HabitCompletionDto.cs
    MarkHabitCompletedDto.cs
    RecurrenceFrequencyDto.cs
  Habits.ConsoleApp/       Console app — references Habits.Domain only
    Program.cs
```

## Business rules enforced in the domain

All of these live inside `Habit` (and `HabitCompletion`), not in the console
app or anywhere else, so they can't be bypassed by a caller:

- `Habit.Create` / `UpdateDetails` reject an empty/whitespace title and one
  longer than `Habit.MaxTitleLength` (200 chars).
- `Habit.Create` rejects a null/empty `UserId`.
- `Habit.MarkCompleted` throws `InvalidOperationException` if the habit is
  archived (`IsActive == false`).
- `Habit.MarkCompleted` throws `InvalidOperationException` if a completion
  already exists for that exact `DateOnly` — no duplicate per-day records.
- `HabitCompletion.Create` is `internal`, so completions can only ever be
  produced through `Habit.MarkCompleted`, keeping the two rules above
  impossible to circumvent from outside the aggregate.

## Test data

`TestDataGenerator.GenerateHabits()` returns 6 habits:

| User | Habit | Status | Notes |
|---|---|---|---|
| demo-user-1 | Drink 2L of water | Active | daily; completed today + 3 prior days |
| demo-user-1 | Morning run | Active | weekly (Mon/Wed/Fri); completed 2 days ago |
| demo-user-1 | Read a book | Active | daily, no description; completed today |
| demo-user-1 | Learn guitar | **Archived** | daily |
| demo-user-2 | Meditate | Active | daily; completed today + yesterday |
| demo-user-2 | No added sugar | Active | weekly (Mon–Fri); completed 4 days ago |

This covers: 2 users, multiple active habits per user, one archived habit,
varied titles/descriptions, completions spread across several dates, and
multiple habits already completed today.

## Console app output

`Habits.ConsoleApp` prints, in order:

1. Total habit count.
2. The distinct user IDs that own habits.
3. A per-user breakdown of habits with Active/Archived status and today's
   completion state.
4. The list of habits completed today.
5. A live demo of domain methods: `IsCompletedOn`, `MarkCompleted`, then two
   deliberate rule violations (marking the same habit twice for today, and
   marking an archived habit) — both caught and printed as expected
   rejections rather than crashing.


Šīs fails bija izveidots ar Maksligo intelektu dažādu iemeslu dēļ:
1)Lai jebkurš varētu īsi saprast to herarhiju/loģiku un metožu darbības
2)Es to tik skaisti sarakstīt nemāku
3)Viņš to izvedoja kodolīgi

Ar cieņu,
Oskars Maksims Puto op24007
