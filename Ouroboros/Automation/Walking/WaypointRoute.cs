namespace Ouroboros.Automation.Walking;

/// <summary>A named, ordered list of waypoints plus the options for walking them.</summary>
public sealed class WaypointRoute
{
    public WaypointRoute(string name) => Name = name;

    public string Name { get; set; }
    public List<Waypoint> Waypoints { get; } = [];
    public WalkOptions Options { get; set; } = new();
}
