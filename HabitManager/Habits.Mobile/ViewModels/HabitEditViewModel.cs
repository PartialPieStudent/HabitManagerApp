using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Habits.Mobile.Services;
using Habits.Shared;
using System.Collections.ObjectModel;
//using static Android.Icu.Text.CaseMap;        //un šo arī pagaidām nedzēst
//using static Android.Util.EventLogTags;

namespace Habits.Mobile.ViewModels;

public partial class HabitEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly IHabitService _habitService;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    private Guid? _habitId;      
    private bool _isActive = true;
    private bool _loaded;

    public HabitEditViewModel(
        IHabitService habitService,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _habitService = habitService;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _navigation = navigation;
    }

    public ObservableCollection<DayOption> Days { get; } = new(
        new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        }.Select(d => new DayOption(d)));

    [ObservableProperty]
    private string pageTitle = "Jauns ieradums";

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private bool isWeekly;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Routes.HabitIdParameter, out var value) && value is Guid id)
        {
            _habitId = id;
            PageTitle = "Labot ieradumu";
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_loaded || _habitId is null) return;
        _loaded = true;

        try
        {
            var habit = await _habitService.GetHabitAsync(_habitId.Value);
            if (habit is null)
            {
                await _dialogs.ShowErrorAsync("Ieradums nav atrasts.");
                await _navigation.GoBackAsync();
                return;
            }

            Title = habit.Title;
            Description = habit.Description;
            IsWeekly = habit.Frequency == RecurrenceFrequencyDto.Weekly;
            _isActive = habit.IsActive;

            foreach (var option in Days)
            {
                option.IsSelected = habit.DaysOfWeek.Contains(option.Day);
            }
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var frequency = IsWeekly ? RecurrenceFrequencyDto.Weekly : RecurrenceFrequencyDto.Daily;
            var days = IsWeekly
                ? Days.Where(d => d.IsSelected).Select(d => d.Day).ToList()
                : new List<DayOfWeek>();

            if (_habitId is null)
            {
                await _habitService.CreateAsync(
                    new CreateHabitDto(_currentUser.UserId, Title, Description, frequency, days));
            }
            else
            {
                await _habitService.UpdateAsync(
                    new UpdateHabitDto(_habitId.Value, Title, Description, _isActive, frequency, days));
            }

            await _navigation.GoBackAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
    }

    [RelayCommand]
    private Task CancelAsync() => _navigation.GoBackAsync();
}