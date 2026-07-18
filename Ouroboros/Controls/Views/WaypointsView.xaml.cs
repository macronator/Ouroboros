using System.Windows;
using Ouroboros.Automation.Walking;
using Ouroboros.ViewModel;

namespace Ouroboros.Controls.Views;

/// <summary>Interaction logic for WaypointsView.xaml — route editor, walker controls, and current-map warps.</summary>
public sealed partial class WaypointsView
{
    public WaypointsView() => InitializeComponent();

    private WaypointsViewModel? ViewModel => DataContext as WaypointsViewModel;

    private void LoadRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (RoutesList.SelectedItem is string name)
            ViewModel?.LoadRoute(name);
    }

    private void DeleteRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (RoutesList.SelectedItem is string name)
            ViewModel?.DeleteRoute(name);
    }

    private void NewRouteButton_Click(object sender, RoutedEventArgs e) => ViewModel?.NewRoute();

    private void SaveRouteButton_Click(object sender, RoutedEventArgs e) => ViewModel?.SaveRoute();

    private void AddWaypointButton_Click(object sender, RoutedEventArgs e) => ViewModel?.AddWaypointHere();

    private void RemoveWaypointButton_Click(object sender, RoutedEventArgs e)
    {
        if (WaypointsList.SelectedItem is Waypoint waypoint)
            ViewModel?.RemoveWaypoint(waypoint);
    }

    private void WalkButton_Click(object sender, RoutedEventArgs e) => ViewModel?.WalkRoute();

    private void StopButton_Click(object sender, RoutedEventArgs e) => ViewModel?.StopWalking();

    private void RemoveWarpButton_Click(object sender, RoutedEventArgs e)
    {
        if (WarpsList.SelectedItem is WarpRow warp)
            ViewModel?.RemoveWarp(warp);
    }
}
