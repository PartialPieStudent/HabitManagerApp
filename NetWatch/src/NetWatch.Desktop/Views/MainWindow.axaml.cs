using Avalonia.Controls;

namespace NetWatch.Desktop.Views;

/// <summary>
/// Код за разметкой главного окна. В MVVM здесь почти ничего нет — вся логика во ViewModel.
/// InitializeComponent() генерируется Avalonia автоматически из MainWindow.axaml.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
