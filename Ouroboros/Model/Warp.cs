using System.Diagnostics.CodeAnalysis;
using Ouroboros.Data;
using Ouroboros.Utilities;

namespace Ouroboros.Model;

public class Warp : MapEntity
{
    public IdLocation Destination { get; }

    /// <inheritdoc />
    [SetsRequiredMembers]
    public Warp(
        Map map,
        int x,
        int y,
        IdLocation destination,
        MapEntityTrackers? trackers = null)
        : base(
            map,
            x,
            y,
            trackers)
        => Destination = destination;
}