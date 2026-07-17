namespace Ouroboros.Model;

/// <summary>
///     Approximate buff/effect durations keyed by Gaelic spell name, used to avoid recasting a buff while
///     it is still active. Values are best-effort and meant to be tuned against the live server; unknown
///     spells fall back to <see cref="Default" />. Offensive spells are intentionally absent (duration
///     zero → freely recastable).
/// </summary>
public static class SpellDurations
{
    /// <summary>Fallback used for spells not in the table.</summary>
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(30);

    private static readonly Dictionary<string, TimeSpan> Durations = new(StringComparer.OrdinalIgnoreCase)
    {
        //defensive skins / dions (~15s)
        ["dion"] = TimeSpan.FromSeconds(15),
        ["mor dion"] = TimeSpan.FromSeconds(15),
        ["iron skin"] = TimeSpan.FromSeconds(15),
        ["stone skin"] = TimeSpan.FromSeconds(15),
        ["draco stance"] = TimeSpan.FromSeconds(15),
        ["wings of protection"] = TimeSpan.FromSeconds(13),
        ["asgall faileas"] = TimeSpan.FromSeconds(13),
        ["perfect defense"] = TimeSpan.FromSeconds(15),

        //nature/aegis buffs
        ["beag fas nadur"] = TimeSpan.FromSeconds(225),
        ["fas nadur"] = TimeSpan.FromSeconds(225),
        ["mor fas nadur"] = TimeSpan.FromSeconds(225),
        ["ard fas nadur"] = TimeSpan.FromSeconds(450),
        ["armachd"] = TimeSpan.FromSeconds(225),
        ["naomh aite"] = TimeSpan.FromSeconds(60),
        ["mor naomh aite"] = TimeSpan.FromSeconds(60),
        ["ard naomh aite"] = TimeSpan.FromSeconds(60),

        //curses / seals
        ["cradh"] = TimeSpan.FromSeconds(180),
        ["mor cradh"] = TimeSpan.FromSeconds(180),
        ["ard cradh"] = TimeSpan.FromSeconds(180),
        ["dark seal"] = TimeSpan.FromSeconds(155),
        ["darker seal"] = TimeSpan.FromSeconds(155),
        ["demise"] = TimeSpan.FromSeconds(155)
    };

    public static TimeSpan For(string spellName) => Durations.GetValueOrDefault(spellName, Default);
}
