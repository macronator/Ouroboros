using System.Collections.Frozen;

namespace Ouroboros.Automation.Combat;

/// <summary>
///     The casting-safety rules the <see cref="TargetSelector" /> applies, resolved for the current map.
///     Sprite sets are injected (rather than read from CONSTANTS) so the selector stays pure and testable.
/// </summary>
public sealed class TargetRules
{
    public int MaxRange { get; init; } = 12;

    /// <summary>Sprites that are effectively invisible / should never be targeted.</summary>
    public IReadOnlySet<ushort> InvisibleSprites { get; init; } = FrozenSet<ushort>.Empty;

    /// <summary>Sprites that are undesirable to attack anywhere.</summary>
    public IReadOnlySet<ushort> UndesirableSprites { get; init; } = FrozenSet<ushort>.Empty;

    /// <summary>If set, ONLY these sprites are valid targets on the current map.</summary>
    public IReadOnlySet<ushort>? Whitelist { get; init; }

    /// <summary>If set, these sprites are excluded on the current map.</summary>
    public IReadOnlySet<ushort>? Blacklist { get; init; }
}
