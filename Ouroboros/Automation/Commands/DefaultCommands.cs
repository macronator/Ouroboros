using System.Globalization;
using System.IO;
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

        interpreter.Register("pots", "toggle consumable auto-use (potions/mana items)", (context, _) =>
        {
            context.Consumables.Enabled = !context.Consumables.Enabled;

            if (context.Consumables.Enabled)
                context.Engine.Start();

            context.Reply(context.Consumables.Enabled ? "consumables on" : "consumables off");
        });

        interpreter.Register("loot", "toggle auto-pickup of nearby ground drops", (context, _) =>
        {
            context.Loot.Enabled = !context.Loot.Enabled;

            if (context.Loot.Enabled)
                context.Engine.Start();

            context.Reply(context.Loot.Enabled ? "loot on" : "loot off");
        });

        interpreter.Register("trash", "toggle auto-drop of items on the trash list", (context, _) =>
        {
            context.Trash.Enabled = !context.Trash.Enabled;

            if (context.Trash.Enabled)
                context.Engine.Start();

            context.Reply(context.Trash.Enabled ? "drop-trash on" : "drop-trash off");
        });

        interpreter.Register("trashitem", "register a trash item to drop, or list them: /trashitem [name]", (context, args) =>
            RegisterConsumable(context, context.Trash.TrashItems, args.Raw, "trash"));

        interpreter.Register("healitem", "register an HP consumable, or list them: /healitem [name]", (context, args) =>
            RegisterConsumable(context, context.Consumables.HealItems, args.Raw, "heal"));

        interpreter.Register("manaitem", "register an MP consumable, or list them: /manaitem [name]", (context, args) =>
            RegisterConsumable(context, context.Consumables.ManaItems, args.Raw, "mana"));

        interpreter.Register("dialog", "show the open NPC dialog or menu", (context, _) =>
        {
            if (context.Npc.Dialog is { } dialog)
            {
                var options = dialog.Options.Count == 0
                    ? ""
                    : "\n" + string.Join('\n', dialog.Options.Select((text, index) => $"  {index + 1}. {text}"));
                var buttons = (dialog.HasPrevious ? " [prev]" : "") + (dialog.HasNext ? " [next]" : "");

                context.ReplyWindow($"{dialog.Name}: {dialog.Text}{options}{buttons}");
            } else if (context.Npc.Menu is { } menu)
            {
                var options = menu.Options.Count == 0
                    ? ""
                    : "\n" + string.Join('\n', menu.Options.Select(option => $"  [{option.PursuitId}] {option.Text}"));

                context.ReplyWindow($"{menu.Name}: {menu.Text}{options}");
            } else
                context.Reply("no dialog open");
        });

        interpreter.Register("next", "advance the NPC dialog", (context, _) =>
            context.Reply(context.Npc.Next() ? "next" : "no dialog open"));

        interpreter.Register("prev", "go back in the NPC dialog", (context, _) =>
            context.Reply(context.Npc.Previous() ? "prev" : "no dialog to go back in"));

        interpreter.Register("close", "close the NPC dialog", (context, _) =>
            context.Reply(context.Npc.Close() ? "closed" : "no dialog open"));

        interpreter.Register("pick", "choose a dialog option: /pick <number|text>", (context, args) =>
        {
            if (args.Raw.Length == 0)
            {
                context.Reply("usage: /pick <number|text>");

                return;
            }

            var chosen = byte.TryParse(args.Raw, out var option)
                ? context.Npc.SelectOption(option)
                : context.Npc.SelectOption(args.Raw);

            context.Reply(chosen ? $"picked {args.Raw}" : "no matching option");
        });

        interpreter.Register("npc", "run an NPC script: /npc pursue Bank; pick 1; next; close", (context, args) =>
        {
            if (args.Raw.Length == 0)
            {
                context.Reply("usage: /npc <step>; <step>; …   verbs: pursue <id|text>, pick <n|text>, next, close");

                return;
            }

            var steps = ParseNpcScript(args.Raw);

            if (steps.Count == 0)
            {
                context.Reply("no valid steps");

                return;
            }

            context.NpcScript.Run(steps);
            context.Engine.Start();
            context.Reply($"running {steps.Count}-step NPC script");
        });

        interpreter.Register("npcstop", "stop the running NPC script", (context, _) =>
        {
            context.NpcScript.Stop();
            context.Reply("npc script stopped");
        });

        interpreter.Register("pursue", "choose a menu pursuit: /pursue <id|text>", (context, args) =>
        {
            if (args.Raw.Length == 0)
            {
                context.Reply("usage: /pursue <id|text>");

                return;
            }

            var chosen = ushort.TryParse(args.Raw, out var pursuitId)
                ? context.Npc.SelectPursuit(pursuitId)
                : context.Npc.SelectPursuit(args.Raw);

            context.Reply(chosen ? $"pursuing {args.Raw}" : "no matching pursuit / no menu open");
        });

        interpreter.Register("key", "post a virtual-key press to the game window: /key <vkCode>", (context, args) =>
        {
            if (context.Input is not { } input)
            {
                context.Reply("no game window attached");

                return;
            }

            if (!int.TryParse(args.Raw, out var virtualKey))
            {
                context.Reply("usage: /key <vkCode>");

                return;
            }

            input.KeyPress(virtualKey);
            context.Reply($"key {virtualKey} posted");
        });

        interpreter.Register("pid", "show the attached client's process id and window handle", (context, _) =>
            context.Reply(context.Window is { } window
                ? $"pid {window.Process.Id}  hwnd 0x{window.WindowHandle:X}"
                : "no game window attached"));

        interpreter.Register("patch", "apply a memory edit by name: /patch <SkipLoadWall|ForceJumpIp|...>", (context, args) =>
        {
            if (context.Window is not { } window)
            {
                context.Reply("no game window attached");

                return;
            }

            if (!Enum.TryParse<MemoryEditFlags>(args.Raw, ignoreCase: true, out var flag))
            {
                context.Reply("usage: /patch <SkipLoadWall|ForceJumpIp|OverwriteIp|OverwritePort|SkipIntro|ForceJumpInstanceCheck>");

                return;
            }

            window.ApplyMemoryEdits(flag);
            context.Reply($"applied {flag}");
        });

        interpreter.Register("peek", "read process memory: /peek <hexAddr> <count>", (context, args) =>
        {
            if (context.Window is not { } window)
            {
                context.Reply("no game window attached");

                return;
            }

            if (args.Tokens.Count != 2
                || !long.TryParse(args.Tokens[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)
                || !int.TryParse(args.Tokens[1], out var count))
            {
                context.Reply("usage: /peek <hexAddr> <count>");

                return;
            }

            count = Math.Clamp(count, 1, 256);
            var buffer = new byte[count];
            window.Pms.Seek(address, SeekOrigin.Begin);
            _ = window.Pms.Read(buffer, 0, count);

            context.Reply(string.Join(' ', buffer.Select(value => value.ToString("X2"))));
        });

        interpreter.Register("savecfg", "save automation thresholds and item lists to disk", (context, _) =>
        {
            context.SaveConfig();
            context.Reply("automation config saved");
        });

        interpreter.Register("hp", "show current vitals", (context, _) =>
        {
            var vitals = context.Vitals;

            context.Reply($"HP {vitals.CurrentHp}/{vitals.MaximumHp} ({vitals.HealthPercent}%)  "
                          + $"MP {vitals.CurrentMp}/{vitals.MaximumMp} ({vitals.ManaPercent}%)");
        });

        interpreter.Register("stats", "show gold, weight, exp and primary stats", (context, _) =>
        {
            var vitals = context.Vitals;

            context.Reply($"Gold {vitals.Gold}  GP {vitals.GamePoints}  Wt {vitals.CurrentWeight}/{vitals.MaxWeight} ({vitals.WeightPercent}%)  "
                          + $"Exp {vitals.TotalExp} (+{vitals.ToNextLevel})  "
                          + $"Str {vitals.Str} Int {vitals.Int} Wis {vitals.Wis} Con {vitals.Con} Dex {vitals.Dex}  AC {vitals.Ac}");
        });

        interpreter.Register("status", "show active status effects (sleep/curse/blind/…)", (context, _) =>
        {
            var status = context.Status.Current;

            context.Reply(status == Ouroboros.Defintions.ClientStatus.None
                ? "no status effects"
                : $"status: {status}  (walk={context.Status.CanWalk} cast={context.Status.CanCast})");
        });

        interpreter.Register("effects", "show active effect icons", (context, _) =>
        {
            var snapshot = context.Effects.Snapshot();

            context.Reply(snapshot.Count == 0
                ? "no active effects"
                : "effects: " + string.Join(", ", snapshot.Select(effect => effect.Name ?? $"#{effect.Icon}")));
        });

        interpreter.Register("useskill", "use a skill by name", (context, args) =>
        {
            if (args.Raw.Length == 0)
            {
                context.Reply("usage: /useskill <name>");

                return;
            }

            context.Reply(context.UseSkill(args.Raw) ? $"used {args.Raw}" : $"'{args.Raw}' unknown or on cooldown");
        });

        interpreter.Register("useitem", "use an inventory item by name", (context, args) =>
        {
            if (args.Raw.Length == 0)
            {
                context.Reply("usage: /useitem <name>");

                return;
            }

            context.Reply(context.UseItem(args.Raw) ? $"used {args.Raw}" : $"'{args.Raw}' not in inventory");
        });

        interpreter.Register("crasher", "configure the Crasher execute combo — /crasher for usage", (context, args) =>
        {
            var crasher = context.Crasher;
            var tokens = args.Tokens;
            var sub = tokens.Count > 0 ? tokens[0].ToLowerInvariant() : "status";
            var rest = tokens.Count > 1 ? string.Join(' ', tokens.Skip(1)) : string.Empty;

            switch (sub)
            {
                case "status":
                    context.Reply($"crasher {(crasher.Enabled ? "ON" : "off")}  hp<={crasher.HpThreshold}  "
                                  + $"execute=[{string.Join(", ", crasher.ExecuteSkills)}]  buffs=[{string.Join(", ", crasher.PreBuffSkills)}]  "
                                  + $"hurtSkill={crasher.SelfDamageSkill ?? "-"}  hurtItem={crasher.SelfDamageItem ?? "-"}");

                    break;

                case "on" when crasher.ExecuteSkills.Count == 0:
                    context.Reply("set an execute skill first: /crasher execute <name>");

                    break;

                case "on":
                    crasher.Enabled = true;
                    context.Engine.Start();
                    context.Reply("crasher ON — it self-damages to ~1 HP; live-test carefully");

                    break;

                case "off":
                    crasher.Enabled = false;
                    context.Reply("crasher off");

                    break;

                case "execute" when rest.Length > 0:
                    crasher.ExecuteSkills.Add(rest);
                    context.Reply($"execute skill added: {rest}");

                    break;

                case "buff" when rest.Length > 0:
                    crasher.PreBuffSkills.Add(rest);
                    context.Reply($"pre-buff skill added: {rest}");

                    break;

                case "hurtskill":
                    crasher.SelfDamageSkill = rest.Length > 0 ? rest : null;
                    context.Reply($"self-damage skill = {crasher.SelfDamageSkill ?? "-"}");

                    break;

                case "hurtitem":
                    crasher.SelfDamageItem = rest.Length > 0 ? rest : null;
                    context.Reply($"self-damage item = {crasher.SelfDamageItem ?? "-"}");

                    break;

                case "hp" when int.TryParse(rest, out var hp):
                    crasher.HpThreshold = Math.Max(1, hp);
                    context.Reply($"hp threshold = {crasher.HpThreshold}");

                    break;

                case "clear":
                    crasher.Enabled = false;
                    crasher.ExecuteSkills.Clear();
                    crasher.PreBuffSkills.Clear();
                    crasher.SelfDamageSkill = null;
                    crasher.SelfDamageItem = null;
                    context.Reply("crasher config cleared");

                    break;

                default:
                    context.Reply("usage: /crasher [status | on | off | execute <name> | buff <name> | hurtskill <name> | hurtitem <name> | hp <n> | clear]");

                    break;
            }
        });

        interpreter.Register("rates", "show session exp/gold per hour (arg 'reset' to restart)", (context, args) =>
        {
            if (string.Equals(args.Raw.Trim(), "reset", StringComparison.OrdinalIgnoreCase))
            {
                context.Stats.Reset();
                context.Reply("session rates reset");

                return;
            }

            var stats = context.Stats.Snapshot();

            context.Reply($"in {stats.Elapsed:hh\\:mm\\:ss}:  Exp +{stats.ExpGained} ({stats.ExpPerHour}/hr)  "
                          + $"Gold {stats.GoldGained:+#;-#;0} ({stats.GoldPerHour}/hr)  "
                          + $"GP +{stats.GamePointsGained} ({stats.GamePointsPerHour}/hr)");
        });
    }

    //shared body for /healitem and /manaitem: no arg lists the set, otherwise adds a name (deduped)
    private static void RegisterConsumable(BotContext context, List<string> items, string name, string kind)
    {
        if (name.Length == 0)
        {
            context.Reply(items.Count == 0 ? $"no {kind} items set" : $"{kind} items: {string.Join(", ", items)}");

            return;
        }

        if (items.Contains(name, StringComparer.OrdinalIgnoreCase))
            context.Reply($"already set: {name}");
        else
        {
            items.Add(name);
            context.Reply($"{kind} item added: {name}");
        }
    }

    //parses "pursue Bank; pick 1; next; close" into steps; blank/unknown verbs are skipped
    private static List<NpcStep> ParseNpcScript(string text)
    {
        var steps = new List<NpcStep>();

        foreach (var raw in text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var space = raw.IndexOf(' ');
            var verb = (space < 0 ? raw : raw[..space]).ToLowerInvariant();
            var arg = space < 0 ? string.Empty : raw[(space + 1)..].Trim();

            NpcStep? step = verb switch
            {
                "pursue" => new NpcStep(NpcStepKind.Pursuit, arg),
                "pick" or "option" => new NpcStep(NpcStepKind.Option, arg),
                "next" => new NpcStep(NpcStepKind.Next),
                "close" => new NpcStep(NpcStepKind.Close),
                _ => null
            };

            if (step is not null)
                steps.Add(step);
        }

        return steps;
    }
}
