namespace Ouroboros.Model;

/// <summary>An active status-effect icon on the local player, with when it was last (re)applied.</summary>
public readonly record struct ActiveEffect(byte Icon, string? Name, DateTime AppliedUtc);

/// <summary>
///     Tracks the local player's active status-effect icons, driven by the Effect (0x3A) packet: a non-None
///     color upserts (refreshing the applied time), a None color removes. Icons resolve to names via a
///     configurable <see cref="Names" /> watch list. Icon 0 is a valid effect, so presence is tracked
///     explicitly by set membership rather than a sentinel value. Thread-safe (fed from the packet loop).
/// </summary>
public sealed class EffectTracker
{
    private readonly Dictionary<byte, DateTime> Active = new();
    private readonly Lock Sync = new();

    /// <summary>Optional icon→name map for well-known buffs/curses (server-specific; tune freely).</summary>
    public Dictionary<byte, string> Names { get; } = new();

    /// <summary>Applies an Effect packet: <paramref name="active" /> upserts (refresh), otherwise removes.</summary>
    public void Apply(byte icon, bool active)
    {
        lock (Sync)
        {
            if (active)
                Active[icon] = DateTime.UtcNow;
            else
                Active.Remove(icon);
        }
    }

    /// <summary>Clears all tracked effects (e.g. on map load / relog, before the bar is re-sent).</summary>
    public void Reset()
    {
        lock (Sync)
            Active.Clear();
    }

    /// <summary>Whether the given effect icon is currently active.</summary>
    public bool IsActive(byte icon)
    {
        lock (Sync)
            return Active.ContainsKey(icon);
    }

    /// <summary>Whether any active effect resolves (case-insensitively) to the given name.</summary>
    public bool IsActive(string name)
    {
        lock (Sync)
        {
            foreach (var icon in Active.Keys)
                if (Names.TryGetValue(icon, out var resolved) && string.Equals(resolved, name, StringComparison.OrdinalIgnoreCase))
                    return true;
        }

        return false;
    }

    /// <summary>Whether any of the given effect icons is currently active.</summary>
    public bool HasAny(IEnumerable<byte> icons)
    {
        lock (Sync)
        {
            foreach (var icon in icons)
                if (Active.ContainsKey(icon))
                    return true;
        }

        return false;
    }

    public int Count
    {
        get
        {
            lock (Sync)
                return Active.Count;
        }
    }

    /// <summary>A snapshot of the currently-active effects (icon + resolved name + applied time).</summary>
    public IReadOnlyList<ActiveEffect> Snapshot()
    {
        lock (Sync)
            return Active.Select(pair => new ActiveEffect(pair.Key, Names.GetValueOrDefault(pair.Key), pair.Value)).ToArray();
    }
}
