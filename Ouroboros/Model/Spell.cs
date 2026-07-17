using Chaos.Networking.Entities.Server;

namespace Ouroboros.Model;

/// <summary>A spell in the character's spell pane, with cast-duration and cooldown/ready tracking.</summary>
public sealed class Spell
{
    public Spell(SpellInfo info)
    {
        Slot = info.Slot;
        Name = ResolveName(info);
        PanelName = info.PanelName;
        Prompt = info.Prompt;
        CastLines = info.CastLines;
        Duration = SpellDurations.For(Name);
    }

    //Chaos's SpellInfo has both Name and PanelName, but the 7.41 AddSpell packet carries a single name
    //string — depending on field order it lands in one or the other, so take whichever is populated.
    internal static string ResolveName(SpellInfo info)
        => !string.IsNullOrEmpty(info.Name) ? info.Name : info.PanelName ?? string.Empty;

    public byte Slot { get; }
    public string Name { get; private set; }
    public string PanelName { get; private set; }
    public string Prompt { get; private set; }
    public byte CastLines { get; private set; }

    /// <summary>Buff/effect duration used to avoid recasting while still active (zero = freely castable).</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>Mana cost — not carried on the wire; set by higher layers if known.</summary>
    public int ManaCost { get; set; }

    public DateTime? LastCastUtc { get; private set; }
    public TimeSpan Cooldown { get; private set; }
    public DateTime? CooldownStartedUtc { get; private set; }

    public void UpdateFrom(SpellInfo info)
    {
        Name = ResolveName(info);
        PanelName = info.PanelName;
        Prompt = info.Prompt;
        CastLines = info.CastLines;
        Duration = SpellDurations.For(Name);
    }

    /// <summary>Records that we just cast this spell, starting its active-duration window.</summary>
    public void MarkCast(DateTime utcNow) => LastCastUtc = utcNow;

    /// <summary>Marks the spell as on an explicit server cooldown.</summary>
    public void StartCooldown(TimeSpan cooldown, DateTime utcNow)
    {
        Cooldown = cooldown;
        CooldownStartedUtc = utcNow;
    }

    /// <summary>
    ///     Whether the spell can be cast now — driven by the explicit server cooldown only, so offensive
    ///     spells remain castable regardless of their (buff-oriented) <see cref="Duration" />. Use
    ///     <see cref="IsBuffActiveAt" /> to decide whether a buff still needs recasting.
    /// </summary>
    public bool IsReadyAt(DateTime utcNow)
        => Cooldown <= TimeSpan.Zero
           || CooldownStartedUtc is not { } started
           || (utcNow - started) >= Cooldown;

    public bool IsReady => IsReadyAt(DateTime.UtcNow);

    /// <summary>Whether this spell's buff/effect is still active from our last cast (avoids recasting buffs).</summary>
    public bool IsBuffActiveAt(DateTime utcNow)
        => Duration > TimeSpan.Zero && LastCastUtc is { } cast && (utcNow - cast) < Duration;

    public bool IsBuffActive => IsBuffActiveAt(DateTime.UtcNow);
}
