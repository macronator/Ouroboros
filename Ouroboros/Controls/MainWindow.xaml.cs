using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Defintions;
using Ouroboros.Services.Factories;
using Ouroboros.Services.Managers;

namespace Ouroboros.Controls;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public sealed partial class MainWindow
{
    private readonly ClientManager ClientManager;
    private readonly DaWindowFactory DaWindowFactory;

    public MainWindow(DaWindowFactory daWindowFactory, ClientManager clientManager)
    {
        DaWindowFactory = daWindowFactory;
        ClientManager = clientManager;

        InitializeComponent();

        //poll the live client state and render a simple status panel
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => UpdateStatus();
        timer.Start();
    }

    private void UpdateStatus()
    {
        var clients = ClientManager.ActiveClients;

        if (clients.Count == 0)
        {
            StatusText.Text = "No client connected. Launch a client and log in.";

            return;
        }

        var builder = new StringBuilder();

        foreach (var client in clients)
        {
            var vitals = client.Vitals;
            var map = client.Aisling?.Map;
            var position = client.ServerPoint;

            builder.AppendLine(client.Aisling?.Name ?? "(character unknown)");
            builder.AppendLine($"  HP {vitals.CurrentHp}/{vitals.MaximumHp} ({vitals.HealthPercent}%)   "
                               + $"MP {vitals.CurrentMp}/{vitals.MaximumMp} ({vitals.ManaPercent}%)");
            builder.AppendLine($"  Map: {map?.Name ?? "-"} [{(map is null ? "-" : map.Id)}]  @ ({position.X}, {position.Y})");
            builder.AppendLine($"  Nearby monsters: {client.EntityManager.GetNearbyMonsters(null).Count}");
            builder.AppendLine($"  Skills: {client.SkillBook.Snapshot().Count}   Spells: {client.SpellBook.Snapshot().Count}");
            builder.AppendLine($"  Walking: {(client.Bot.Walker.Route is not null)}   "
                               + $"Combat: {client.Bot.Combat.Enabled}   Support: {client.Bot.Support.Enabled}");
            builder.AppendLine();
        }

        StatusText.Text = builder.ToString();
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