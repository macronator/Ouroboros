using Chaos.Common.Definitions;
using Chaos.Networking.Entities.Server;

namespace Ouroboros.Model;

/// <summary>
///     The controlled character's vitals, updated from the partial <c>Attributes</c> (0x08) packet. HP/MP
///     are only refreshed when the packet's <see cref="StatUpdateType.Vitality" /> bit is set, and level
///     only on <see cref="StatUpdateType.Primary" /> — so a partial update never zeroes fields it omitted.
/// </summary>
public sealed class SelfState
{
    public byte Level { get; private set; }
    public uint CurrentHp { get; private set; }
    public uint MaximumHp { get; private set; }
    public uint CurrentMp { get; private set; }
    public uint MaximumMp { get; private set; }

    /// <summary>Current HP as a 0-100 percent (100 when max is unknown).</summary>
    public int HealthPercent => MaximumHp == 0 ? 100 : (int)(CurrentHp * 100 / MaximumHp);

    /// <summary>Current MP as a 0-100 percent (100 when max is unknown).</summary>
    public int ManaPercent => MaximumMp == 0 ? 100 : (int)(CurrentMp * 100 / MaximumMp);

    public void Update(AttributesArgs args)
    {
        if (args.StatUpdateType.HasFlag(StatUpdateType.Vitality))
        {
            CurrentHp = args.CurrentHp;
            CurrentMp = args.CurrentMp;

            //current-only vitality updates carry 0 for max — keep the last known max instead of zeroing it
            if (args.MaximumHp > 0)
                MaximumHp = args.MaximumHp;

            if (args.MaximumMp > 0)
                MaximumMp = args.MaximumMp;
        }

        if (args.StatUpdateType.HasFlag(StatUpdateType.Primary))
            Level = args.Level;
    }
}
