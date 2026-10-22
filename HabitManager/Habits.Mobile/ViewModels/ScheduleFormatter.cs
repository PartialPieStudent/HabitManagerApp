using Habits.Shared;

namespace Habits.Mobile.ViewModels;

public static class ScheduleFormatter
{
    public static string Format(RecurrenceFrequencyDto frequency, IEnumerable<DayOfWeek> days) => frequency switch
    {
        RecurrenceFrequencyDto.Daily => "Katru dienu",
        RecurrenceFrequencyDto.Weekly => "Nedēļā: " + string.Join(", ", days.OrderBy(SortKey).Select(ShortName)),
        _ => "—"
    };

    public static string ShortName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pr",
        DayOfWeek.Tuesday => "Ot",
        DayOfWeek.Wednesday => "Tr",
        DayOfWeek.Thursday => "Ce",
        DayOfWeek.Friday => "Pk",
        DayOfWeek.Saturday => "Se",
        DayOfWeek.Sunday => "Sv",
        _ => day.ToString()
    };

    // Manam Svētdienu uz 7, jo amerikāņiem (Microsoft/.Net) tā ir 0 (pirmā) nedēļas diena
    private static int SortKey(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;
}
