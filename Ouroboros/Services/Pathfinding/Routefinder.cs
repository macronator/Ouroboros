using System.Collections.Frozen;
using Chaos.Common.Synchronization;
using Chaos.Extensions.Common;
using Chaos.Extensions.Geometry;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions;
using Ouroboros.Abstractions;
using Ouroboros.Data.Meta;

namespace Ouroboros.Services.Pathfinding;

public sealed class Routefinder
{
    private readonly PriorityQueue<RouteNode, int> PriorityQueue;
    private FrozenDictionary<short, RouteNode> RouteNodes;
    private readonly AutoReleasingMonitor Sync;
    private readonly WorldMeta World;

    public Routefinder(IReadOnlyStorage<WorldMeta> routeWorld)
    {
        World = routeWorld.Value;
        Sync = new AutoReleasingMonitor();
        PriorityQueue = new PriorityQueue<RouteNode, int>();
        RouteNodes = BuildGraph();
    }

    /// <summary>Rebuilds the world graph from the (possibly mutated) <see cref="WorldMeta" />.</summary>
    public void Rebuild()
    {
        using var @lock = Sync.Enter();

        RouteNodes = BuildGraph();
    }

    private FrozenDictionary<short, RouteNode> BuildGraph()
    {
        var nodes = new Dictionary<short, RouteNode>();

        foreach (var map in World.Maps.Values)
        {
            var node = new RouteNode
            {
                MapId = map.MapId,
            };

            nodes.Add(node.MapId, node);
        }

        foreach (var node in nodes.Values)
        {
            var neighborIds = new HashSet<short>();

            if(!World.Maps.TryGetValue(node.MapId, out var routeMap))
                continue;

            //add warp destinations as neighbors
            neighborIds.AddRange(routeMap.Warps.Select(warp => warp.Destination.MapId));

            //add field destinations as neighbors
            foreach (var fieldRef in routeMap.Fields)
            {
                if(!World.Fields.TryGetValue(fieldRef.FieldId, out var field))
                    continue;

                neighborIds.AddRange(field.Destinations.Select(destination => destination.MapId));
            }

            //add gates as neighbors
            neighborIds.AddRange(routeMap.Gates.Select(gate => gate.Destination.MapId));

            //prune unknown ids
            neighborIds.RemoveWhere(id => !nodes.ContainsKey(id));

            node.Neighbors = neighborIds.Select(id => nodes[id])
                                        .ToArray();
        }

        return nodes.ToFrozenDictionary();
    }
    
    internal Stack<short> FindRoute(short startMapId, short endMapId)
    {
        if(startMapId == endMapId)
            return new Stack<short>();
        
        if(!RouteNodes.TryGetValue(startMapId, out var startNode))
            return new Stack<short>();
        
        if(!RouteNodes.TryGetValue(endMapId, out var endNode))
            return new Stack<short>();

        using var @lock = Sync.Enter();
        
        //reset queue
        PriorityQueue.Clear();
        
        //prepare nodes
        foreach (var node in RouteNodes.Values)
            node.Reset();
        
        PriorityQueue.Enqueue(startNode, 0);

        while (PriorityQueue.Count > 0)
        {
            var node = PriorityQueue.Dequeue();

            if (node.Closed)
                continue;

            foreach (var neighbor in node.Neighbors)
            {
                if (neighbor.Closed || neighbor.Open)
                    continue;

                if (neighbor.Equals(endNode))
                {
                    neighbor.Parent = node;
                    
                    return new Stack<short>(GetParentChain(endNode));
                }

                neighbor.Parent = node;
                neighbor.AccumulatedCost = node.AccumulatedCost + 1;

                PriorityQueue.Enqueue(neighbor, neighbor.AccumulatedCost);
            }

            node.Closed = true;
            node.Open = false;
        }

        return new Stack<short>(GetParentChain(endNode));
    }
    
    private IEnumerable<short> GetParentChain(RouteNode routeNode)
    {
        while (routeNode.Parent != null)
        {
            yield return routeNode.MapId;

            routeNode = routeNode.Parent;
        }
    }

    /// <summary>
    ///     Finds the tile on <paramref name="fromMapId" /> to step onto to travel to
    ///     <paramref name="toMapId" /> — the source point of the nearest warp, gate, or field connecting the
    ///     two maps. Returns null if no such transition is known.
    /// </summary>
    public Point? FindTransitionPoint(short fromMapId, short toMapId, IPoint from)
    {
        if (!World.Maps.TryGetValue(fromMapId, out var map))
            return null;

        Point? best = null;
        var bestDistance = int.MaxValue;

        void Consider(Point source)
        {
            var distance = from.DistanceFrom(source);

            if (distance >= bestDistance)
                return;

            best = source;
            bestDistance = distance;
        }

        foreach (var warp in map.Warps)
            if (warp.Destination.MapId == toMapId)
                Consider(warp.SourcePoint);

        foreach (var gate in map.Gates)
            if (gate.Destination.MapId == toMapId)
                Consider(gate.SourcePoint);

        foreach (var fieldRef in map.Fields)
            if (World.Fields.TryGetValue(fieldRef.FieldId, out var field)
                && field.Destinations.Any(destination => destination.MapId == toMapId))
                Consider(fieldRef.SourcePoint);

        return best;
    }
}