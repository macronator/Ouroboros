namespace Ouroboros.Networking;

/// <summary>
///     Relative priority used when draining queued packets. High-priority packets are always
///     forwarded ahead of normal traffic so the game's keep-alive contract (heartbeat and tick
///     synchronization) is never delayed behind bulk data such as map or entity streams.
/// </summary>
public enum NetworkPriority : byte
{
    Normal = 0,
    High = 1
}
