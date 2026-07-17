namespace Ouroboros.Networking;

/// <summary>Which way a packet is travelling through the proxy.</summary>
public enum PacketDirection
{
    /// <summary>Sent by the game client, headed for the server.</summary>
    ClientToServer,

    /// <summary>Sent by the server, headed for the game client.</summary>
    ServerToClient
}
