using System.Collections.ObjectModel;
using System.IO;
using Ouroboros.Automation.Walking;
using Ouroboros.Client;
using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>
///     View-model for the waypoints tab. Edits a <see cref="WaypointRoute" /> (persisted as .route files via
///     <see cref="RouteStore" />), drives the selected client's walker, and lists the auto-learned warps on the
///     current map. Route storage is client-independent; walking, "add here", and warp removal need a client.
/// </summary>
public sealed class WaypointsViewModel : NotifyPropertyChangedBase
{
    private static readonly string RoutesDirectory = Path.Combine("data", "routes");

    private DarkAgesClient? Client;
    private WaypointRoute Route = new("new-route");
    private string _status = "No client selected.";
    private string _walkerState = "idle";

    public WaypointsViewModel() => ReloadRouteList();

    /// <summary>Names of the saved routes on disk.</summary>
    public ObservableCollection<string> Routes { get; } = [];

    /// <summary>Waypoints of the route currently being edited.</summary>
    public ObservableCollection<Waypoint> Waypoints { get; } = [];

    /// <summary>Auto-learned warps on the current map.</summary>
    public ObservableCollection<WarpRow> Warps { get; } = [];

    public string RouteName
    {
        get => Route.Name;
        set
        {
            Route.Name = value;
            OnPropertyChanged();
        }
    }

    public bool Loop
    {
        get => Route.Options.Loop;
        set
        {
            Route.Options.Loop = value;
            OnPropertyChanged();
        }
    }

    public int Proximity
    {
        get => Route.Options.Proximity;
        set
        {
            Route.Options.Proximity = value;
            OnPropertyChanged();
        }
    }

    public string Status { get => _status; private set => SetField(ref _status, value); }
    public string WalkerState { get => _walkerState; private set => SetField(ref _walkerState, value); }
    public bool HasClient => Client is not null;

    public void Bind(DarkAgesClient? client)
    {
        if (ReferenceEquals(client, Client))
            return;

        Client = client;
        Status = client is null ? "No client selected." : string.Empty;
        OnPropertyChanged(nameof(HasClient));
    }

    /// <summary>Refreshes the walker status line and the current-map warp list. Call on the UI thread.</summary>
    public void Refresh()
    {
        WalkerState = Client?.Bot.Walker is { Route: { } route } walker
            ? $"walking \"{route.Name}\"  [{Math.Min(walker.CurrentIndex + 1, route.Waypoints.Count)}/{route.Waypoints.Count}]"
            : "idle";

        SyncWarps();
    }

    public void NewRoute()
    {
        Route = new WaypointRoute("new-route");
        SyncWaypoints();
        NotifyRouteChanged();
        Status = "new route";
    }

    public void LoadRoute(string name)
    {
        if (RouteStore.Load(name, RoutesDirectory) is not { } loaded)
        {
            Status = $"route '{name}' not found";

            return;
        }

        Route = loaded;
        SyncWaypoints();
        NotifyRouteChanged();
        Status = $"loaded {name}";
    }

    public void SaveRoute()
    {
        if (string.IsNullOrWhiteSpace(Route.Name))
        {
            Status = "name the route first";

            return;
        }

        RouteStore.Save(Route, RoutesDirectory);
        ReloadRouteList();
        Status = $"saved {Route.Name}";
    }

    public void DeleteRoute(string name)
    {
        var path = Path.Combine(RoutesDirectory, name + RouteStore.Extension);

        if (File.Exists(path))
            File.Delete(path);

        ReloadRouteList();
        Status = $"deleted {name}";
    }

    public void AddWaypointHere()
    {
        if (Client?.Aisling?.Map is not { } map)
        {
            Status = "no map loaded";

            return;
        }

        var position = Client.ServerPoint;
        var waypoint = new Waypoint(map.Id, position.X, position.Y);
        Route.Waypoints.Add(waypoint);
        Waypoints.Add(waypoint);
        Status = $"added {waypoint}";
    }

    public void RemoveWaypoint(Waypoint waypoint)
    {
        Route.Waypoints.Remove(waypoint);
        Waypoints.Remove(waypoint);
    }

    public void WalkRoute()
    {
        if (Client is null)
        {
            Status = "no client selected";

            return;
        }

        if (Route.Waypoints.Count == 0)
        {
            Status = "route is empty";

            return;
        }

        Client.Bot.Walker.Route = Route;
        Client.Bot.Engine.Start();
        Status = $"walking {Route.Name}";
    }

    public void StopWalking()
    {
        if (Client is null)
            return;

        Client.Bot.Walker.Route = null;
        Status = "stopped";
    }

    public void RemoveWarp(WarpRow warp)
    {
        if (Client?.Aisling?.Map is not { } map)
            return;

        Client.RemoveWarp(map.Id, warp.SourceX, warp.SourceY);
        SyncWarps();
    }

    private void ReloadRouteList()
    {
        Routes.Clear();

        foreach (var name in RouteStore.List(RoutesDirectory))
            Routes.Add(name);
    }

    private void SyncWaypoints()
    {
        Waypoints.Clear();

        foreach (var waypoint in Route.Waypoints)
            Waypoints.Add(waypoint);
    }

    private void SyncWarps()
    {
        Warps.Clear();

        if (Client?.Aisling?.Map is not { } map || !Client.WorldStorage.Value.Maps.TryGetValue(map.Id, out var meta))
            return;

        foreach (var warp in meta.Warps)
            Warps.Add(new WarpRow(warp.SourcePoint.X, warp.SourcePoint.Y, warp.Destination.MapId, warp.Destination.X, warp.Destination.Y));
    }

    private void NotifyRouteChanged()
    {
        OnPropertyChanged(nameof(RouteName));
        OnPropertyChanged(nameof(Loop));
        OnPropertyChanged(nameof(Proximity));
    }
}

/// <summary>A row in the current-map warp list: the tile you step on and where it takes you.</summary>
public sealed record WarpRow(int SourceX, int SourceY, short DestMap, int DestX, int DestY)
{
    public string Summary => $"({SourceX}, {SourceY})  →  {DestMap}:({DestX}, {DestY})";
}
