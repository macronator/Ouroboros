namespace Ouroboros.Model;

/// <summary>A point-in-time readout of session gains and their hourly rates.</summary>
public readonly record struct StatsSnapshot(
    TimeSpan Elapsed,
    long ExpGained,
    long GoldGained,
    long GamePointsGained,
    long ExpPerHour,
    long GoldPerHour,
    long GamePointsPerHour);

/// <summary>
///     Tracks session experience/gold/game-point gains and their per-hour rates. A baseline is captured on
///     the first sample that carries real exp/gold data; every later sample updates the current totals. Gold
///     can fall (spending), so its gain may be negative. <see cref="Reset" /> restarts the session baseline.
/// </summary>
public sealed class StatsTracker
{
    private readonly Lock Sync = new();
    private bool HasBaseline;
    private DateTime StartUtc;
    private uint BaseExp, BaseGold, BaseGamePoints;
    private uint CurrentExp, CurrentGold, CurrentGamePoints;

    /// <summary>Feeds the latest totals (call only when the packet actually carried exp/gold, i.e. ExpGold bit).</summary>
    public void Sample(uint totalExp, uint gold, uint gamePoints)
    {
        lock (Sync)
        {
            if (!HasBaseline)
            {
                HasBaseline = true;
                StartUtc = DateTime.UtcNow;
                BaseExp = totalExp;
                BaseGold = gold;
                BaseGamePoints = gamePoints;
            }

            CurrentExp = totalExp;
            CurrentGold = gold;
            CurrentGamePoints = gamePoints;
        }
    }

    /// <summary>Restarts the session baseline from the next sample.</summary>
    public void Reset()
    {
        lock (Sync)
            HasBaseline = false;
    }

    public StatsSnapshot Snapshot()
    {
        lock (Sync)
        {
            var elapsed = HasBaseline ? DateTime.UtcNow - StartUtc : TimeSpan.Zero;
            var hours = elapsed.TotalHours;

            var expGained = (long)CurrentExp - BaseExp;
            var goldGained = (long)CurrentGold - BaseGold;
            var gamePointsGained = (long)CurrentGamePoints - BaseGamePoints;

            return new StatsSnapshot(
                elapsed,
                expGained,
                goldGained,
                gamePointsGained,
                PerHour(expGained, hours),
                PerHour(goldGained, hours),
                PerHour(gamePointsGained, hours));
        }
    }

    private static long PerHour(long gained, double hours) => hours > 0 ? (long)(gained / hours) : 0;
}
