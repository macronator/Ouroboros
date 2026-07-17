using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>
///     Scaffold view-model for the waypoints tab. The route model and persistence already exist
///     (<c>Automation/Walking/</c>, <c>RouteStore</c>); this tab will host the waypoint/route editor and the
///     auto-learned world-warp list. Kept minimal and designer-ready until then.
/// </summary>
public sealed class WaypointsViewModel : NotifyPropertyChangedBase
{
    public string Title => "Waypoints";

    public string Placeholder
        => "Editor for waypoint routes and the auto-learned world-warp graph, driving the walker. "
           + "Backed by Automation/Walking/ and the WorldMeta store — UI binding pending.";
}
