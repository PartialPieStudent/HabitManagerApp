using Habits.Mobile.ViewModels;

namespace Habits.Mobile.Views;

public partial class HabitEditPage : ContentPage
{
    private readonly HabitEditViewModel _viewModel;

    public HabitEditPage(HabitEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}