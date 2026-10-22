using Habits.Shared;

namespace Habits.Mobile.ViewModels;

public sealed record HabitListItem(
    Guid Id,
    string Title,
    string ScheduleText,
    bool IsActive,
    bool IsCompletedToday)
{
    public bool IsArchived => !IsActive;

    public string ToggleText => IsCompletedToday ? "Izpildīts" : "Atzīmēt";

    public static HabitListItem From(HabitDto dto) => new(
        dto.Id,
        dto.Title,
        ScheduleFormatter.Format(dto.Frequency, dto.DaysOfWeek),
        dto.IsActive,
        dto.IsCompletedToday);
}
