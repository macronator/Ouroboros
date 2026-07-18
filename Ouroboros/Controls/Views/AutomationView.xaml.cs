namespace Ouroboros.Controls.Views;

/// <summary>Interaction logic for AutomationView.xaml — combat/support/consumable toggles for the selected client.</summary>
public sealed partial class AutomationView
{
    public AutomationView() => InitializeComponent();

    private void SaveButton_Click(object sender, System.Windows.RoutedEventArgs e)
        => (DataContext as ViewModel.AutomationViewModel)?.Save();
}
