using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;
using Chaos.Packets.Abstractions.Definitions;

namespace Ouroboros.Networking;

/// <summary>
///     Classifies packets so the heartbeat (client 0x45 / server 0x3B) and tick synchronization
///     (client 0x75 / server 0x68) exchanges are elevated to <see cref="NetworkPriority.High" />,
///     regardless of which side queued them. Mirrors the priority-send-queue approach used by
///     ewrogers/Arbiter, where these opcodes are drained ahead of all other traffic to keep the
///     latency-sensitive keep-alive/timing packets from queuing behind bulk streams.
/// </summary>
public static class PacketPriority
{
    /// <summary>Resolves the priority of a packet the game client sent (destined for the server).</summary>
    public static NetworkPriority ForClientOpCode(byte opCode)
        => opCode is (byte)ClientOpCode.HeartBeat or (byte)ClientOpCode.SynchronizeTicks
            ? NetworkPriority.High
            : NetworkPriority.Normal;

    /// <summary>Resolves the priority of a packet the server sent (destined for the game client).</summary>
    public static NetworkPriority ForServerOpCode(byte opCode)
        => opCode is (byte)ServerOpCode.HeartBeatResponse or (byte)ServerOpCode.SynchronizeTicksResponse
            ? NetworkPriority.High
            : NetworkPriority.Normal;

    /// <summary>Resolves the priority of a packet we inject ourselves, by its serializable type.</summary>
    public static NetworkPriority ForSerializable(IPacketSerializable data)
        => data is HeartBeatArgs or SynchronizeTicksArgs or HeartBeatResponseArgs or SynchronizeTicksResponseArgs
            ? NetworkPriority.High
            : NetworkPriority.Normal;
}
