namespace Ouroboros.Automation.Walking;

/// <summary>Tuning for how a <see cref="WaypointRoute" /> is walked.</summary>
public sealed class WalkOptions
{
    /// <summary>A waypoint counts as reached when the character is within this many tiles of it.</summary>
    public int Proximity { get; set; }

    /// <summary>Delay between individual steps. ~420ms is normal Dark Ages walk speed; higher = slower.</summary>
    public TimeSpan StepDelay { get; set; } = TimeSpan.FromMilliseconds(420);

    /// <summary>When the last waypoint is reached, loop back to the first instead of stopping.</summary>
    public bool Loop { get; set; } = true;
}
