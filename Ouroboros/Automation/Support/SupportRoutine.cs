using Chaos.Networking.Entities.Client;
using Ouroboros.Defintions;

namespace Ouroboros.Automation.Support;

/// <summary>
///     Keeps the character alive and buffed: casts a ready heal when HP drops to a threshold, and maintains
///     a configured set of self-buffs (recasting only once their tracked duration lapses). Off by default;
///     issues at most one action per tick. Dispelling debuffs is a later addition (needs effect tracking).
/// </summary>
public sealed class SupportRoutine : BotRoutine
{
    public override string Name => "Support";

    /// <summary>When false the routine idles.</summary>
    public bool Enabled { get; set; }

    /// <summary>Heal when current HP percent is at or below this value.</summary>
    public int HealHealthThreshold { get; set; } = 60;

    /// <summary>Only maintain buffs while MP percent is at or above this value.</summary>
    public int MinManaPercentForBuffs { get; set; } = 20;

    /// <summary>Names of self-buff spells to keep active (empty by default so nothing is cast unasked).</summary>
    public List<string> Buffs { get; } = [];

    public override TimeSpan Interval => CONSTANTS.HALF_SECOND;

    public override ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (!Enabled)
            return default;

        var self = context.Self;
        var vitals = context.Vitals;

        if (self is null)
            return default;

        var now = DateTime.UtcNow;

        //healing takes priority
        if (vitals.MaximumHp > 0 && vitals.HealthPercent <= HealHealthThreshold)
        {
            var heal = context.Spells.Snapshot().FirstOrDefault(spell => spell.IsReadyAt(now) && CONSTANTS.KNOWN_HEALS.Contains(spell.Name));

            if (heal is not null)
            {
                Cast(context, heal.Slot, self.Id);
                heal.MarkCast(now);

                return default;
            }
        }

        //maintain buffs one at a time
        if (Buffs.Count > 0 && vitals.ManaPercent >= MinManaPercentForBuffs)
            foreach (var name in Buffs)
            {
                var buff = context.Spells[name];

                if (buff is null || !buff.IsReadyAt(now) || buff.IsBuffActiveAt(now))
                    continue;

                Cast(context, buff.Slot, self.Id);
                buff.MarkCast(now);

                return default;
            }

        return default;
    }

    private static void Cast(BotContext context, byte slot, uint selfId)
        => context.Server.SendSpellUse(new SpellUseArgs
        {
            SourceSlot = slot,
            ArgsData = SelfTarget(selfId)
        });

    //Cast on ourselves as the target (point zeroed) — DALib 7.41 UseSpellPacket format
    //[u32 BE serial][u16 BE x][u16 BE y]; the server reads the serial.
    private static byte[] SelfTarget(uint id) =>
    [
        (byte)(id >> 24), (byte)(id >> 16), (byte)(id >> 8), (byte)id,
        0, 0, 0, 0
    ];
}
