using Chaos.Extensions.Geometry;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Ouroboros.Defintions;

namespace Ouroboros.Automation.Combat;

/// <summary>
///     The "Crasher" execute combo. When a live target is adjacent AND an execute skill is off cooldown, it
///     (optionally) applies pre-buffs, drops the character's own HP to the trigger via a self-damage skill or
///     item, then uses the execute skill.
///     <para>
///         SAFETY: it NEVER self-damages unless both a live adjacent target exists and an execute skill is
///         ready — so the character is never stranded at low HP with no payoff — and it never drives HP below
///         the trigger once there. Fully opt-in: <see cref="Enabled" /> is false and every name is unset by
///         default, so the routine is inert until deliberately configured and enabled. Because it intentionally
///         brings you to ~1 HP, it must be live-tested before it is trusted.
///     </para>
/// </summary>
public sealed class CrasherRoutine : BotRoutine
{
    private DateTime LastActionUtc = DateTime.MinValue;
    private bool BuffsApplied;

    public override string Name => "Crasher";

    /// <summary>When false the routine idles. Off by default.</summary>
    public bool Enabled { get; set; }

    /// <summary>Execute skills tried in priority order (e.g. Crasher, Execute, Animal Feast). Empty = disabled.</summary>
    public List<string> ExecuteSkills { get; } = [];

    /// <summary>Optional pre-buff skills applied once before the combo (e.g. Mad Soul, Sacrifice).</summary>
    public List<string> PreBuffSkills { get; } = [];

    /// <summary>A skill that lowers your own HP to the trigger (e.g. Auto Hemloch), or null.</summary>
    public string? SelfDamageSkill { get; set; }

    /// <summary>An inventory item that lowers your own HP (e.g. Hemloch / Satchel of Hemloch), or null.</summary>
    public string? SelfDamageItem { get; set; }

    /// <summary>Use the execute skill once current HP is at or below this value (1 = guaranteed).</summary>
    public int HpThreshold { get; set; } = 1;

    /// <summary>Minimum gap between actions, so one reading doesn't burn the whole sequence at once.</summary>
    public TimeSpan MinInterval { get; set; } = CONSTANTS.HALF_SECOND;

    public override TimeSpan Interval => CONSTANTS.QUARTER_SECOND;

    public override ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (!Enabled || (ExecuteSkills.Count == 0))
            return default;

        var vitals = context.Vitals;

        if (context.Self is null || (vitals.MaximumHp == 0))
            return default;

        //SAFETY GATE 1: require a live adjacent target — never self-damage into empty air
        if (!HasAdjacentTarget(context))
        {
            BuffsApplied = false; //reset the combo when there is nothing to hit

            return default;
        }

        //SAFETY GATE 2: require a ready execute skill — never self-damage if we can't cash it in
        var execute = FirstReadySkill(context, ExecuteSkills);

        if (execute is null)
            return default;

        var now = DateTime.UtcNow;

        if ((now - LastActionUtc) < MinInterval)
            return default;

        //optional pre-buffs, one per tick, before dropping HP
        if ((PreBuffSkills.Count > 0) && !BuffsApplied)
        {
            foreach (var buff in PreBuffSkills)
                if (context.UseSkill(buff))
                {
                    LastActionUtc = now;

                    return default;
                }

            BuffsApplied = true; //none were ready/known — stop waiting on them
        }

        //at or under the trigger → execute (turn to face first; the server resolves the adjacent target)
        if (vitals.CurrentHp <= (uint)Math.Max(1, HpThreshold))
        {
            FaceAdjacentTarget(context);
            context.UseSkill(execute);
            LastActionUtc = now;
            BuffsApplied = false; //combo consumed

            return default;
        }

        //above the trigger → apply exactly one self-damage step (skill preferred, then item)
        if (SelfDamageSkill is { Length: > 0 } damageSkill && context.UseSkill(damageSkill))
        {
            LastActionUtc = now;

            return default;
        }

        if (SelfDamageItem is { Length: > 0 } damageItem && context.Inventory[damageItem] is { } item)
        {
            context.Server.SendItemUse(new ItemUseArgs { SourceSlot = item.Slot });
            item.MarkUsed(now);
            LastActionUtc = now;
        }

        return default;
    }

    private static bool HasAdjacentTarget(BotContext context)
    {
        var position = context.Position;

        foreach (var monster in context.Entities.GetNearbyMonsters(null))
            if ((monster.HealthPercent > 0) && ((Math.Abs(monster.X - position.X) + Math.Abs(monster.Y - position.Y)) <= 1))
                return true;

        return false;
    }

    private static string? FirstReadySkill(BotContext context, List<string> names)
    {
        foreach (var name in names)
            if (context.Skills[name] is { IsReady: true })
                return name;

        return null;
    }

    private static void FaceAdjacentTarget(BotContext context)
    {
        var position = context.Position;

        foreach (var monster in context.Entities.GetNearbyMonsters(null))
        {
            if ((monster.HealthPercent <= 0) || ((Math.Abs(monster.X - position.X) + Math.Abs(monster.Y - position.Y)) > 1))
                continue;

            var facing = new Point(monster.X, monster.Y).DirectionalRelationTo(position);

            if (facing != Direction.Invalid)
                context.Server.SendTurn(new TurnArgs { Direction = facing });

            return;
        }
    }
}
