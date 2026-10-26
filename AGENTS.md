# Agent notes for REBIRTH 3.0

Project directives: read `_Documentation/REBIRTH_PROJECT_OPERATING_DIRECTIVES.md` before changing anything.

Build: `dotnet build RebirthUtils.csproj -c Debug` (output `RebirthUtils.dll` in the mod root).
The game must be closed to build: it locks the DLL.

## Working with another agent (Codex and Claude Code)

Codex leads on changes; Claude Code assists (review, Game Bridge playtesting, verification, isolated tasks).
- Read `_Documentation/AGENT_HANDOFF.md` before starting and update it before stopping.
- Use separate branches or worktrees. Never edit the same checkout at the same time, and never touch files the handoff file lists as being edited by the other agent.
- Only one agent builds, launches or play-tests at a time (the game locks `RebirthUtils.dll` and the bridge drives one instance). Set the game owner in the handoff file first and clear it afterwards.
- Don't commit, push or merge the other agent's work without the user's say-so.

## Testing in the running game: the Game Bridge

You can launch the game, **play it** (walk, look, use stations, click through UI, craft, cook, eat,
fight) and check the results yourself through `Tools/GameBridge/gamebridge.ps1`. Every command prints
JSON. The exit code is non-zero on failure. Actions go through the game's real input actions (virtual
keyboard/mouse), so game code and REBIRTH patches behave exactly as they do for a player. The full
command list is in `powershell -File Tools\GameBridge\gamebridge.ps1 help`.

```powershell
$gb = "Tools\GameBridge\gamebridge.ps1"   # run as: powershell -ExecutionPolicy Bypass -File $gb <command> ...
launch [-Reset]            # build, start, auto-load the test save (fresh copy with -Reset), set 9:00, wait until in-game
restart                    # quit, rebuild, relaunch (after C# or XML changes)
status                     # state = menu | loading | spawning | ingame | dead
```

**See**
```
state -Sections "player,stats,buffs,cvars,inventory,skills,world"
target                                     # what's under the crosshair (block + "Press E" prompt, or entity)
findblocks name=campfire radius=24         # nearby blocks by name
entities radius=40 contains=zombie
uitree [window=ID]                         # every visible UI node: text, items, recipes, crafting queue, buttons, rect=[x,y,w,h]
screenshot label                           # PNG path: LOOK AT IT to judge layout/visuals
errors -Since <lastLogSeq>                 # new error/exception log lines
```

**Act**
```
walkto x=14.5 z=993 [run=1]                # walks with real movement input, turns smoothly, unsticks itself
lookat x= y= z= block=1 | entity=ID        # smooth camera turn
activate x=14 y=45 z=995 block=1           # look at it, press E (opens workstations, containers, doors)
key name=Tab | Escape | E | R | Space      # press a physical key (fires every action bound to it)
select slot=1..10                          # toolbelt hotkey
press action=Primary [seconds=0.2]         # attack / eat / use held item;  Secondary = power attack / aim
move forward=1 seconds=1 [run=1] [jump=1]
click text="Baked Potato" | id= | path= | item= | recipe= | x= y=   [button=right] [shift=1]
drag from.item=foodBakedPotato to.path=toolbelt/windowToolbelt/toolbelt/inventory/0
type id=searchInput value="potato"   hover text=...   scroll ... delta=-1
fight [contains=zombie] [aim=body] [power=1]    # real-time combat loop run inside the game
flee                                        # run straight away from the nearest enemy
```

**Test setup and cheats** (to prepare a situation, not to replace playing it)
```
give item=meleeWpnClubT1BaseballBat toolbelt=1   give item=ammo9mmBulletBall count=60
spawn entity=zombieArlene distance=8
damage amount=20 type=Bashing
console "settime 1 9 0" "buff buffDrugPainkillers"   # any console command; output + log lines returned
```

**Scenarios** (repeatable, PASS/FAIL per step): `test Tools\GameBridge\tests\<name>.json`. The existing ones are
`smoke`, `cooking_campfire`, `combat_melee` and `combat_ranged`; the step format is in
`Tools/GameBridge/tests/README.md`. Add a scenario for every feature you change.

### How to play well (learned the hard way)
- **Windows:** Esc is `key name=Escape` (the game's `permanent.Cancel`). Tab opens the inventory/crafting
  screen. Gameplay keys (attack, toolbelt) are ignored while a window is open, so close it first.
- **Items:** slots are click-to-pick / click-to-place. `drag` handles that. `giveself` puts items in the
  backpack, so use `give ... toolbelt=N` to hand something to the player directly.
- **Zombies:** they are faster than a bridge round trip, so always fight with `fight`, never with
  individual `press` calls. A single zombie is something you fight, not flee:
  - Hit first as it steps in (that can stun it), then keep hitting on a steady rhythm with normal attacks.
  - Aim at the upper body while it moves, and at the head when it is stunned or knocked down.
  - Only step aside from a swing that can actually reach you, then step straight back in. Keep moving a
    little instead of freezing.
  - At about a third of your stamina, walk a few metres away, face it and recover. Come back at about
    75%, or earlier with a first hit if it walks back into range.
  - Run away only when badly hurt or outnumbered. Fight on flat ground, never next to a big drop.
  - With a gun, keep your distance and reload when the magazine is empty.
  - Test **both melee and ranged** when you touch combat.
- **Instincts** (`guard`) run inside the game and must stay ON. On arrival (spawn, respawn, load,
  teleport) they scan 30 m and log what is there, engage awake threats nearby, turn to face zombies
  approaching from the side or behind, heal when hurt and clear, and reload when quiet. Never switch them
  off to "control" a test: start scenarios with `cleararea radius=40` + `restore` instead. Read
  `surroundings` before acting after any arrival.
- **Never back up blindly:** the fight loop checks behind before stepping back (it sidesteps or turns to
  face instead) and retreats away from all nearby threats, not just one.
- **Fight results fail on incidents** ("in range without attacking", "damage without hitting back",
  "chasing an enemy that isn't coming"). Each fight writes a 5 Hz trace (`out/traces/`). Read it when
  something looks wrong before changing code.
- **Visibility:** it is daytime (9:00) after launch. Use `console "settime <day> 9 0"` if it gets dark.
- **Death:** the launcher presses Spawn automatically. Otherwise use `click text=Spawn`. The dropped
  backpack is an entity (`entities contains=backpack`).
- Don't quit next to a zombie: it will be there on reload. Prefer `launch -Reset` for a clean save.

### Workflow for a change
1. Edit code/XML, then `restart` (XML and DLL changes need a full restart).
2. Reproduce the feature by playing it: walk there, open it, click it, use it.
3. Check the result: `state`, `cvar`, `uitree`, `errors -Since <seq>`, and look at `screenshot`s. Judge
   the UI too: is anything misaligned, clipped, overlapping, unclear, or could it be better?
4. Adjust and repeat. Save the flow as a `tests/*.json` scenario.

Notes:
- Configuration (world, test save, baseline save, start hour, window size): `Tools/GameBridge/gamebridge.config.json`.
  The test save is disposable. Never point `save` at one of the user's real saves.
- Moving the real mouse over the focused game window hands the cursor back to the user.
- Game log: `%USERPROFILE%\AppData\LocalLow\The Fun Pimps\7 Days To Die\Player.log`.
- Design notes: `_Documentation/GameBridge/README.md`.
