namespace Ouroboros.Automation.Walking;

/// <summary>A single map-qualified destination on a walking route.</summary>
public readonly record struct Waypoint(short MapId, int X, int Y)
{
    public override string ToString() => $"{MapId}:({X}, {Y})";
}
