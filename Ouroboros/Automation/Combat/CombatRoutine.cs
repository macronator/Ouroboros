using Chaos.Extensions.Geometry;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Ouroboros.Defintions;
using Ouroboros.Services.Pathfinding;

namespace Ouroboros.Automation.Combat;

/// <summary>
///     Selects the nearest safe target among nearby monsters and attacks it — casting a ready attack spell
///     when available, assailing when adjacent, or walking toward it when out of melee range with no ready
///     spell. Off by default; toggle via <see cref="Enabled" />. Target safety (whitelists/blacklists/
///     invisible sprites) comes from CONSTANTS via <see cref="TargetSelector" />.
/// </summary>
public sealed class CombatRoutine : BotRoutine
{
    private readonly PathfinderOptions PathOptions = new();
    private byte StepCount;

    public override string Name => "Combat";

    /// <summary>When false the routine idles.</summary>
    public bool Enabled { get; set; }

    /// <summary>Paced so casts/attacks are not flooded; the server still enforces real cooldowns.</summary>
    public override TimeSpan Interval => CONSTANTS.QUARTER_SECOND;

    public override ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (!Enabled)
            return default;

        var map = context.Map;

        if (map is null)
            return default;

        var monsters = context.Entities.GetNearbyMonsters(null);

        if (monsters.Count == 0)
            return default;

        var candidates = monsters
                         .Select(static monster => new TargetCandidate(monster.Id, monster.Sprite, monster.HealthPercent, monster.X, monster.Y))
                         .ToArray();

        var position = context.Position;
        var target = TargetSelector.Select(candidates, position.X, position.Y, ResolveRules(map.Id, map.Name));

        if (target is not { } chosen)
            return default;

        //prefer a ready attack spell
        var spell = context.Spells.Snapshot().FirstOrDefault(candidate => candidate.IsReady && IsAttackSpell(candidate.Name));

        if (spell is not null)
        {
            context.Server.SendSpellUse(new SpellUseArgs
            {
                SourceSlot = spell.Slot,
                ArgsData = BuildSpellTarget(chosen.Id, chosen.X, chosen.Y)
            });

            return default;
        }

        var distance = Math.Abs(chosen.X - position.X) + Math.Abs(chosen.Y - position.Y);

        //assail if the target is adjacent
        if (distance <= 1)
        {
            var assail = context.Skills["Assail"];

            if (assail is { IsReady: true })
            {
                //assail hits the tile we're facing, so turn toward the target first
                var facing = new Point(chosen.X, chosen.Y).DirectionalRelationTo(position);

                if (facing != Direction.Invalid)
                    context.Server.SendTurn(new TurnArgs { Direction = facing });

                context.Server.SendSkillUse(new SkillUseArgs { SourceSlot = assail.Slot });
            }

            return default;
        }

        //out of melee range with no ready spell — approach the target, but not while the walker is routing
        if (context.Walker.Route is null && context.Pathfinder is { } pathfinder)
        {
            var path = pathfinder.FindPath(position, new Point(chosen.X, chosen.Y), PathOptions);

            if (path.Count > 0)
            {
                var direction = path.Peek().DirectionalRelationTo(position);

                if (direction != Direction.Invalid)
                    context.Server.SendClientWalk(new ClientWalkArgs { Direction = direction, StepCount = StepCount++ });
            }
        }

        return default;
    }

    private static bool IsAttackSpell(string name)
        => CONSTANTS.KNOWN_ATTACKS1.Contains(name) || CONSTANTS.KNOWN_ATTACKS2.Contains(name);

    private static TargetRules ResolveRules(short mapId, string mapName)
        => new()
        {
            MaxRange = CONSTANTS.DEFAULT_MAX_RANGE,
            InvisibleSprites = CONSTANTS.INVISIBLE_SPRITES,
            UndesirableSprites = CONSTANTS.UNDESIRABLE_SPRITES,
            Whitelist = CONSTANTS.WHITE_LIST_BY_MAP_ID.GetValueOrDefault((ushort)mapId)
                        ?? CONSTANTS.WHITE_LIST_BY_MAP_NAME.GetValueOrDefault(mapName),
            Blacklist = CONSTANTS.BLACK_LIST_BY_MAP_NAME.GetValueOrDefault(mapName)
        };

    //Targeted-spell payload, confirmed against DALib's 7.41 UseSpellPacket (0x0F):
    //[u32 BE targetSerial][u16 BE x][u16 BE y]. Servers typically read only the serial.
    private static byte[] BuildSpellTarget(uint targetId, int x, int y)
        =>
        [
            (byte)(targetId >> 24), (byte)(targetId >> 16), (byte)(targetId >> 8), (byte)targetId,
            (byte)(x >> 8), (byte)x,
            (byte)(y >> 8), (byte)y
        ];
}
