using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Habits.Mobile.Services;
using Habits.Shared;

namespace Habits.Mobile.ViewModels;

public partial class HabitsViewModel : ObservableObject
{
    private readonly IHabitService _habitService;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    public HabitsViewModel(IHabitService habitService, IDialogService dialogs, INavigationService navigation)
    {
        _habitService = habitService;
        _dialogs = dialogs;
        _navigation = navigation;
    }

    public ObservableCollection<HabitListItem> Habits { get; } = new();

    [ObservableProperty]
    private bool showArchived;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string todaySummary = string.Empty;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    partial void OnShowArchivedChanged(bool value) => LoadCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var habits = await _habitService.GetHabitsAsync(ShowArchived);

            Habits.Clear();
            foreach (var habit in habits)
            {
                Habits.Add(HabitListItem.From(habit));
            }

            var active = habits.Where(h => h.IsActive).ToList();
            TodaySummary = $"Šodien izpildīti: {active.Count(h => h.IsCompletedToday)} no {active.Count}";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ToggleTodayAsync(HabitListItem item)
    {
        try
        {
            var dto = new MarkHabitCompletedDto(item.Id, Today);

            if (item.IsCompletedToday)
                await _habitService.UnmarkCompletedAsync(dto);
            else
                await _habitService.MarkCompletedAsync(dto);

            await LoadAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
    }

    [RelayCommand]
    private Task AddAsync() => _navigation.GoToAsync(Routes.HabitEdit);

    [RelayCommand]
    private Task OpenAsync(HabitListItem item) =>
        _navigation.GoToAsync(Routes.HabitDetail, new Dictionary<string, object>
        {
            [Routes.HabitIdParameter] = item.Id
        });
}
