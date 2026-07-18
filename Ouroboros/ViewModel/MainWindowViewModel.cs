using System.Collections.ObjectModel;
using Ouroboros.Client;
using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>
///     Backing view-model for the main window shell. Holds the live set of connected clients (each a
///     <see cref="ClientStatusViewModel" />) plus the currently-selected one the feature tabs act on, and
///     the per-tab view-models. Refreshed on the UI-thread timer from the client manager, so the tabs bind
///     to state instead of the old code-behind string dump — and the designer renders a sample client.
/// </summary>
public sealed class MainWindowViewModel : NotifyPropertyChangedBase
{
    private ClientStatusViewModel? _selectedClient;

    /// <summary>Design-time constructor: seeds one sample client so the designer renders populated.</summary>
    public MainWindowViewModel()
    {
        Clients.Add(ClientStatusViewModel.DesignSample());
        SelectedClient = Clients[0];
    }

    public ObservableCollection<ClientStatusViewModel> Clients { get; } = [];

    public ClientStatusViewModel? SelectedClient
    {
        get => _selectedClient;
        set
        {
            if (!SetField(ref _selectedClient, value))
                return;

            Automation.Bind(value?.Client);
            PacketConsole.Bind(value?.Client);
            Waypoints.Bind(value?.Client);
            Thumbnail.Bind(value?.Client);
        }
    }

    public bool HasClients => Clients.Count > 0;

    public string StatusSummary
        => Clients.Count == 0
            ? "No client connected. Launch a client and log in."
            : $"{Clients.Count} client{(Clients.Count == 1 ? "" : "s")} connected";

    public PacketConsoleViewModel PacketConsole { get; } = new();
    public WaypointsViewModel Waypoints { get; } = new();
    public AutomationViewModel Automation { get; } = new();
    public ThumbnailViewModel Thumbnail { get; } = new();

    /// <summary>
    ///     Syncs the client rows with the live snapshot — updating existing rows in place and adding/removing
    ///     as clients connect and disconnect — then refreshes the derived state. Call on the UI thread.
    /// </summary>
    public void Refresh(IReadOnlyList<DarkAgesClient> clients)
    {
        //drop rows whose client is gone (also clears the design sample, whose Client is null)
        for (var i = Clients.Count - 1; i >= 0; i--)
            if (Clients[i].Client is not { } existing || !clients.Contains(existing))
                Clients.RemoveAt(i);

        foreach (var client in clients)
        {
            var row = Clients.FirstOrDefault(c => ReferenceEquals(c.Client, client));

            if (row is null)
            {
                row = new ClientStatusViewModel(client);
                Clients.Add(row);
            }

            row.Update();
        }

        //keep a valid selection: default to the first, and recover if the selected client disconnected
        if (SelectedClient is null || !Clients.Contains(SelectedClient))
            SelectedClient = Clients.FirstOrDefault();

        Automation.Bind(SelectedClient?.Client);
        PacketConsole.Bind(SelectedClient?.Client);
        PacketConsole.Refresh();
        Waypoints.Bind(SelectedClient?.Client);
        Waypoints.Refresh();
        Thumbnail.Bind(SelectedClient?.Client);

        OnPropertyChanged(nameof(HasClients));
        OnPropertyChanged(nameof(StatusSummary));
    }
}
