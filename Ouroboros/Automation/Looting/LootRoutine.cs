using Chaos.Extensions.Geometry;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Ouroboros.Defintions;
using Ouroboros.Model;
using Ouroboros.Services.Pathfinding;

namespace Ouroboros.Automation.Looting;

/// <summary>
///     Auto-loot: walks to the nearest wanted ground drop and picks it up into the first free inventory slot.
///     One pickup grabs whatever is on the tile — gold or item — so a single path covers both. Ground drops
///     are identified only by sprite (the server sends no name), so filtering is by sprite: an optional
///     whitelist (<see cref="OnlySprites" />, empty = take anything) and blacklist (<see cref="IgnoreSprites" />).
///     Off by default; one action per tick; yields movement to the walker so routes aren't disrupted.
/// </summary>
public sealed class LootRoutine : BotRoutine
{
    private readonly PathfinderOptions PathOptions = new();
    private byte StepCount;

    public override string Name => "Loot";

    /// <summary>When false the routine idles.</summary>
    public bool Enabled { get; set; }

    /// <summary>Only pick up drops whose sprite is in this set. Empty = pick up any drop.</summary>
    public HashSet<ushort> OnlySprites { get; } = [];

    /// <summary>Never pick up drops whose sprite is in this set (trash / quest clutter).</summary>
    public HashSet<ushort> IgnoreSprites { get; } = [];

    /// <summary>Farthest a drop may be (in tiles) before we bother chasing it.</summary>
    public int Range { get; set; } = CONSTANTS.DEFAULT_MAX_RANGE;

    public override TimeSpan Interval => CONSTANTS.QUARTER_SECOND;

    public override ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (!Enabled)
            return default;

        if (context.Map is null)
            return default;

        //don't wrestle the walker for control of movement
        if (context.Walker.Route is not null)
            return default;

        var position = context.Position;

        var target = context.Entities
                            .GetNearbyGroundItems(Wanted)
                            .Where(item => Distance(position, item) <= Range)
                            .OrderBy(item => Distance(position, item))
                            .FirstOrDefault();

        if (target is null)
            return default;

        //standing on it — pick it up (into the first free slot; gold ignores the slot server-side)
        if ((target.X == position.X) && (target.Y == position.Y))
        {
            if (FirstFreeSlot(context) is { } destinationSlot)
                context.Server.SendPickup(new PickupArgs
                {
                    DestinationSlot = destinationSlot,
                    SourcePoint = new Point(position.X, position.Y)
                });

            return default;
        }

        //otherwise step toward it
        if (context.Pathfinder is { } pathfinder)
        {
            var path = pathfinder.FindPath(position, new Point(target.X, target.Y), PathOptions);

            if (path.Count > 0)
            {
                var direction = path.Peek().DirectionalRelationTo(position);

                if (direction != Direction.Invalid)
                    context.Server.SendClientWalk(new ClientWalkArgs { Direction = direction, StepCount = StepCount++ });
            }
        }

        return default;
    }

    private bool Wanted(GroundItem item)
        => !IgnoreSprites.Contains(item.Sprite) && ((OnlySprites.Count == 0) || OnlySprites.Contains(item.Sprite));

    private static int Distance(Point from, GroundItem item) => Math.Abs(item.X - from.X) + Math.Abs(item.Y - from.Y);

    //first empty inventory slot (1..capacity), or null when the pack is full
    private static byte? FirstFreeSlot(BotContext context)
    {
        for (byte slot = 1; slot <= Inventory.Capacity; slot++)
            if (context.Inventory[slot] is null)
                return slot;

        return null;
    }
}
