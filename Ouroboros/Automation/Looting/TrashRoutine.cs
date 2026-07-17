using Chaos.Geometry;
using Chaos.Networking.Entities.Client;
using Ouroboros.Defintions;
using Ouroboros.Model;

namespace Ouroboros.Automation.Looting;

/// <summary>
///     Drops junk: any inventory item whose name is on the trash list is dropped at the character's feet.
///     Unlike ground drops, inventory items carry names, so the list is by name. Off by default with an empty
///     list (nothing dropped unasked); one drop per tick, paced so a full pack clears steadily without flooding.
/// </summary>
public sealed class TrashRoutine : BotRoutine
{
    private DateTime LastDropUtc = DateTime.MinValue;

    public override string Name => "Drop trash";

    /// <summary>When false the routine idles.</summary>
    public bool Enabled { get; set; }

    /// <summary>Inventory item names to drop on sight (case-insensitive; empty = disabled).</summary>
    public List<string> TrashItems { get; } = [];

    /// <summary>Minimum gap between drops, so clearing a full pack doesn't flood the server.</summary>
    public TimeSpan MinInterval { get; set; } = CONSTANTS.HALF_SECOND;

    public override TimeSpan Interval => CONSTANTS.HALF_SECOND;

    public override ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (!Enabled || (TrashItems.Count == 0))
            return default;

        var now = DateTime.UtcNow;

        if ((now - LastDropUtc) < MinInterval)
            return default;

        //first inventory item flagged as trash, in list priority order
        Item? trash = null;

        foreach (var name in TrashItems)
        {
            trash = context.Inventory[name];

            if (trash is not null)
                break;
        }

        if (trash is null)
            return default;

        var position = context.Position;

        context.Server.SendItemDrop(new ItemDropArgs
        {
            SourceSlot = trash.Slot,
            Count = (int)trash.Count,
            DestinationPoint = new Point(position.X, position.Y)
        });

        LastDropUtc = now;

        return default;
    }
}
