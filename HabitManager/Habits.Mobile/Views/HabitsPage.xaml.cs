using Habits.Mobile.ViewModels;

namespace Habits.Mobile.Views;

public partial class HabitsPage : ContentPage
{
    private readonly HabitsViewModel _viewModel;

    public HabitsPage(HabitsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
    // Tikai vizuāls efekts (nospiešanas "atsperīte"); navigāciju veic OpenCommand view modelī.
    private async void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View card) return;

        await card.ScaleTo(0.97, 60, Easing.CubicOut);
        await card.ScaleTo(1.0, 80, Easing.CubicIn);
    }
}