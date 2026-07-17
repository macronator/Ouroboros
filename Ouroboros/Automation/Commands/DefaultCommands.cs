using Ouroboros.Automation.Walking;
using Ouroboros.Defintions;

namespace Ouroboros.Automation.Commands;

/// <summary>Registers the built-in slash commands. Kept small and additive; more can be layered on later.</summary>
public static class DefaultCommands
{
    public static void Register(SlashCommandInterpreter interpreter)
    {
        interpreter.Register("help", "list commands", (context, _) =>
            context.ReplyWindow(string.Join('\n', interpreter.Registered
                                                             .OrderBy(command => command.Verb)
                                                             .Select(command => $"/{command.Verb} - {command.Help}"))));

        interpreter.Register("where", "show current map and position", (context, _) =>
        {
            var map = context.Map;
            var position = context.Position;

            context.Reply(map is null
                ? "no map loaded"
                : $"{map.Name} [{map.Id}] @ ({position.X}, {position.Y})");
        });

        interpreter.Register("skills", "list skills and cooldowns", (context, _) =>
        {
            var now = DateTime.UtcNow;
            var skills = context.Skills.Snapshot();

            context.ReplyWindow(skills.Count == 0
                ? "no skills"
                : string.Join('\n', skills.OrderBy(skill => skill.Slot)
                                          .Select(skill => $"[{skill.Slot}] {skill.Name} - "
                                                           + (skill.IsReadyAt(now)
                                                               ? "ready"
                                                               : $"cd {(int)skill.RemainingCooldownAt(now).TotalSeconds}s"))));
        });

        interpreter.Register("spells", "list spells and cooldowns", (context, _) =>
        {
            var now = DateTime.UtcNow;
            var spells = context.Spells.Snapshot();

            context.ReplyWindow(spells.Count == 0
                ? "no spells"
                : string.Join('\n', spells.OrderBy(spell => spell.Slot)
                                          .Select(spell => $"[{spell.Slot}] {spell.Name} - "
                                                           + (spell.IsReadyAt(now) ? "ready" : "cd")
                                                           + (spell.IsBuffActiveAt(now) ? " (active)" : ""))));
        });

        interpreter.Register("inv", "list inventory items", (context, _) =>
        {
            var items = context.Inventory.Snapshot();

            context.ReplyWindow(items.Count == 0
                ? "inventory empty"
                : $"{context.Inventory.Count}/{Model.Inventory.Capacity} used:\n"
                  + string.Join('\n', items.OrderBy(item => item.Slot)
                                           .Select(item =>
                                           {
                                               var count = item.Count > 1 ? $" x{item.Count}" : "";
                                               var durability = item.DurabilityPercent is { } percent ? $" ({percent}%)" : "";

                                               return $"[{item.Slot}] {item.Name}{count}{durability}";
                                           })));
        });

        interpreter.Register("count", "count an item by name: /count <name>", (context, args) =>
        {
            if (args.Raw.Length == 0)
            {
                context.Reply("usage: /count <name>");

                return;
            }

            var total = context.Inventory.CountOf(args.Raw);
            context.Reply(total == 0 ? $"no '{args.Raw}'" : $"{args.Raw}: {total}");
        });

        interpreter.Register("near", "list nearby monsters", (context, _) =>
        {
            var monsters = context.Entities.GetNearbyMonsters(null);

            context.ReplyWindow(monsters.Count == 0
                ? "no monsters nearby"
                : $"{monsters.Count} nearby:\n"
                  + string.Join('\n', monsters.Select(monster =>
                      $"sprite {monster.Sprite} @ ({monster.X}, {monster.Y}) hp {monster.HealthPercent}%")));
        });

        interpreter.Register("goto", "walk to a named location or 'mapId x y'", (context, args) =>
        {
            if (args.Raw.Length == 0)
            {
                context.Reply("usage: /goto <location>  or  /goto <mapId> <x> <y>");

                return;
            }

            Waypoint waypoint;
            string label;

            if (CONSTANTS.WALK_LOCATIONS.TryGetValue(args.Raw, out var location))
            {
                waypoint = new Waypoint(location.MapId, location.X, location.Y);
                label = args.Raw;
            } else if (args.Tokens.Count == 3
                       && short.TryParse(args.Tokens[0], out var mapId)
                       && int.TryParse(args.Tokens[1], out var x)
                       && int.TryParse(args.Tokens[2], out var y))
            {
                waypoint = new Waypoint(mapId, x, y);
                label = $"{mapId}:({x}, {y})";
            } else
            {
                context.Reply($"unknown location: {args.Raw}");

                return;
            }

            var route = new WaypointRoute(label) { Options = { Loop = false } };
            route.Waypoints.Add(waypoint);

            context.Walker.Route = route;
            context.Engine.Start();
            context.Reply($"walking to {label}");
        });

        interpreter.Register("addwarp", "map a warp: /addwarp <dstMap> <dx> <dy> (from here) or <srcMap> <sx> <sy> <dstMap> <dx> <dy>",
            (context, args) =>
            {
                var t = args.Tokens;

                if (t.Count == 3
                    && context.Map is { } map
                    && short.TryParse(t[0], out var d3Map)
                    && int.TryParse(t[1], out var d3X)
                    && int.TryParse(t[2], out var d3Y))
                {
                    var position = context.Position;
                    context.AddWarp(map.Id, position.X, position.Y, d3Map, d3X, d3Y);
                    context.Reply($"warp added: {map.Id}:({position.X},{position.Y}) -> {d3Map}:({d3X},{d3Y})");
                } else if (t.Count == 6
                           && short.TryParse(t[0], out var sMap)
                           && int.TryParse(t[1], out var sX)
                           && int.TryParse(t[2], out var sY)
                           && short.TryParse(t[3], out var dMap)
                           && int.TryParse(t[4], out var dX)
                           && int.TryParse(t[5], out var dY))
                {
                    context.AddWarp(sMap, sX, sY, dMap, dX, dY);
                    context.Reply($"warp added: {sMap}:({sX},{sY}) -> {dMap}:({dX},{dY})");
                } else
                    context.Reply("usage: /addwarp <dstMap> <dx> <dy>   or   /addwarp <srcMap> <sx> <sy> <dstMap> <dx> <dy>");
            });

        interpreter.Register("delwarp", "remove a warp: /delwarp <mapId> <x> <y>", (context, args) =>
        {
            var t = args.Tokens;

            if (t.Count == 3 && short.TryParse(t[0], out var mapId) && int.TryParse(t[1], out var x) && int.TryParse(t[2], out var y))
                context.Reply(context.RemoveWarp(mapId, x, y) ? "warp removed" : "no warp at that tile");
            else
                context.Reply("usage: /delwarp <mapId> <x> <y>");
        });

        interpreter.Register("warps", "list warps mapped on the current map", (context, _) =>
        {
            var map = context.Map;

            if (map is null)
            {
                context.Reply("no map loaded");

                return;
            }

            var warps = context.World.Maps.TryGetValue(map.Id, out var meta) ? meta.Warps : [];

            context.ReplyWindow(warps.Count == 0
                ? "no warps mapped on this map"
                : string.Join('\n', warps.Select(warp =>
                    $"({warp.SourcePoint.X},{warp.SourcePoint.Y}) -> {warp.Destination.MapId}:({warp.Destination.X},{warp.Destination.Y})")));
        });

        interpreter.Register("stop", "stop walking / automation", (context, args) =>
        {
            context.Walker.Route = null;
            _ = context.Engine.StopAsync();
            context.Reply("stopped");
        });

        interpreter.Register("start", "start automation", (context, _) =>
        {
            context.Engine.Start();
            context.Reply("started");
        });

        interpreter.Register("fight", "toggle combat auto-attack", (context, _) =>
        {
            context.Combat.Enabled = !context.Combat.Enabled;

            if (context.Combat.Enabled)
                context.Engine.Start();

            context.Reply(context.Combat.Enabled ? "combat on" : "combat off");
        });

        interpreter.Register("support", "toggle heal/buff support", (context, _) =>
        {
            context.Support.Enabled = !context.Support.Enabled;

            if (context.Support.Enabled)
                context.Engine.Start();

            context.Reply(context.Support.Enabled ? "support on" : "support off");
        });

        interpreter.Register("hp", "show current vitals", (context, _) =>
        {
            var vitals = context.Vitals;

            context.Reply($"HP {vitals.CurrentHp}/{vitals.MaximumHp} ({vitals.HealthPercent}%)  "
                          + $"MP {vitals.CurrentMp}/{vitals.MaximumMp} ({vitals.ManaPercent}%)");
        });
    }
}
