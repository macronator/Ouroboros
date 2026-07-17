using Chaos.Networking.Entities.Server;

namespace Ouroboros.Model;

/// <summary>The character's skill pane: skills indexed by slot and by name, updated from server packets.</summary>
public sealed class SkillBook
{
    private readonly Dictionary<string, Skill> ByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<byte, Skill> BySlot = new();
    private readonly object Gate = new();

    public Skill? this[byte slot]
    {
        get
        {
            lock (Gate)
                return BySlot.GetValueOrDefault(slot);
        }
    }

    public Skill? this[string name]
    {
        get
        {
            lock (Gate)
                return ByName.GetValueOrDefault(name);
        }
    }

    public IReadOnlyList<Skill> Snapshot()
    {
        lock (Gate)
            return BySlot.Values.ToArray();
    }

    public void AddOrUpdate(SkillInfo info)
    {
        //empty pane slots broadcast a blank name in both fields — treat them as clearing the slot
        if (string.IsNullOrEmpty(Skill.ResolveName(info)))
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

                //if the slot's skill was renamed, drop the stale name key
                if (!string.Equals(oldName, existing.Name, StringComparison.OrdinalIgnoreCase))
                    ByName.Remove(oldName);

                ByName[existing.Name] = existing;
            } else
            {
                var skill = new Skill(info);
                BySlot[info.Slot] = skill;
                ByName[skill.Name] = skill;
            }
        }
    }

    public void Remove(byte slot)
    {
        lock (Gate)
        {
            if (!BySlot.Remove(slot, out var skill))
                return;

            //only drop the name key if it still points at the skill we removed
            if (ByName.TryGetValue(skill.Name, out var named) && named.Slot == slot)
                ByName.Remove(skill.Name);
        }
    }

    public void StartCooldown(byte slot, TimeSpan cooldown, DateTime utcNow)
    {
        lock (Gate)
            if (BySlot.TryGetValue(slot, out var skill))
                skill.StartCooldown(cooldown, utcNow);
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
