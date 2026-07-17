using Chaos.Networking.Entities.Server;

namespace Ouroboros.Model;

/// <summary>An item occupying an inventory slot, tracked from the server's inventory-pane packets.</summary>
public sealed class Item
{
    public Item(ItemInfo info) => UpdateFrom(info, info.Slot);

    public byte Slot { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ushort Sprite { get; private set; }

    /// <summary>Stack size; non-stackable items report 1.</summary>
    public uint Count { get; private set; }
    public bool Stackable { get; private set; }
    public int CurrentDurability { get; private set; }
    public int MaximumDurability { get; private set; }

    /// <summary>When automation last used this item (UTC), for use-pacing; null if never used.</summary>
    public DateTime? LastUsedUtc { get; private set; }

    /// <summary>Durability as a 0–100 percentage, or null for items that have no durability.</summary>
    public int? DurabilityPercent
        => MaximumDurability > 0 ? (int)(CurrentDurability * 100L / MaximumDurability) : null;

    public void UpdateFrom(ItemInfo info) => UpdateFrom(info, Slot);

    private void UpdateFrom(ItemInfo info, byte slot)
    {
        Slot = slot;
        Name = info.Name ?? string.Empty;
        Sprite = info.Sprite;
        Count = info.Count ?? 1;
        Stackable = info.Stackable;
        CurrentDurability = info.CurrentDurability;
        MaximumDurability = info.MaxDurability;
    }

    public void MarkUsed(DateTime utcNow) => LastUsedUtc = utcNow;
}
