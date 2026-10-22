using Habits.Mobile.Views;

namespace Habits.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(Routes.HabitDetail, typeof(HabitDetailPage));
        Routing.RegisterRoute(Routes.HabitEdit, typeof(HabitEditPage));
    }
}
