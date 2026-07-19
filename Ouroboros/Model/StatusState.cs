using Ouroboros.Defintions;

namespace Ouroboros.Model;

/// <summary>
///     Tracks which <see cref="ClientStatus" /> flags are active on the local player. Each bit may carry an
///     expected expiry so a dropped removal signal never leaves it stuck — queries purge expired bits first
///     (sweep-on-read). Onset sets a bit (with an optional duration), expiry/clear removes it, and
///     <see cref="Reset" /> wipes all on relog/map. Exposes convenience gates for the automation loops.
/// </summary>
public sealed class StatusState
{
    private const ClientStatus Incapacitating =
        ClientStatus.Dall
        | ClientStatus.BeagSuain
        | ClientStatus.Suain
        | ClientStatus.Sleep
        | ClientStatus.Halt
        | ClientStatus.Pause
        | ClientStatus.Coma
        | ClientStatus.Skulled;

    private readonly Lock Sync = new();
    private readonly Dictionary<ClientStatus, DateTime> ExpiresAt = new();
    private ClientStatus Active;

    /// <summary>Marks a status active. A positive duration arms the safety sweep to auto-clear it later.</summary>
    public void Set(ClientStatus status, TimeSpan? duration = null)
    {
        if (status == ClientStatus.None)
            return;

        lock (Sync)
        {
            Active |= status;

            if (duration is { } span && span > TimeSpan.Zero)
                ExpiresAt[status] = DateTime.UtcNow + span;
            else
                ExpiresAt.Remove(status);
        }
    }

    /// <summary>Clears a status.</summary>
    public void Clear(ClientStatus status)
    {
        lock (Sync)
        {
            Active &= ~status;
            ExpiresAt.Remove(status);
        }
    }

    /// <summary>Sets or clears a status from a boolean signal (e.g. the packet blind flag).</summary>
    public void SetOrClear(ClientStatus status, bool on, TimeSpan? duration = null)
    {
        if (on)
            Set(status, duration);
        else
            Clear(status);
    }

    /// <summary>Clears every tracked status (relog / map reset).</summary>
    public void Reset()
    {
        lock (Sync)
        {
            Active = ClientStatus.None;
            ExpiresAt.Clear();
        }
    }

    /// <summary>Whether all bits of <paramref name="status" /> are active.</summary>
    public bool Has(ClientStatus status)
    {
        if (status == ClientStatus.None)
            return false;

        lock (Sync)
        {
            PurgeExpired();

            return (Active & status) == status;
        }
    }

    /// <summary>Whether any bit of <paramref name="mask" /> is active.</summary>
    public bool HasAny(ClientStatus mask)
    {
        lock (Sync)
        {
            PurgeExpired();

            return (Active & mask) != ClientStatus.None;
        }
    }

    /// <summary>The full active flag set (after purging expired bits).</summary>
    public ClientStatus Current
    {
        get
        {
            lock (Sync)
            {
                PurgeExpired();

                return Active;
            }
        }
    }

    /// <summary>True when the character is frozen/asleep/held/skulled and cannot act.</summary>
    public bool IsIncapacitated => HasAny(Incapacitating);

    /// <summary>Whether the character can currently move.</summary>
    public bool CanWalk => !HasAny(Incapacitating | ClientStatus.Stuck);

    /// <summary>Whether the character can currently cast.</summary>
    public bool CanCast => !HasAny(Incapacitating);

    //clears any status whose expected expiry has passed; caller must hold the lock
    private void PurgeExpired()
    {
        if (ExpiresAt.Count == 0)
            return;

        var now = DateTime.UtcNow;
        List<ClientStatus>? expired = null;

        foreach (var pair in ExpiresAt)
            if (pair.Value <= now)
                (expired ??= []).Add(pair.Key);

        if (expired is null)
            return;

        foreach (var status in expired)
        {
            Active &= ~status;
            ExpiresAt.Remove(status);
        }
    }
}
