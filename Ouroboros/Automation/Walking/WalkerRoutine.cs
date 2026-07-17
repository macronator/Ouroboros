using Chaos.Extensions.Geometry;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Ouroboros.Services.Pathfinding;

namespace Ouroboros.Automation.Walking;

/// <summary>
///     Walks the character along a <see cref="WaypointRoute" />, one step per tick. On the waypoint's map it
///     A*-paths to the tile and advances by proximity; on a different map it uses the route finder to pick
///     the next map hop and A*-paths to the warp/gate/field tile that leads there, stepping through it.
/// </summary>
public sealed class WalkerRoutine : BotRoutine
{
    private readonly PathfinderOptions PathOptions = new();
    private byte StepCount;

    private WaypointRoute? RouteField;

    public override string Name => "Walker";

    /// <summary>The route to walk. Null or empty means the walker idles. Assigning a route resets progress.</summary>
    public WaypointRoute? Route
    {
        get => RouteField;
        set
        {
            RouteField = value;
            CurrentIndex = 0;
        }
    }

    /// <summary>Index of the waypoint currently being walked toward.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>Pacing follows the route's configured step delay.</summary>
    public override TimeSpan Interval => Route?.Options.StepDelay ?? base.Interval;

    public override ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken)
    {
        var route = Route;

        if (route is null || route.Waypoints.Count == 0)
            return default;

        var map = context.Map;
        var pathfinder = context.Pathfinder;

        //need a loaded map and its pathfinder before we can move
        if (map is null)
            return Report("no current map loaded");

        if (pathfinder is null)
            return Report($"no pathfinder for map {map.Id} (map tile data not loaded)");

        if (CurrentIndex >= route.Waypoints.Count)
            CurrentIndex = 0;

        var target = route.Waypoints[CurrentIndex];
        var current = context.Position;

        if (target.MapId == map.Id)
        {
            var targetPoint = new Point(target.X, target.Y);

            if (current.DistanceFrom(targetPoint) <= route.Options.Proximity)
            {
                AdvanceIndex(route);

                return default;
            }

            //no reachable step (blocked or effectively arrived) — advance so we don't stall forever
            if (TryStepToward(context, pathfinder, current, targetPoint))
                Report($"stepping to ({target.X},{target.Y}) on map {map.Id} from ({current.X},{current.Y})");
            else
            {
                Report($"no path from ({current.X},{current.Y}) to ({target.X},{target.Y}) on map {map.Id}");
                AdvanceIndex(route);
            }

            return default;
        }

        //different map: head for the transition tile toward the next hop on the route
        var hops = context.Routefinder.FindRoute(map.Id, target.MapId);

        if (hops.Count == 0)
            return Report($"no route from map {map.Id} to map {target.MapId}");

        var nextHop = hops.Peek();

        if (context.Routefinder.FindTransitionPoint(map.Id, nextHop, current) is not { } transition)
            return Report($"no transition tile from map {map.Id} toward map {nextHop}");

        if (TryStepToward(context, pathfinder, current, transition))
            Report($"heading to transition ({transition.X},{transition.Y}) toward map {nextHop}");
        else
            Report($"can't reach transition ({transition.X},{transition.Y}) toward map {nextHop} from ({current.X},{current.Y})");

        return default;
    }

    private string LastStatus = "";

    //logs the walker's decision to the debug output, but only when it changes (avoids per-tick spam)
    private ValueTask Report(string status)
    {
        if (status != LastStatus)
        {
            LastStatus = status;
            System.Diagnostics.Debug.WriteLine($"[Ouroboros] walker: {status}");
        }

        return default;
    }

    /// <summary>Issues one walk step toward <paramref name="targetPoint" />; returns false when none could be made.</summary>
    private bool TryStepToward(BotContext context, Pathfinder pathfinder, Point current, IPoint targetPoint)
    {
        if (current.DistanceFrom(targetPoint) == 0)
            return false;

        var path = pathfinder.FindPath(current, targetPoint, PathOptions);

        if (path.Count == 0)
            return false;

        var direction = path.Peek().DirectionalRelationTo(current);

        if (direction == Direction.Invalid)
            return false;

        context.Server.SendClientWalk(new ClientWalkArgs
        {
            Direction = direction,
            StepCount = StepCount++
        });

        return true;
    }

    private void AdvanceIndex(WaypointRoute route)
    {
        CurrentIndex++;

        if (CurrentIndex < route.Waypoints.Count)
            return;

        CurrentIndex = route.Options.Loop ? 0 : route.Waypoints.Count - 1;
    }
}
