namespace Ouroboros.Automation.Combat;

/// <summary>
///     Chooses which creature to attack from a set of candidates, applying casting-safety rules and
///     picking the nearest eligible target (ties broken by lowest serial for determinism). Pure logic —
///     no game/Model/CONSTANTS dependencies — so it is fully unit-testable.
/// </summary>
public static class TargetSelector
{
    public static bool IsEligible(TargetCandidate candidate, TargetRules rules)
    {
        if (candidate.HealthPercent == 0)
            return false;

        if (rules.InvisibleSprites.Contains(candidate.Sprite))
            return false;

        if (rules.UndesirableSprites.Contains(candidate.Sprite))
            return false;

        //a whitelist, when present, is exclusive: only listed sprites are valid on this map
        if (rules.Whitelist is not null && !rules.Whitelist.Contains(candidate.Sprite))
            return false;

        if (rules.Blacklist is not null && rules.Blacklist.Contains(candidate.Sprite))
            return false;

        return true;
    }

    public static TargetCandidate? Select(IReadOnlyList<TargetCandidate> candidates, int originX, int originY, TargetRules rules)
    {
        TargetCandidate? best = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in candidates)
        {
            if (!IsEligible(candidate, rules))
                continue;

            var distance = Math.Abs(candidate.X - originX) + Math.Abs(candidate.Y - originY);

            if (distance > rules.MaxRange)
                continue;

            if (distance < bestDistance || (distance == bestDistance && best is { } current && candidate.Id < current.Id))
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }
}
