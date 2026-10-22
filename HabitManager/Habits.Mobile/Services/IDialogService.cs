namespace Habits.Mobile.Services;

public interface IDialogService
{
    Task ShowErrorAsync(string message);
    Task<bool> ConfirmAsync(string title, string message);
}

public sealed class ShellDialogService : IDialogService
{
    public Task ShowErrorAsync(string message) =>
        Shell.Current.DisplayAlert("Kļūda", message, "OK");

    public Task<bool> ConfirmAsync(string title, string message) =>
        Shell.Current.DisplayAlert(title, message, "Jā", "Nē");
}
