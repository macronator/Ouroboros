using Chaos.Networking.Entities.Client;
using Ouroboros.Defintions;

namespace Ouroboros.Automation.Support;

/// <summary>
///     Consumable auto-use: when HP or MP falls to a configured percentage, uses the first matching ready
///     consumable from the inventory (a healing potion, a mana item). Off by default and with empty item
///     lists, so nothing is ever consumed unasked. Issues at most one use per tick and paces re-use, so a
///     single low-vitals reading doesn't burn the whole stack before the server reports the recovery.
/// </summary>
public sealed class ItemRoutine : BotRoutine
{
    private DateTime LastUseUtc = DateTime.MinValue;

    public override string Name => "Consumables";

    /// <summary>When false the routine idles.</summary>
    public bool Enabled { get; set; }

    /// <summary>Use a healing item when HP percent is at or below this value.</summary>
    public int HealHealthThreshold { get; set; } = 50;

    /// <summary>Use a mana item when MP percent is at or below this value.</summary>
    public int RestoreManaThreshold { get; set; } = 30;

    /// <summary>Inventory item names treated as HP restoratives, highest priority first (empty = disabled).</summary>
    public List<string> HealItems { get; } = [];

    /// <summary>Inventory item names treated as MP restoratives, highest priority first (empty = disabled).</summary>
    public List<string> ManaItems { get; } = [];

    /// <summary>Minimum gap between consecutive uses, so we don't over-consume before vitals refresh.</summary>
    public TimeSpan MinInterval { get; set; } = CONSTANTS.ONE_SECOND;

    public override TimeSpan Interval => CONSTANTS.HALF_SECOND;

    public override ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (!Enabled)
            return default;

        var vitals = context.Vitals;
        var now = DateTime.UtcNow;

        //pace re-use: the server needs a moment to broadcast the vitals change after a consume
        if ((now - LastUseUtc) < MinInterval)
            return default;

        //HP first — staying alive beats topping off mana
        if (HealItems.Count > 0
            && vitals.MaximumHp > 0
            && vitals.HealthPercent <= HealHealthThreshold
            && TryUseFirst(context, HealItems, now))
            return default;

        if (ManaItems.Count > 0 && vitals.MaximumMp > 0 && vitals.ManaPercent <= RestoreManaThreshold)
            TryUseFirst(context, ManaItems, now);

        return default;
    }

    //uses the first named item actually present in the inventory; returns whether one was used
    private bool TryUseFirst(BotContext context, List<string> names, DateTime now)
    {
        foreach (var name in names)
        {
            var item = context.Inventory[name];

            if (item is null)
                continue;

            context.Server.SendItemUse(new ItemUseArgs { SourceSlot = item.Slot });
            item.MarkUsed(now);
            LastUseUtc = now;

            return true;
        }

        return false;
    }
}
