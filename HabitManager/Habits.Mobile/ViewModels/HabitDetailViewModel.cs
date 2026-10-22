using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Habits.Mobile.Services;
using Habits.Shared;
//using Java.Lang.Annotation;                   //pagaidām nedzēst
using System.Collections.ObjectModel;
//using static Android.Icu.Text.CaseMap;
//using static Android.Util.EventLogTags;

namespace Habits.Mobile.ViewModels;

public partial class HabitDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IHabitService _habitService;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private Guid _habitId;

    public HabitDetailViewModel(IHabitService habitService, IDialogService dialogs, INavigationService navigation)
    {
        _habitService = habitService;
        _dialogs = dialogs;
        _navigation = navigation;
    }

    public ObservableCollection<HabitCompletionDto> Completions { get; } = new();

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    private string? description;

    [ObservableProperty]
    private string scheduleText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(ArchiveButtonText))]
    private bool isActive;

    [ObservableProperty]
    private string createdText = string.Empty;

    [ObservableProperty]
    private string updatedText = string.Empty;

    [ObservableProperty]
    private bool hasNoCompletions;

    [ObservableProperty]
    private DateTime selectedDate = DateTime.Today;

    public DateTime MaxDate => DateTime.Today;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public string StatusText => IsActive ? "Aktīvs" : "Arhivēts";

    public string ArchiveButtonText => IsActive ? "Arhivēt" : "Atjaunot no arhīva";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Routes.HabitIdParameter, out var value) && value is Guid id)
        {
            _habitId = id;
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var habit = await _habitService.GetHabitAsync(_habitId);
            if (habit is null)
            {
                await _dialogs.ShowErrorAsync("Ieradums nav atrasts.");
                await _navigation.GoBackAsync();
                return;
            }

            Title = habit.Title;
            Description = habit.Description;
            ScheduleText = ScheduleFormatter.Format(habit.Frequency, habit.DaysOfWeek);
            IsActive = habit.IsActive;
            CreatedText = habit.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            UpdatedText = habit.UpdatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

            var completions = await _habitService.GetCompletionsAsync(_habitId);
            Completions.Clear();
            foreach (var completion in completions)
            {
                Completions.Add(completion);
            }
            HasNoCompletions = Completions.Count == 0;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
    }

    [RelayCommand]
    private Task MarkSelectedDateAsync() =>
        RunAndReloadAsync(() => _habitService.MarkCompletedAsync(
            new MarkHabitCompletedDto(_habitId, DateOnly.FromDateTime(SelectedDate))));

    [RelayCommand]
    private Task RemoveCompletionAsync(HabitCompletionDto completion) =>
        RunAndReloadAsync(() => _habitService.UnmarkCompletedAsync(
            new MarkHabitCompletedDto(_habitId, completion.Date)));

    [RelayCommand]
    private async Task ToggleArchiveAsync()
    {
        var archive = IsActive;
        var confirmed = await _dialogs.ConfirmAsync(
            archive ? "Arhivēt ieradumu?" : "Atjaunot ieradumu?",
            archive ? "Arhivētu ieradumu nevarēs atzīmēt kā izpildītu." : "Ieradums atkal būs aktīvs.");

        if (!confirmed) return;

        await RunAndReloadAsync(() => _habitService.SetArchivedAsync(_habitId, archive));
    }

    [RelayCommand]
    private Task EditAsync() =>
        _navigation.GoToAsync(Routes.HabitEdit, new Dictionary<string, object>
        {
            [Routes.HabitIdParameter] = _habitId
        });

    private async Task RunAndReloadAsync(Func<Task> action)
    {
        try
        {
            await action();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
    }
}