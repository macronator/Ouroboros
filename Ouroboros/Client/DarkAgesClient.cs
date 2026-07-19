using System.Collections.Concurrent;
using System.Net;
using Chaos.Cryptography.Abstractions;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets;
using Chaos.Packets.Abstractions;
using Ouroboros.Abstractions;
using Ouroboros.Automation;
using Ouroboros.Automation.Events;
using Ouroboros.Data;
using Ouroboros.Defintions;
using Ouroboros.Data.Meta;
using Ouroboros.Memory;
using Ouroboros.Model;
using Ouroboros.Networking;
using Ouroboros.Services.Managers;
using Ouroboros.Services.Pathfinding;
using Ouroboros.Utilities;
using Ouroboros.ViewModel;

namespace Ouroboros.Client;

public sealed class DarkAgesClient : IEquatable<DarkAgesClient>
{
    public string Guid { get; } = System.Guid.NewGuid().ToString();
    public uint? Id { get; set; }
    public DaWindow? DaWindow { get; set; }
    public event EventHandler? OnDisconnect;
    private readonly ConcurrentQueue<byte[]> ClientReceiveQueue;
    private readonly ConcurrentQueue<byte[]> ClientReceivePriorityQueue;
    private readonly ConcurrentQueue<byte[]> ServerReceiveQueue;
    private readonly ConcurrentQueue<byte[]> ServerReceivePriorityQueue;
    private readonly ConcurrentQueue<IPacketSerializable> ClientSendQueue;
    private readonly ConcurrentQueue<IPacketSerializable> ClientSendPriorityQueue;
    private readonly ConcurrentQueue<IPacketSerializable> ServerSendQueue;
    private readonly ConcurrentQueue<IPacketSerializable> ServerSendPriorityQueue;
    private readonly ConcurrentQueue<byte[]> ClientRawSendQueue;
    private readonly ConcurrentQueue<byte[]> ServerRawSendQueue;
    private readonly ProxyServer ProxyServer;
    private readonly ProxyClient ProxyClient;
    private readonly IPacketSerializer PacketSerializer;
    private readonly PacketHandler?[] ClientPacketHandlers;
    private readonly PacketHandler?[] ServerPacketHandlers;
    private readonly AsyncSignal Signal;
    private int NotifiedDisconnect;
    private DateTime LastWalkUtc;
    private (short SrcMap, Point SrcPoint, short DstMap)? PendingWarp;
    public GeneralOptions GeneralOptions { get; }
    public ClientManager Manager { get; }
    public RedirectManager RedirectManager { get; }
    // ReSharper disable once NotAccessedField.Local
    private Task? ProcessLoopTask;
    public ClientActions ClientActions { get; }
    public ServerActions ServerActions { get; }
    public Aisling? Aisling { get; set; }
    public Direction ClientDirection { get; set; }
    public Point ClientPoint { get; set; }
    public Direction ServerDirection { get; set; }
    public Point ServerPoint { get; set; }
    public EntityManager EntityManager { get; }
    public Pathfinder? Pathfinder { get; set; }
    public Routefinder Routefinder { get; set; }
    public BotContext Bot { get; }
    public PacketConsole Console { get; }
    public SkillBook SkillBook { get; }
    public SpellBook SpellBook { get; }
    public SelfState Vitals { get; }
    public EffectTracker Effects { get; }
    public StatusState Status { get; }
    public Inventory Inventory { get; }

    /// <summary>The NPC dialog currently open on the client, or null. Updated from the DisplayDialog packet.</summary>
    public NpcDialog? Dialog { get; set; }

    /// <summary>The NPC menu (pursuit list) currently open, or null. Updated from the DisplayMenu packet.</summary>
    public NpcMenu? Menu { get; set; }
    public IStorage<WorldMeta> WorldStorage { get; }
    private readonly IStorage<AutomationConfig> AutomationStorage;
    public Dictionary<string, object> Temp { get; set; }

    public delegate HandlerResult PacketHandler(in Packet packet, out IPacketSerializable serialized);

    public DarkAgesClient(
        ProxyClient proxyClient,
        ProxyServer proxyServer,
        IPacketSerializer packetSerializer,
        RedirectManager redirectManager,
        ClientManager manager,
        Routefinder routefinder,
        IReadOnlyStorage<GeneralOptions> generalOptions,
        IStorage<WorldMeta> worldStorage,
        IStorage<AutomationConfig> automationConfig)
    {
        GeneralOptions = generalOptions.Value;
        WorldStorage = worldStorage;
        AutomationStorage = automationConfig;
        ProxyClient = proxyClient;
        ProxyServer = proxyServer;
        Routefinder = routefinder;
        PacketSerializer = packetSerializer;
        RedirectManager = redirectManager;
        Manager = manager;
        ClientActions = new ClientActions(this);
        ServerActions = new ServerActions(this);
        ClientReceiveQueue = new ConcurrentQueue<byte[]>();
        ClientReceivePriorityQueue = new ConcurrentQueue<byte[]>();
        ServerReceiveQueue = new ConcurrentQueue<byte[]>();
        ServerReceivePriorityQueue = new ConcurrentQueue<byte[]>();
        ClientSendQueue = new ConcurrentQueue<IPacketSerializable>();
        ClientSendPriorityQueue = new ConcurrentQueue<IPacketSerializable>();
        ServerSendQueue = new ConcurrentQueue<IPacketSerializable>();
        ServerSendPriorityQueue = new ConcurrentQueue<IPacketSerializable>();
        ClientRawSendQueue = new ConcurrentQueue<byte[]>();
        ServerRawSendQueue = new ConcurrentQueue<byte[]>();
        ProxyClient.OnReceive += EnqueueClientReceive;
        ProxyServer.OnReceive += EnqueueServerReceive;
        ClientPacketHandlers = new ClientHandlers(this, packetSerializer).GetIndexedHandlers();
        ServerPacketHandlers = new ServerHandlers(this, packetSerializer).GetIndexedHandlers();
        Signal = new AsyncSignal();
        EntityManager = new EntityManager(this);
        Bot = new BotContext(this);
        Bot.ApplyConfig(AutomationStorage.Value);
        Console = new PacketConsole(this);
        SkillBook = new SkillBook();
        SpellBook = new SpellBook();
        Vitals = new SelfState();
        Vitals.MailArrived += () => Bot.Reply("[Ouroboros] You have unread mail.");
        Effects = new EffectTracker();
        Status = new StatusState();

        //drive the status bitmask from the reliable signals we have: the packet blind flag and curse chat
        Vitals.BlindChanged += blind => Status.SetOrClear(ClientStatus.Dall, blind);
        Bot.Chat.Received += serverEvent =>
        {
            if (serverEvent.Kind == ServerEventKind.CurseApplied)
                Status.Set(ClientStatus.Cradh, SpellDurations.For("cradh"));
        };
        Inventory = new Inventory();
        Temp = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (GeneralOptions.LogRawPackets)
        {
            ProxyClient.LogRawPackets = true;
            ProxyServer.LogRawPackets = true;
        }

        SetupDisconnectEvent();
    }

    private void SetupDisconnectEvent()
    {
        ProxyClient.OnDisconnected += (_, _) =>
        {
            ProxyServer.Disconnect();
            NotifyDisconnected();
        };
        ProxyServer.OnDisconnected += (_, _) =>
        {
            ProxyClient.Disconnect();
            NotifyDisconnected();
        };

        return;

        void NotifyDisconnected()
        {
            if (Interlocked.CompareExchange(ref NotifiedDisconnect, 1, 0) != 0)
                return;

            //tear down any running automation loops before signalling the disconnect
            _ = Bot.Engine.StopAsync();
            OnDisconnect?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ProcessLoop()
    {
        while (ProxyClient.Connected)
        {
            await Signal.WaitAsync();

            //heartbeat + tick synchronization always jump ahead of bulk traffic in both directions
            ProcessPacketsFromClient(ClientReceivePriorityQueue);
            ProcessPacketsFromServer(ServerReceivePriorityQueue);
            ProcessPacketsToClient(ClientSendPriorityQueue);
            ProcessPacketsToServer(ServerSendPriorityQueue);

            //everything else
            ProcessPacketsFromClient(ClientReceiveQueue);
            ProcessPacketsFromServer(ServerReceiveQueue);
            ProcessPacketsToClient(ClientSendQueue);
            ProcessPacketsToServer(ServerSendQueue);

            //raw packets crafted/injected via the packet console
            ProcessRawToClient(ClientRawSendQueue);
            ProcessRawToServer(ServerRawSendQueue);
        }
    }
    
    public void SetServerSequence(byte sequence) => ProxyServer.Sequence = sequence;

    public void SetCrypto(ICrypto crypto)
    {
        ProxyClient.Crypto = crypto;
        ProxyServer.Crypto = crypto;
    }

    /// <summary>
    ///     Adds a warp edge (source map + tile → destination map + tile) to the world graph, persists it to
    ///     <c>WorldMeta.json</c>, and rebuilds the route finder so cross-map routing picks it up.
    /// </summary>
    public void AddWarp(short sourceMapId, int sourceX, int sourceY, short destMapId, int destX, int destY)
    {
        var world = WorldStorage.Value;
        world.Maps.TryGetValue(sourceMapId, out var mapMeta);

        //skip if this exact warp is already mapped (auto-learning re-traverses the same warps)
        if (mapMeta is not null
            && mapMeta.Warps.Any(warp => (warp.SourcePoint.X == sourceX)
                                         && (warp.SourcePoint.Y == sourceY)
                                         && (warp.Destination.MapId == destMapId)))
            return;

        if (mapMeta is null)
        {
            mapMeta = new MapMeta
            {
                MapId = sourceMapId,
                Name = Aisling?.Map is { } current && current.Id == sourceMapId ? current.Name : $"Map {sourceMapId}"
            };
            world.Maps[sourceMapId] = mapMeta;
        }

        var warps = mapMeta.Warps as List<WarpMeta> ?? mapMeta.Warps.ToList();
        warps.Add(new WarpMeta
        {
            SourcePoint = new Point(sourceX, sourceY),
            Destination = new IdLocation(destMapId, destX, destY)
        });
        mapMeta.Warps = warps;

        WorldStorage.Save();
        Routefinder.Rebuild();
    }

    /// <summary>Records that we just took a walk step (used to distinguish step-on warps from teleports).</summary>
    public void MarkWalked() => LastWalkUtc = DateTime.UtcNow;

    /// <summary>
    ///     Called on a map change: remembers the tile we left from as a candidate warp source, but only if
    ///     we walked immediately beforehand (so NPC/world-map teleports don't create un-walkable edges).
    /// </summary>
    public void CaptureWarpSource(short sourceMapId, Point sourcePoint, short destMapId)
    {
        if ((DateTime.UtcNow - LastWalkUtc) <= TimeSpan.FromSeconds(2))
            PendingWarp = (sourceMapId, sourcePoint, destMapId);
    }

    /// <summary>Called once we know the arrival tile on the new map: completes and learns the pending warp.</summary>
    public void CompleteWarp(int destX, int destY)
    {
        if (PendingWarp is not { } pending || Aisling?.Map?.Id != pending.DstMap)
            return;

        PendingWarp = null;
        AddWarp(pending.SrcMap, pending.SrcPoint.X, pending.SrcPoint.Y, pending.DstMap, destX, destY);
    }

    /// <summary>Removes any warp whose source tile matches, persists, and rebuilds. Returns whether one was removed.</summary>
    public bool RemoveWarp(short mapId, int x, int y)
    {
        var world = WorldStorage.Value;

        if (!world.Maps.TryGetValue(mapId, out var mapMeta))
            return false;

        var warps = mapMeta.Warps as List<WarpMeta> ?? mapMeta.Warps.ToList();
        var removed = warps.RemoveAll(warp => (warp.SourcePoint.X == x) && (warp.SourcePoint.Y == y)) > 0;
        mapMeta.Warps = warps;

        if (!removed)
            return false;

        WorldStorage.Save();
        Routefinder.Rebuild();

        return true;
    }

    /// <summary>Captures the current automation settings and persists them to disk.</summary>
    public void SaveAutomationConfig()
    {
        Bot.CaptureConfigInto(AutomationStorage.Value);
        AutomationStorage.Save();
    }

    public void Connect(IPEndPoint? serverEndPoint = null)
    {
        if (serverEndPoint is null)
        {
            ProxyClient.BeginReceive();
            ProcessLoopTask = ProcessLoop();

            ClientActions.SendAcceptConnection(
                new AcceptConnectionArgs
                {
                    Message = "CONNECTED SERVER"
                });
        } else
        {
            //start receiving from the client
            ProxyServer.Connect(serverEndPoint);

            ProxyServer.BeginReceive();
        }
    }

    private void ProcessPacketsFromClient(ConcurrentQueue<byte[]> queue)
    {
        while (queue.TryDequeue(out var buffer))
        {
            var span = buffer.AsSpan();
            var opCode = span[3];
            var packet = new Packet(ref span, ProxyServer.IsEncrypted(opCode));
            
            var handler = ClientPacketHandlers[packet.OpCode];

            //if there's no handler, just act as a pass through for the packet
            if (handler is null)
            {
                ProxyServer.Send(ref packet);

                continue;
            }

            HandlerResult ret;
            IPacketSerializable serialized = null!;

            try
            {
                ret = handler(packet, out serialized);
            }
            catch (Exception ex)
            {
                //a throwing handler must never tear down the relay — pass the original packet through
                System.Diagnostics.Debug.WriteLine($"[Ouroboros] client handler for opcode 0x{opCode:X2} threw, passing original: {ex}");
                ProxyServer.Send(ref packet);

                continue;
            }

            //if the handler wants to cancel the packet, continue
            if (ret.Cancel)
                continue;

            //if the packet is marked as a passthrough, send the original
            if (ret.UseOriginal)
            {
                ProxyServer.Send(ref packet);

                continue;
            }

            //re-serialize the converted type and send it
            ProxyServer.Send(serialized);
        }
    }
    
    private void ProcessPacketsFromServer(ConcurrentQueue<byte[]> queue)
    {
        while (queue.TryDequeue(out var buffer))
        {
            var span = buffer.AsSpan();
            var opCode = span[3];
            var packet = new Packet(ref span, ProxyClient.IsEncrypted(opCode));
            var handler = ServerPacketHandlers[packet.OpCode];

            //if there's no handler, just act as a pass through for the packet
            if (handler is null)
            {
                ProxyClient.Send(ref packet);

                continue;
            }

            HandlerResult ret;
            IPacketSerializable serialized = null!;

            try
            {
                ret = handler(packet, out serialized);
            }
            catch (Exception ex)
            {
                //a throwing handler must never tear down the relay — pass the original packet through
                System.Diagnostics.Debug.WriteLine($"[Ouroboros] server handler for opcode 0x{opCode:X2} threw, passing original: {ex}");
                ProxyClient.Send(ref packet);

                continue;
            }

            //if the handler wants to cancel the packet, continue
            if (ret.Cancel)
                continue;

            //if the packet is marked as a passthrough, send the original
            if (ret.UseOriginal)
            {
                ProxyClient.Send(ref packet);

                continue;
            }

            //re-serialize the converted type and send it
            ProxyClient.Send(serialized);
        }
    }
    
    private void ProcessPacketsToClient(ConcurrentQueue<IPacketSerializable> queue)
    {
        while (queue.TryDequeue(out var data))
        {
            var packet = PacketSerializer.Serialize(data);
            ProxyClient.Send(ref packet);
        }
    }

    private void ProcessPacketsToServer(ConcurrentQueue<IPacketSerializable> queue)
    {
        while (queue.TryDequeue(out var data))
        {
            var packet = PacketSerializer.Serialize(data);
            ProxyServer.Send(ref packet);
        }
    }

    private void ProcessRawToClient(ConcurrentQueue<byte[]> queue)
    {
        while (queue.TryDequeue(out var frame))
        {
            var span = frame.AsSpan();
            var opCode = span[3];
            var packet = new Packet(ref span, ProxyClient.IsEncrypted(opCode));
            ProxyClient.Send(ref packet);
        }
    }

    private void ProcessRawToServer(ConcurrentQueue<byte[]> queue)
    {
        while (queue.TryDequeue(out var frame))
        {
            var span = frame.AsSpan();
            var opCode = span[3];
            var packet = new Packet(ref span, ProxyServer.IsEncrypted(opCode));
            ProxyServer.Send(ref packet);
        }
    }

    /// <summary>Builds a server-bound wire frame (client → server), encrypting per the opcode's tier.</summary>
    public byte[] BuildServerFrame(byte opCode, byte[] body)
        => PacketFrame.Build(opCode, body, ProxyServer.IsEncrypted(opCode), ProxyServer.Sequence);

    /// <summary>Builds a client-bound wire frame (server → client), encrypting per the opcode's tier.</summary>
    public byte[] BuildClientFrame(byte opCode, byte[] body)
        => PacketFrame.Build(opCode, body, ProxyClient.IsEncrypted(opCode), ProxyClient.Sequence);

    /// <summary>Queues a fully-formed frame to be sent to the real server on the process loop.</summary>
    public void InjectRawToServer(byte[] frame)
    {
        ServerRawSendQueue.Enqueue(frame);
        Signal.Pulse();
    }

    /// <summary>Queues a fully-formed frame to be sent to the game client on the process loop.</summary>
    public void InjectRawToClient(byte[] frame)
    {
        ClientRawSendQueue.Enqueue(frame);
        Signal.Pulse();
    }

    public void ClientEnqueue(IPacketSerializable data)
    {
        if (PacketPriority.ForSerializable(data) == NetworkPriority.High)
            ClientSendPriorityQueue.Enqueue(data);
        else
            ClientSendQueue.Enqueue(data);

        Signal.Pulse();
    }

    public void ServerEnqueue(IPacketSerializable data)
    {
        if (PacketPriority.ForSerializable(data) == NetworkPriority.High)
            ServerSendPriorityQueue.Enqueue(data);
        else
            ServerSendQueue.Enqueue(data);

        Signal.Pulse();
    }

    private void EnqueueClientReceive(byte[] buffer)
    {
        Console.Record(PacketDirection.ClientToServer, buffer);

        //buffer[3] is the opcode; heartbeat/tick are routed to their own priority lane
        if (PacketPriority.ForClientOpCode(buffer[3]) == NetworkPriority.High)
            ClientReceivePriorityQueue.Enqueue(buffer);
        else
            ClientReceiveQueue.Enqueue(buffer);

        Signal.Pulse();
    }

    private void EnqueueServerReceive(byte[] buffer)
    {
        Console.Record(PacketDirection.ServerToClient, buffer);

        //buffer[3] is the opcode; heartbeat/tick are routed to their own priority lane
        if (PacketPriority.ForServerOpCode(buffer[3]) == NetworkPriority.High)
            ServerReceivePriorityQueue.Enqueue(buffer);
        else
            ServerReceiveQueue.Enqueue(buffer);

        Signal.Pulse();
    }

    #region IEquatable
    /// <inheritdoc />
    public bool Equals(DarkAgesClient? other)
    {
        if (ReferenceEquals(null, other))
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Guid == other.Guid;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is DarkAgesClient other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Guid.GetHashCode();

    public static bool operator ==(DarkAgesClient? left, DarkAgesClient? right) => Equals(left, right);
    public static bool operator !=(DarkAgesClient? left, DarkAgesClient? right) => !Equals(left, right);
    #endregion
}