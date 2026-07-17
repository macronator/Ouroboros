using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Defintions;
using Ouroboros.Services.Factories;
using Ouroboros.Services.Managers;
using Ouroboros.ViewModel;

namespace Ouroboros.Controls;

/// <summary>
///     Interaction logic for MainWindow.xaml. The window is a thin shell: a <see cref="MainWindowViewModel" />
///     holds the bound state and the feature tabs render it. A UI-thread timer refreshes the view-model from
///     the live client snapshot; all display logic lives in the view-models and XAML, not here.
/// </summary>
public sealed partial class MainWindow
{
    private readonly ClientManager ClientManager;
    private readonly DaWindowFactory DaWindowFactory;
    private readonly MainWindowViewModel ViewModel;

    public MainWindow(DaWindowFactory daWindowFactory, ClientManager clientManager)
    {
        DaWindowFactory = daWindowFactory;
        ClientManager = clientManager;
        ViewModel = new MainWindowViewModel();

        InitializeComponent();

        DataContext = ViewModel;

        //clear the design sample and show the real (initially empty) state before the first tick
        ViewModel.Refresh(ClientManager.ActiveClients);

        //poll the live client state and let the bindings update the tabs
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => ViewModel.Refresh(ClientManager.ActiveClients);
        timer.Start();
    }

    private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
    {
        var window = await DaWindowFactory.CreateAsync(MemoryEditFlags.AllExceptWalls)
                                          .ConfigureAwait(false);

        ClientManager.AddWindow(window);
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var options = App.Instance.Provider.GetRequiredService<OptionsWindow>();
        options.Owner = this;
        options.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        options.Show();
    }

    #region TopBar UI
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void HamburgerButton_Click(object sender, RoutedEventArgs e) => DrawerHost.IsLeftDrawerOpen = !DrawerHost.IsLeftDrawerOpen;
    #endregion TopBar UI
}
