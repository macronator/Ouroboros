using Chaos.Networking.Entities.Server;

namespace Ouroboros.Model;

/// <summary>The character's spell pane: spells indexed by slot and by name, updated from server packets.</summary>
public sealed class SpellBook
{
    private readonly Dictionary<string, Spell> ByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<byte, Spell> BySlot = new();
    private readonly object Gate = new();

    public Spell? this[byte slot]
    {
        get
        {
            lock (Gate)
                return BySlot.GetValueOrDefault(slot);
        }
    }

    public Spell? this[string name]
    {
        get
        {
            lock (Gate)
                return ByName.GetValueOrDefault(name);
        }
    }

    public IReadOnlyList<Spell> Snapshot()
    {
        lock (Gate)
            return BySlot.Values.ToArray();
    }

    public void AddOrUpdate(SpellInfo info)
    {
        //empty pane slots broadcast a blank name in both fields — treat them as clearing the slot
        if (string.IsNullOrEmpty(Spell.ResolveName(info)))
        {
            Remove(info.Slot);

            return;
        }

        lock (Gate)
        {
            if (BySlot.TryGetValue(info.Slot, out var existing))
            {
                var oldName = existing.Name;
                existing.UpdateFrom(info);

                if (!string.Equals(oldName, existing.Name, StringComparison.OrdinalIgnoreCase))
                    ByName.Remove(oldName);

                ByName[existing.Name] = existing;
            } else
            {
                var spell = new Spell(info);
                BySlot[info.Slot] = spell;
                ByName[spell.Name] = spell;
            }
        }
    }

    public void Remove(byte slot)
    {
        lock (Gate)
        {
            if (!BySlot.Remove(slot, out var spell))
                return;

            if (ByName.TryGetValue(spell.Name, out var named) && named.Slot == slot)
                ByName.Remove(spell.Name);
        }
    }

    public void StartCooldown(byte slot, TimeSpan cooldown, DateTime utcNow)
    {
        lock (Gate)
            if (BySlot.TryGetValue(slot, out var spell))
                spell.StartCooldown(cooldown, utcNow);
    }

    /// <summary>Records a cast against the spell in <paramref name="slot" />, if present.</summary>
    public void MarkCast(byte slot, DateTime utcNow)
    {
        lock (Gate)
            if (BySlot.TryGetValue(slot, out var spell))
                spell.MarkCast(utcNow);
    }

    public void Clear()
    {
        lock (Gate)
        {
            BySlot.Clear();
            ByName.Clear();
        }
    }
}
