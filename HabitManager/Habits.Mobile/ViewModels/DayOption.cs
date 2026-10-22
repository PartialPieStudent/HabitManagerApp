using CommunityToolkit.Mvvm.ComponentModel;

namespace Habits.Mobile.ViewModels;

public partial class DayOption : ObservableObject
{
    public DayOption(DayOfWeek day)
    {
        Day = day;
        Name = ScheduleFormatter.ShortName(day);
    }

    public DayOfWeek Day { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool isSelected;
}