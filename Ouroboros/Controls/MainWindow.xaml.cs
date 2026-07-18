using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Automation;
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
    private HotkeyService? Hotkeys;

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

    //register global hotkeys once the window handle exists: Ctrl+Shift+F1..F6 toggle features on the selected client
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (HwndSource.FromHwnd(new WindowInteropHelper(this).Handle) is not { } source)
            return;

        Hotkeys = new HotkeyService(source);
        const uint mod = HotkeyService.ModControl | HotkeyService.ModShift;

        Hotkeys.Register(0x70, () => ToggleRoutine(bot => bot.Combat.Enabled = !bot.Combat.Enabled), mod);       // F1
        Hotkeys.Register(0x71, () => ToggleRoutine(bot => bot.Support.Enabled = !bot.Support.Enabled), mod);     // F2
        Hotkeys.Register(0x72, () => ToggleRoutine(bot => bot.Consumables.Enabled = !bot.Consumables.Enabled), mod); // F3
        Hotkeys.Register(0x73, () => ToggleRoutine(bot => bot.Loot.Enabled = !bot.Loot.Enabled), mod);           // F4
        Hotkeys.Register(0x74, () => ToggleRoutine(bot => bot.Trash.Enabled = !bot.Trash.Enabled), mod);         // F5
        Hotkeys.Register(0x75, StopAll, mod);                                                                    // F6
    }

    protected override void OnClosed(EventArgs e)
    {
        Hotkeys?.Dispose();
        base.OnClosed(e);
    }

    //applies a toggle to the selected client's bot, then starts the engine so a newly-enabled routine runs
    private void ToggleRoutine(Action<BotContext> apply)
    {
        if (ViewModel.SelectedClient?.Client?.Bot is not { } bot)
            return;

        apply(bot);
        bot.Engine.Start();
    }

    //panic stop: clear the route and disable every routine on the selected client
    private void StopAll()
    {
        if (ViewModel.SelectedClient?.Client?.Bot is not { } bot)
            return;

        bot.Walker.Route = null;
        bot.Combat.Enabled = false;
        bot.Support.Enabled = false;
        bot.Consumables.Enabled = false;
        bot.Loot.Enabled = false;
        bot.Trash.Enabled = false;
        _ = bot.Engine.StopAsync();
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
