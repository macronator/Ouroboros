using Chaos.Networking.Entities.Server;

namespace Ouroboros.Model;

/// <summary>
///     The character's inventory pane: items kept by slot and updated from the server's add/remove packets.
///     Multiple slots can hold identically-named stacks, so name lookups scan and stack totals sum across slots.
/// </summary>
public sealed class Inventory
{
    /// <summary>Usable inventory slots in Dark Ages (slot 0 is reserved and never holds an item).</summary>
    public const int Capacity = 59;

    private readonly Dictionary<byte, Item> BySlot = new();
    private readonly object Gate = new();

    public Item? this[byte slot]
    {
        get
        {
            lock (Gate)
                return BySlot.GetValueOrDefault(slot);
        }
    }

    /// <summary>The first item matching <paramref name="name" /> (case-insensitive), or null.</summary>
    public Item? this[string name]
    {
        get
        {
            lock (Gate)
                return BySlot.Values.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Number of occupied slots.</summary>
    public int Count
    {
        get
        {
            lock (Gate)
                return BySlot.Count;
        }
    }

    public bool IsFull
    {
        get
        {
            lock (Gate)
                return BySlot.Count >= Capacity;
        }
    }

    public int FreeSlots
    {
        get
        {
            lock (Gate)
                return Math.Max(0, Capacity - BySlot.Count);
        }
    }

    public IReadOnlyList<Item> Snapshot()
    {
        lock (Gate)
            return BySlot.Values.ToArray();
    }

    public bool Contains(string name) => this[name] is not null;

    /// <summary>Total stack count across every slot holding <paramref name="name" /> (case-insensitive).</summary>
    public long CountOf(string name)
    {
        lock (Gate)
            return BySlot.Values
                         .Where(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                         .Sum(item => (long)item.Count);
    }

    public void AddOrUpdate(ItemInfo info)
    {
        //a cleared slot broadcasts a blank name — treat that as removing the slot
        if (string.IsNullOrEmpty(info.Name))
        {
            Remove(info.Slot);

            return;
        }

        lock (Gate)
        {
            if (BySlot.TryGetValue(info.Slot, out var existing))
                existing.UpdateFrom(info);
            else
                BySlot[info.Slot] = new Item(info);
        }
    }

    public void Remove(byte slot)
    {
        lock (Gate)
            BySlot.Remove(slot);
    }

    public void Clear()
    {
        lock (Gate)
            BySlot.Clear();
    }
}
