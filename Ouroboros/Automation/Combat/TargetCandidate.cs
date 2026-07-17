namespace Ouroboros.Automation.Combat;

/// <summary>A minimal projection of a creature used by <see cref="TargetSelector" /> (keeps it Model-free).</summary>
public readonly record struct TargetCandidate(uint Id, ushort Sprite, byte HealthPercent, int X, int Y);
