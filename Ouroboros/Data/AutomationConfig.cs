namespace Ouroboros.Data;

/// <summary>
///     Persisted automation settings: the thresholds and item lists that are tedious to retype each session.
///     Deliberately does NOT persist which routines are enabled — the bot never auto-starts combat/loot on
///     login; the operator re-arms routines each session, but their tuning survives restarts.
/// </summary>
public sealed class AutomationConfig
{
    public int HealThreshold { get; set; } = 60;
    public int PotionHpThreshold { get; set; } = 50;
    public int PotionMpThreshold { get; set; } = 30;
    public List<string> HealItems { get; set; } = [];
    public List<string> ManaItems { get; set; } = [];
    public List<string> TrashItems { get; set; } = [];
    public List<string> Buffs { get; set; } = [];

    //Crasher combo config — names/threshold persist, but NOT its enabled flag (a self-damage routine
    //must never auto-arm on login; the operator re-enables it each session after a deliberate check).
    public List<string> CrasherExecuteSkills { get; set; } = [];
    public List<string> CrasherPreBuffSkills { get; set; } = [];
    public string? CrasherSelfDamageSkill { get; set; }
    public string? CrasherSelfDamageItem { get; set; }
    public int CrasherHpThreshold { get; set; } = 1;
}
