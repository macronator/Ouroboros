using Chaos.Networking.Entities.Server;

namespace Ouroboros.Model;

/// <summary>A skill in the character's skill pane, with cooldown/ready tracking.</summary>
public sealed class Skill
{
    public Skill(SkillInfo info)
    {
        Slot = info.Slot;
        Name = ResolveName(info);
        PanelName = info.PanelName;
        Sprite = info.Sprite;
    }

    //Chaos's SkillInfo has both Name and PanelName, but the 7.41 AddSkill packet carries a single name
    //string — depending on field order it lands in one or the other, so take whichever is populated.
    internal static string ResolveName(SkillInfo info)
        => !string.IsNullOrEmpty(info.Name) ? info.Name : info.PanelName ?? string.Empty;

    public byte Slot { get; }
    public string Name { get; private set; }
    public string PanelName { get; private set; }
    public ushort Sprite { get; private set; }

    /// <summary>The cooldown last reported by the server (<see cref="TimeSpan.Zero" /> = none).</summary>
    public TimeSpan Cooldown { get; private set; }

    /// <summary>When the current cooldown started (UTC), or null if it has never been on cooldown.</summary>
    public DateTime? CooldownStartedUtc { get; private set; }

    public void UpdateFrom(SkillInfo info)
    {
        Name = ResolveName(info);
        PanelName = info.PanelName;
        Sprite = info.Sprite;
    }

    /// <summary>Marks the skill as on cooldown for <paramref name="cooldown" />, starting at <paramref name="utcNow" />.</summary>
    public void StartCooldown(TimeSpan cooldown, DateTime utcNow)
    {
        Cooldown = cooldown;
        CooldownStartedUtc = utcNow;
    }

    public bool IsReadyAt(DateTime utcNow)
        => Cooldown <= TimeSpan.Zero
           || CooldownStartedUtc is not { } started
           || (utcNow - started) >= Cooldown;

    public bool IsReady => IsReadyAt(DateTime.UtcNow);

    public TimeSpan RemainingCooldownAt(DateTime utcNow)
        => CooldownStartedUtc is { } started
            ? TimeSpan.FromTicks(Math.Max(0, (Cooldown - (utcNow - started)).Ticks))
            : TimeSpan.Zero;
}
