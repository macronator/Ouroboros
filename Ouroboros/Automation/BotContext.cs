using Chaos.Common.Definitions;
using Chaos.Geometry;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Ouroboros.Automation.Combat;
using Ouroboros.Automation.Events;
using Ouroboros.Automation.Commands;
using Ouroboros.Automation.Looting;
using Ouroboros.Automation.Support;
using Ouroboros.Automation.Walking;
using Ouroboros.Client;
using Ouroboros.Data;
using Ouroboros.Data.Meta;
using Ouroboros.Memory;
using Ouroboros.Model;
using Ouroboros.Services.Managers;
using Ouroboros.Services.Pathfinding;

namespace Ouroboros.Automation;

/// <summary>
///     Per-client façade that the automation loops read and act through. It bundles the live game state
///     and the outbound action senders so routines never touch the proxy/socket layer directly — they
///     observe <see cref="Entities" />/<see cref="Self" /> and issue commands via <see cref="Server" />.
/// </summary>
public sealed class BotContext
{
    private readonly DarkAgesClient Client;

    public BotContext(DarkAgesClient client)
    {
        Client = client;
        Engine = new AutomationEngine(this);
        Walker = new WalkerRoutine();
        Engine.Register(Walker);
        Combat = new CombatRoutine();
        Engine.Register(Combat);
        Crasher = new CrasherRoutine();
        Engine.Register(Crasher);
        Support = new SupportRoutine();
        Engine.Register(Support);
        Consumables = new ItemRoutine();
        Engine.Register(Consumables);
        Loot = new LootRoutine();
        Engine.Register(Loot);
        Trash = new TrashRoutine();
        Engine.Register(Trash);
        NpcScript = new NpcScriptRunner();
        Engine.Register(NpcScript);
        Commands = new SlashCommandInterpreter();
        DefaultCommands.Register(Commands);
        Npc = new NpcSession(client);
        Chat = new ServerTextRouter(this);
    }

    /// <summary>Passive event bus over inbound server/chat text (bot-check panic, cast-confirm, curse, durability…).</summary>
    public ServerTextRouter Chat { get; }

    /// <summary>Drives the NPC dialog/menu the client currently has open.</summary>
    public NpcSession Npc { get; }

    /// <summary>Runs scripted NPC interactions (banking / vendoring / quests) through <see cref="Npc" />.</summary>
    public NpcScriptRunner NpcScript { get; }

    /// <summary>The attached game window (process handle, memory stream, patch surface), or null if none.</summary>
    public DaWindow? Window => Client.DaWindow;

    /// <summary>Posts keystrokes to this client's game window, or null if no window is attached yet.</summary>
    public InputSender? Input
    {
        get
        {
            var handle = Client.DaWindow?.WindowHandle ?? nint.Zero;

            return handle == nint.Zero ? null : new InputSender(handle);
        }
    }

    /// <summary>The automation loop manager for this client.</summary>
    public AutomationEngine Engine { get; }

    /// <summary>The waypoint walker (registered on the engine).</summary>
    public WalkerRoutine Walker { get; }

    /// <summary>The combat auto-attacker (registered on the engine; off until enabled).</summary>
    public CombatRoutine Combat { get; }

    /// <summary>The Crasher execute combo (self-damage → execute); inert until configured and enabled.</summary>
    public CrasherRoutine Crasher { get; }

    /// <summary>The heal/buff support routine (registered on the engine; off until enabled).</summary>
    public SupportRoutine Support { get; }

    /// <summary>The consumable auto-use routine — potions/mana items at thresholds (off until enabled).</summary>
    public ItemRoutine Consumables { get; }

    /// <summary>The auto-loot routine — walks to and picks up nearby ground drops (off until enabled).</summary>
    public LootRoutine Loot { get; }

    /// <summary>The drop-trash routine — drops inventory items on the trash list (off until enabled).</summary>
    public TrashRoutine Trash { get; }

    /// <summary>The chat slash-command interpreter for this client.</summary>
    public SlashCommandInterpreter Commands { get; }

    /// <summary>
    ///     Lock a routine should hold when reading or mutating cross-thread game state (entity/map
    ///     collections), since routines run on their own tasks alongside the packet process loop.
    /// </summary>
    public object SyncRoot { get; } = new();

    /// <summary>Commands sent to the real server (walk, cast, use item/skill, turn, …).</summary>
    public ServerActions Server => Client.ServerActions;

    /// <summary>Packets injected toward the game client (display updates, messages, …).</summary>
    public ClientActions ClientView => Client.ClientActions;

    /// <summary>Entities currently tracked for this client.</summary>
    public EntityManager Entities => Client.EntityManager;

    /// <summary>The character's skills, with cooldown/ready state.</summary>
    public SkillBook Skills => Client.SkillBook;

    /// <summary>The character's spells, with cast-duration/ready state.</summary>
    public SpellBook Spells => Client.SpellBook;

    /// <summary>The character's inventory pane, kept in sync from add/remove-item packets.</summary>
    public Inventory Inventory => Client.Inventory;

    /// <summary>The controlled character, once the world hand-off has identified it.</summary>
    public Aisling? Self => Client.Aisling;

    /// <summary>The character's vitals (HP/MP/level), updated from the Attributes packet.</summary>
    public SelfState Vitals => Client.Vitals;

    /// <summary>The local player's active status-effect icons, updated from the Effect packet.</summary>
    public EffectTracker Effects => Client.Effects;

    /// <summary>The local player's active ClientStatus bitmask (sleep/curse/blind/…) with expiry sweep.</summary>
    public StatusState Status => Client.Status;

    /// <summary>Session exp/gold/game-point gains and their per-hour rates.</summary>
    public StatsTracker Stats => Client.Stats;

    /// <summary>The character's current server-confirmed position.</summary>
    public Point Position => Client.ServerPoint;

    /// <summary>The current map, or null before one is loaded.</summary>
    public Map? Map => Client.Aisling?.Map;

    /// <summary>The intra-map A* pathfinder for the current map, rebuilt on each map load.</summary>
    public Pathfinder? Pathfinder => Client.Pathfinder;

    /// <summary>The inter-map route finder over the world graph.</summary>
    public Routefinder Routefinder => Client.Routefinder;

    /// <summary>Injects a short status message into the game client (orange bar by default).</summary>
    public void Reply(string message, ServerMessageType type = ServerMessageType.OrangeBar1)
        => Client.ClientActions.SendServerMessage(new ServerMessageArgs
        {
            Message = message,
            ServerMessageType = type
        });

    /// <summary>Injects a multi-line message into the game client as a scroll window.</summary>
    public void ReplyWindow(string message) => Reply(message, ServerMessageType.ScrollWindow);

    /// <summary>Uses a skill by name if it is known and off cooldown. Returns whether the use was sent.</summary>
    public bool UseSkill(string name)
    {
        if (Skills[name] is not { } skill || !skill.IsReady)
            return false;

        Server.SendSkillUse(new SkillUseArgs { SourceSlot = skill.Slot });

        return true;
    }

    /// <summary>The persistent world graph (maps + warps) used for cross-map routing.</summary>
    public WorldMeta World => Client.WorldStorage.Value;

    /// <summary>Adds a warp edge to the world graph, persists it, and rebuilds the route finder.</summary>
    public void AddWarp(short sourceMapId, int sourceX, int sourceY, short destMapId, int destX, int destY)
        => Client.AddWarp(sourceMapId, sourceX, sourceY, destMapId, destX, destY);

    /// <summary>Removes a warp by its source tile. Returns whether one was removed.</summary>
    public bool RemoveWarp(short mapId, int x, int y) => Client.RemoveWarp(mapId, x, y);

    /// <summary>Applies persisted automation settings (thresholds + item lists) to this client's routines.</summary>
    public void ApplyConfig(AutomationConfig config)
    {
        Support.HealHealthThreshold = config.HealThreshold;
        Support.Buffs.Clear();
        Support.Buffs.AddRange(config.Buffs);

        Consumables.HealHealthThreshold = config.PotionHpThreshold;
        Consumables.RestoreManaThreshold = config.PotionMpThreshold;
        Consumables.HealItems.Clear();
        Consumables.HealItems.AddRange(config.HealItems);
        Consumables.ManaItems.Clear();
        Consumables.ManaItems.AddRange(config.ManaItems);

        Trash.TrashItems.Clear();
        Trash.TrashItems.AddRange(config.TrashItems);
    }

    /// <summary>Captures this client's current automation settings into <paramref name="config" />.</summary>
    public void CaptureConfigInto(AutomationConfig config)
    {
        config.HealThreshold = Support.HealHealthThreshold;
        config.Buffs = Support.Buffs.ToList();
        config.PotionHpThreshold = Consumables.HealHealthThreshold;
        config.PotionMpThreshold = Consumables.RestoreManaThreshold;
        config.HealItems = Consumables.HealItems.ToList();
        config.ManaItems = Consumables.ManaItems.ToList();
        config.TrashItems = Trash.TrashItems.ToList();
    }

    /// <summary>Persists this client's automation settings to disk.</summary>
    public void SaveConfig() => Client.SaveAutomationConfig();
}
