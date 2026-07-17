using Ouroboros.Client;

namespace Ouroboros.Networking;

/// <summary>
///     In-memory structured packet log plus an injection surface. It observes decrypted traffic in both
///     directions and can craft/inject raw packets toward the client or server. The WPF packet console
///     binds to this: subscribe to <see cref="EntryCaptured" /> (or poll <see cref="Snapshot" />) and call
///     <see cref="SendToServer" />/<see cref="SendToClient" /> to inject.
/// </summary>
public sealed class PacketConsole
{
    private const int MaxEntries = 5000;
    private readonly DarkAgesClient Client;
    private readonly Queue<PacketLogEntry> Entries = new();
    private readonly object Gate = new();
    private long Sequence;

    public PacketConsole(DarkAgesClient client) => Client = client;

    /// <summary>Raised (outside the lock) whenever a packet is captured.</summary>
    public event Action<PacketLogEntry>? EntryCaptured;

    /// <summary>When false, traffic is not recorded. Injection still works.</summary>
    public bool IsCapturing { get; set; } = true;

    /// <summary>A snapshot of the current log, oldest first.</summary>
    public IReadOnlyList<PacketLogEntry> Snapshot()
    {
        lock (Gate)
            return Entries.ToArray();
    }

    public void Clear()
    {
        lock (Gate)
            Entries.Clear();
    }

    /// <summary>Records an observed packet. <paramref name="frame" /> is the decrypted wire frame.</summary>
    public void Record(PacketDirection direction, ReadOnlySpan<byte> frame)
    {
        if (!IsCapturing || frame.Length <= 3)
            return;

        PacketLogEntry entry;

        lock (Gate)
        {
            entry = new PacketLogEntry(DateTime.Now, direction, frame.ToArray(), ++Sequence);
            Entries.Enqueue(entry);

            while (Entries.Count > MaxEntries)
                Entries.Dequeue();
        }

        EntryCaptured?.Invoke(entry);
    }

    /// <summary>Parses DSL text and injects the result toward the server (client → server).</summary>
    public void SendToServer(string craftText)
        => InjectToServer(PacketCraftParser.Parse(craftText, ResolveNpc, ResolveUser));

    /// <summary>Parses DSL text and injects the result toward the game client (server → client).</summary>
    public void SendToClient(string craftText)
        => InjectToClient(PacketCraftParser.Parse(craftText, ResolveNpc, ResolveUser));

    public void InjectToServer(CraftedPacket packet)
    {
        var frame = Client.BuildServerFrame(packet.OpCode, packet.Body);
        Record(PacketDirection.ClientToServer, frame);
        Client.InjectRawToServer(frame);
    }

    public void InjectToClient(CraftedPacket packet)
    {
        var frame = Client.BuildClientFrame(packet.OpCode, packet.Body);
        Record(PacketDirection.ServerToClient, frame);
        Client.InjectRawToClient(frame);
    }

    private uint? ResolveUser(string name)
        => Client.EntityManager
                 .GetNearbyAislings(aisling => string.Equals(aisling.Name, name, StringComparison.OrdinalIgnoreCase))
                 .Select(static aisling => (uint?)aisling.Id)
                 .FirstOrDefault();

    private uint? ResolveNpc(string name)
        => Client.EntityManager
                 .GetNearbyMerchants(merchant => string.Equals(merchant.Name, name, StringComparison.OrdinalIgnoreCase))
                 .Select(static merchant => (uint?)merchant.Id)
                 .FirstOrDefault();
}
