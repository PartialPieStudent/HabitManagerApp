using Habits.Mobile.ViewModels;

namespace Habits.Mobile.Views;

public partial class HabitDetailPage : ContentPage
{
    private readonly HabitDetailViewModel _viewModel;

    public HabitDetailPage(HabitDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Refrešo lapu pēc iziešanas no edit lapas
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
