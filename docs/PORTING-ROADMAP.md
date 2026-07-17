# Ouroboros Roadmap

A living plan for building out Ouroboros's automation/tooling. It draws on the broader
Dark Ages tooling landscape — the many proxies and bots that came before it — as references
for *what features exist and how they behave*, never copying anyone's code, and never porting
any unsafe or host-reaching behaviors.

These materials and references are clean-room, static-analysis notes kept locally (not in this
repo), gathered from multiple prior tools rather than any single one. They all target the same
game and network protocol, so their features map almost 1:1 onto Ouroboros's existing
Chaos-based proxy.

## Guiding rules
- Work happens on `dev-build` (never on `develop`).
- Clean-room only: this is an open-source project — never paste code written by anyone else
  (Accolade, Ascend, Deity, Ditto, Dojo, ETDA, Porygon, ProxyBase, SleepHunter, Zeus, the many
  open-source DA private servers and client/engine re-writes, or our own prior work). We may use
  the same or similar concepts, but always re-write them in our own code.
- No Claude attribution in commits.

## Already present in Ouroboros (do NOT re-port)
- Loopback MITM proxy spine + per-opcode client/server handler tables (`Networking/`, `Client/`).
- Heartbeat + tick-sync priority queues (`Networking/NetworkPriority.cs`, `PacketPriority.cs`).
- Full outbound action surface — `Client/ServerActions.cs` (walk, spell/skill/item use, turn,
  pickup, dialog/menu, whisper…) and `Client/ClientActions.cs`.
- Entity models + `EntityManager`; trackers (`Data/*Trackers.cs`).
- Pathfinding: A* (`Services/Pathfinding/Pathfinder.cs`) + Dijkstra (`Routefinder.cs`).
- Bot data already in `Defintions/CONSTANTS.cs`: named-map waypoints (`WALK_LOCATIONS`),
  memory-patch offsets (`FORCEJUMP_IP_OFFSET`, `SKIP_LOAD_WALLS_OFFSET`, `PORT_OFFSET`,
  `AISLING_NAME_OFFSET`, …), and the spell taxonomy (`KNOWN_HEALS/DIONS/CURSES/CONTROLS/
  ATTACKS`, casting white/blacklists, `KNOWN_RANGERS`, regex patterns).
- Dual-use primitives: `PInvoke/ProcessMemoryStream.cs` (RPM/WPM by address, == the reference bot `Stream0`),
  and in `PInvoke/UnsafeNativeMethods.cs` the injection/launch toolkit (`CreateRemoteThread`,
  `VirtualAllocEx/FreeEx`, `WriteProcessMemory`, `ReadProcessMemory`, `OpenProcess`,
  `CreateProcess` suspended, `ResumeThread`, `GetProcAddress`, `GetModuleHandle`), DWM thumbnail
  mirror, and window control.

---

## Build phases (proposed order)

### Phase 0 — Automation engine foundation
The one missing piece everything else stands on.
- `Automation/` namespace: a per-client task-runner (the reference bot `Class20`/`Delegate0`): a set of
  worker loops with start/stop, safe retry, and pacing (`CONSTANTS.BOT_DELAY_MS`).
- A `BotContext` hanging off `DarkAgesClient` exposing live state (self, entities, map,
  inventory, skills) + the `ServerActions`/`ClientActions` façade the loops drive.
- Deliverable: an empty engine that starts/stops cleanly and can run a no-op loop.

### Phase 1 — Packet console + craft DSL
- Craft DSL parser (the reference bot §4.1 `smethod_0`): text → raw packet. Tokens `str/str8/str16`,
  `npc(name)/user(name)` → entity serial, `xy(x,y)` → coords, remaining hex → body; CP949.
- Packet log stream (hook `ProxyClient/ProxyServer.OnReceive` + the send path), with opcode
  names from Chaos `ClientOpCode`/`ServerOpCode`, hex + ASCII, direction, filtering.
- WPF console: log view, send-to-client / send-to-server, copy-hex. Foundation + debug tool.

### Phase 2 — Skills/spells + cooldown tracking
- Skill/spell models with ready-state timers and durations (the reference bot `Class130–134`), populated
  from `AddSkillToPane`/`AddSpellToPane` (handlers already exist). Uses `CONSTANTS` durations.
- Feeds Phase 5.

### Phase 3 — Waypoint + walking bot
- Waypoint model (map-qualified coords) + route persistence (`waypoints/`), editor UI.
- Walker: drives `Pathfinder`/`Routefinder` → `SendClientWalk`, with proximity/speed rules
  (the reference bot §8.5). Named-map navigation already seeded by `WALK_LOCATIONS`.

### Phase 4 — Chat slash-command interpreter
- Case-insensitive command dictionary (the reference bot `Class58`, ~39 commands). Hook the public/whisper
  handler; route verbs to actions. Start with a core subset (`/goto`, `/f`, `/s`, `/r`,
  banking) and grow. NPC-dialog session helper with cooldown (the reference bot `Class73`).

### Phase 5 — Combat/support automation ("the brain")
- Port the reference bot `Class21` worker loops consuming the existing `CONSTANTS` tables:
  support (heal/buff/dispel at % thresholds), combat (target selection via filter rules +
  the white/blacklists), farming, audio alerts. Target/creature effect inference (the reference bot `Class140`).
- Largest phase; depends on Phases 0, 2, 3, and `EntityManager`.

### Phase 6 — Dual-use game-process layer (owner-authorized)
- Input simulation: add `PostMessage`/`SendMessage`/`MapVirtualKey`/`FindWindow`/`RegisterHotKey`
  P/Invokes (the one primitive gap) + a keystroke/mouse synth helper (the reference bot `Class8`).
- Process discovery: map the client PID via `GetExtendedTcpTable` (IPHLPAPI) — cleaner than
  the reference bot's netstat shell-out.
- Memory patch + injection orchestrator: use the existing toolkit + `CONSTANTS` offsets for
  suspended-launch patches and optional helper-DLL injection.
  - ⚠ The offsets are specific to one `Darkages.exe` build; verify against the target client
    or add a signature scan before writing memory.
- Hotkey engine + optional DWM thumbnail preview pane (primitives already present).

---

## Status
- [x] Heartbeat/tick priority queues (done)
- [x] Phase 0 — automation engine foundation (done)
- [x] Phase 1 — packet console + craft DSL (done: DSL parser, log model, raw
  injection, receive taps, and the WPF console tab — live log (time/direction/opcode/
  hex), capture toggle, clear, and a craft box that injects toward client/server)
- [x] Phase 2 — skills/spells + cooldowns (done: SkillBook/SpellBook populated
  from pane + cooldown packets; ready-state + buff-active tracking)
- [~] Phase 3 — waypoint + walking bot (done: Waypoint/route model, persistence,
  intra-map A* walker, and inter-map traversal via warp/gate/field transition
  tiles resolved from WorldMeta; waypoint editor UI still pending)
- [x] Phase 4 — chat slash-commands (done: interpreter hooked into public-message
  handler, replies injected to client; starter set /help /where /skills /spells
  /goto /stop /start — more verbs can be layered on)
- [x] Phase 5 — combat/support brain (5a: TargetSelector + CombatRoutine /fight;
  5b: SelfState HP/MP tracking from Attributes packet + SupportRoutine heal/buff
  via /support, /hp. Combat faces the target before assailing. Spell target
  ArgsData layout confirmed against DALib's 7.41 UseSpellPacket. Remaining
  refinement: dispel (needs effect tracking).)
- [x] Inventory tracking (`Inventory`/`Item` models populated from Add/RemoveItemToPane;
  by-slot and by-name lookups, stack totals, free-slot/full checks; `/inv` and `/count`
  commands). Feeds item-use automation (potions/reagents) and bank/drop logic later.
- [x] Consumable auto-use (`Automation/Support/ItemRoutine.cs`): uses a healing/mana item
  from inventory when HP/MP drop to a threshold, HP first, one use per tick, re-use paced
  by `MinInterval` so a single low reading doesn't drain the stack. Off by default with empty
  item lists (nothing consumed unasked); `/pots` toggles, `/healitem`/`/manaitem` register names.
- [x] UI shell (MVVM). `MainWindow` is now a `TabControl` shell bound to a `MainWindowViewModel`,
  with each tab its own designer-ready `UserControl` under `Controls/Views/` (design-time data
  via `d:DataContext`), matching the pattern `OptionsWindow` already set:
  - **Dashboard** — live per-client status cards bound to `ClientStatusViewModel` (replaces the
    old code-behind string dump; timer refreshes the view-model, XAML binds).
  - **Automation** — two-way toggles for combat/support/consumables on the selected client.
  - **Packet Console** — live packet log (time, direction, opcode name, hex) polled from the
    per-client `PacketConsole`, with a capture toggle, clear, and a craft box that injects.
  - **Waypoints** — scaffolded UserControl + view-model, ready to fill in.
- [x] Auto-loot (`Automation/Looting/LootRoutine.cs`): walks to the nearest wanted ground drop and
  picks it into the first free inventory slot — one pickup grabs gold or item alike. Ground drops carry
  no name (only a sprite), so filtering is by sprite whitelist/blacklist; yields movement to the walker.
  Off by default; `/loot` toggles, and it appears on the Automation tab and Dashboard.
- [x] Drop-trash (`Automation/Looting/TrashRoutine.cs`): drops inventory items whose name is on a
  trash list at the character's feet (inventory items carry names, so filtering is by name — the
  complement of sprite-based loot). Off by default, empty list, paced. `/trash` toggles,
  `/trashitem [name]` curates the list, and it's on the Automation tab.
- [x] NPC dialog/menu session helper (`Automation/NpcSession.cs` + `Model/NpcDialog.cs`,
  `Model/NpcMenu.cs`). The DisplayDialog/DisplayMenu handlers (were stubbed) now track the open
  dialog/menu on the client; `NpcSession` drives it — Next/Previous/SelectOption/Close for dialogs
  (applying the ±1 DialogId action offset the protocol needs) and SelectPursuit for menus, by id or
  matching text. Commands: `/dialog` (show), `/next` `/prev` `/close`, `/pick <n|text>`,
  `/pursue <id|text>`. Foundation for scripted banking/vendoring. (Text-entry dialogs: later.)
- [ ] Phase 6 — dual-use game-process layer

## Live-test findings (in-game on 7.41)
Working: launch/login/relay, status UI, self-state (`/where`,`/hp`), skill/spell
books (`/skills`,`/spells`), nearby-entity tracking (`/near`), combat toggle, movement
tracking (self + creatures).

Fixed after live testing:
- Relay made resilient (a throwing handler passes the original packet through +
  logs `[Ouroboros] ...threw` instead of killing the connection).
- Base game-state wiring finished (was stubbed): `Client.Id`/facing, position,
  self-`Aisling` + nearby players, map+pathfinder, nearby creatures/items.
  `[SetsRequiredMembers]` added down the entity ctor chain; added `Monster`.
- `ClientManager.OnConnection` never called `AddClient` (redirect "find client" error).
- Skill/spell names come through in Chaos's `PanelName` (not `Name`) for 7.41 — resolve
  from whichever is set (books were empty → combat had nothing to use).
- `SelfState` keeps last-known max HP/MP when a current-only vitality update carries 0.
- `Pathfinder` ctor NRE'd on null (unloaded) tiles → swallowed → null pathfinder → walker
  wouldn't move. Null tiles now treated as walkable. `OnMapData` no longer throws before
  the Aisling exists (tiles reach the initial map).

Cross-map routing (world graph):
- The graph starts empty (no `data/WorldMeta.json`) and **auto-learns warps as you walk**:
  a map change that follows a walk within 2s records the tile you left (the warp) → the map/tile
  you arrived on. Each learned warp persists to `data/WorldMeta.json`, is deduped, and rebuilds
  the `Routefinder`, so cross-map `/goto` progressively works as you explore and survives restarts.
  The 2s-after-walk gate skips NPC/world-map teleports (un-walkable edges). Manual `/addwarp`
  (`<dstMap> <dx> <dy>` from here, or explicit 6-arg), `/warps`, `/delwarp` remain for curation.

Known gaps:
- Combat only casts spells whose names are in `CONSTANTS.KNOWN_ATTACKS1/2`.
- Memory-patch offsets are specific to one `Darkages.exe` build.
- Effect/dispel: the Effect packet carries only an icon byte — no reliable dispel source.
