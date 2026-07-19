using Chaos.Common.Definitions;
using Chaos.Networking.Entities.Server;

namespace Ouroboros.Model;

/// <summary>
///     The controlled character's live stats, rebuilt from the partial <c>Attributes</c> (0x08) packet. Each
///     sub-block is applied only when its <see cref="StatUpdateType" /> bit is present, so a partial update
///     never zeroes fields it omitted (weight is additionally guarded by a non-zero max).
/// </summary>
public sealed class SelfState
{
    private bool MailKnown;

    public byte Level { get; private set; }
    public byte Ability { get; private set; }
    public uint CurrentHp { get; private set; }
    public uint MaximumHp { get; private set; }
    public uint CurrentMp { get; private set; }
    public uint MaximumMp { get; private set; }

    //primary stats
    public byte Str { get; private set; }
    public byte Int { get; private set; }
    public byte Wis { get; private set; }
    public byte Con { get; private set; }
    public byte Dex { get; private set; }
    public byte UnspentPoints { get; private set; }
    public bool HasUnspentPoints { get; private set; }

    //secondary/combat stats
    public sbyte Ac { get; private set; }
    public byte Dmg { get; private set; }
    public byte Hit { get; private set; }
    public byte MagicResistance { get; private set; }
    public bool Blind { get; private set; }
    public Element OffenseElement { get; private set; }
    public Element DefenseElement { get; private set; }

    //carry weight
    public short CurrentWeight { get; private set; }
    public short MaxWeight { get; private set; }

    //experience / currency
    public uint TotalExp { get; private set; }
    public uint ToNextLevel { get; private set; }
    public uint TotalAbility { get; private set; }
    public uint ToNextAbility { get; private set; }
    public uint GamePoints { get; private set; }
    public uint Gold { get; private set; }

    //flags
    public bool HasUnreadMail { get; private set; }
    public bool IsAdmin { get; private set; }

    /// <summary>Raised on the false→true edge of unread mail — fire an in-client alert (not email).</summary>
    public event Action? MailArrived;

    /// <summary>Raised when the blind flag changes (drives the Dall status bit).</summary>
    public event Action<bool>? BlindChanged;

    /// <summary>Current HP as a 0-100 percent (100 when max is unknown).</summary>
    public int HealthPercent => MaximumHp == 0 ? 100 : (int)(CurrentHp * 100 / MaximumHp);

    /// <summary>Current MP as a 0-100 percent (100 when max is unknown).</summary>
    public int ManaPercent => MaximumMp == 0 ? 100 : (int)(CurrentMp * 100 / MaximumMp);

    /// <summary>Carry weight as a 0-100 percent (0 when max is unknown).</summary>
    public int WeightPercent => MaxWeight <= 0 ? 0 : Math.Clamp(CurrentWeight * 100 / MaxWeight, 0, 100);

    /// <summary>True when carrying at or over the weight cap.</summary>
    public bool IsOverweight => MaxWeight > 0 && CurrentWeight >= MaxWeight;

    public void Update(AttributesArgs args)
    {
        var type = args.StatUpdateType;

        if (type.HasFlag(StatUpdateType.Vitality))
        {
            CurrentHp = args.CurrentHp;
            CurrentMp = args.CurrentMp;

            //current-only vitality updates carry 0 for max — keep the last known max instead of zeroing it
            if (args.MaximumHp > 0)
                MaximumHp = args.MaximumHp;

            if (args.MaximumMp > 0)
                MaximumMp = args.MaximumMp;
        }

        if (type.HasFlag(StatUpdateType.Primary))
        {
            Level = args.Level;
            Ability = args.Ability;
            Str = args.Str;
            Int = args.Int;
            Wis = args.Wis;
            Con = args.Con;
            Dex = args.Dex;
            UnspentPoints = args.UnspentPoints;
            HasUnspentPoints = args.HasUnspentPoints;
        }

        if (type.HasFlag(StatUpdateType.Secondary))
        {
            var wasBlind = Blind;

            Ac = args.Ac;
            Dmg = args.Dmg;
            Hit = args.Hit;
            MagicResistance = args.MagicResistance;
            Blind = args.Blind;
            OffenseElement = args.OffenseElement;
            DefenseElement = args.DefenseElement;

            if (Blind != wasBlind)
                BlindChanged?.Invoke(Blind);
        }

        if (type.HasFlag(StatUpdateType.ExpGold))
        {
            TotalExp = args.TotalExp;
            ToNextLevel = args.ToNextLevel;
            TotalAbility = args.TotalAbility;
            ToNextAbility = args.ToNextAbility;
            GamePoints = args.GamePoints;
            Gold = args.Gold;
        }

        //weight travels with a non-zero max; guard on it so we never clobber with a partial 0
        if (args.MaxWeight > 0)
        {
            MaxWeight = args.MaxWeight;
            CurrentWeight = args.CurrentWeight;
        }

        if (type.HasFlag(StatUpdateType.GameMasterA) || type.HasFlag(StatUpdateType.GameMasterB))
            IsAdmin = args.IsAdmin;

        //mail flag arrives on the dedicated bit and rides along with secondary updates
        if (type.HasFlag(StatUpdateType.UnreadMail) || type.HasFlag(StatUpdateType.Secondary))
        {
            var had = HasUnreadMail;
            HasUnreadMail = args.HasUnreadMail;

            //only alert on a real false→true transition once we've seen at least one mail-bearing packet
            if (MailKnown && !had && HasUnreadMail)
                MailArrived?.Invoke();

            MailKnown = true;
        }
    }
}
