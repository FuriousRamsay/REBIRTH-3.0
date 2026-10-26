# Game Bridge test scenarios

`npc_ai_combat_isolated_profiler.json` requires a fresh disposable `CodexTest`
clone (`gamebridge.ps1 reset-save`) before the profiler launches. It verifies
zero nearby survivors, spawns exactly three, teleports the player beyond the
guard's 30 m arrival scan, then uses Debug Bridge `spawn` with exact `x` and
`z` ground coordinates to place one zombie beside them. The guard remains on;
the scenario requires an NPC-attributed kill and no errors. Bridge `cleararea`
only removes hostile entities, so it cannot make an old NPC-filled save isolated.

`npc_moving_talk_focus.json` starts with a spawned, unowned survivor already
within 2.5 m after the player has walked to it. It dispatches native Activate
and requires the interaction window. Stage the moving/behind position and
inspect the Bridge target and opt-in AI trace separately; a passing UI open
alone does not establish that the target was moving at the input frame.

`npc_companion_overview_portrait.json` captures the full-body character preview after a
survivor has been hired through Talk and the Companions Overview is open. The screenshot
must be inspected for stray geometry, clipping, pose, and framing; the script's pass
only confirms the window, capture, and absence of logged errors.

Camper hood/ladder Travel and Camp, relay stripe-center/shared station fade, and
reverse-light input scenarios are generated from a current read-only `/camper`
snapshot by `Tools/GameBridge/prepare_camper_surface_scenarios.py`. They remain
NOT RUN until the parent task commits/builds and establishes the disposable native
fixture. See `_Documentation/GameBridge/CAMPER_SURFACE_INPUT_SCENARIOS_20261021.md`.
Generation substitutes actual entity/root/authored-ladder coordinates; it does
not launch the game, force a mode, teleport, or replace real walk/jump/reverse input.

`mpB_015_completion_audit.json` is a read-only MP015 diagnostic smoke, NOT_RUN.
The actual native completion/independent-stage/remote ACK continuation driver is
`Tools/GameBridge/mp015_completion_live.py`; its exact observed-plan requirements,
source mapping, deferred failure matrix, and native material persistence limit are
in `_Documentation/MultiplayerAudit/ProjectWide_20261014/MP015_DURABLE_NATIVE_COMPLETION_SOURCE_20261016.md`.
Neither diagnostic availability nor an existing skill receipt proves the later
statistics/reagent stages or native region-file persistence.

`entity_studio_farmer.json` verifies the nineteen-capsule diagnostic Farmer and all
four tiers, native movement/blocker geometry, humanoid/root-motion binding, eleven
ragdoll bodies/ten joints, and a forced ragdoll/recovery. Run it after a fresh
diagnostic launch with native `ai freezepos` initially OFF; it toggles that setting
ON for the fixture and restores OFF at successful completion. A failed run may
leave it ON: inspect the command log before repeating. Guard stays enabled.
`expectEntityStudio` resolves exactly one live class within 80m and checks dotted
physics fields; equality checks may specify a nonnegative numeric `tolerance`.
It saves physics evidence and optionally a GPU model capture. This does not certify
visual quality, normal damage-triggered transitions, melee/ranged hit accuracy,
or target builds other than the one actually running.

`capture: true, fullResolution: true` records texture residency before capture,
requests mip zero for streaming textures and waits up to 15 seconds while game
frames continue. It verifies loaded mip zero and saves the texture metadata with
the render. After capture it verifies prior mip requests were restored. The bridge
also expires abandoned requests after 30 seconds; shared texture requests remain
held until every Entity Studio lease using them is released. Global game texture
quality is reported, not changed. The Farmer fixture now uses this capture mode.

UI selectors accept `exactText` for an ordinal full label match, combined with `id` and
`window`. Existing `text` keeps its substring/tooltip matching. Use exact labels for
numeric counters: matching `1` as a substring could incorrectly accept `10` or `21`.
`survival_healing_use.json` is prepared but not run: it requires a fresh disposable
completed Survivor character with zero recorded healing uses, consumes one actual
first-aid bandage, checks the inventory decrement and the specific healing-use label.
Death, reconnect and save/reload custody require the separate qualification flows.

Run one with:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\GameBridge\gamebridge.ps1 test Tools\GameBridge\tests\smoke.json
```

A scenario is a JSON file with a `name`, an optional `stopOnFailure` flag, and an ordered list of `steps`.
Each step is an object with exactly one key, the step type:

Fence wear checks use read-only `blockdamage x=X y=Y z=Z` on loaded blocks. `markFenceWear` records two authoritative `electricfencepost` damage values; `expectFenceWearSplit` requires both values to rise and their integer damage deltas to differ by at most one. Use an isolated, natively placed and wired post pair that actually shocks an enemy; `setblock` does not prepare a native electrical fixture. See `electric_fence_shared_wear_20261020.json`.

| Step | Example | Passes when |
|---|---|---|
| `action` | `{ "action": { "command": "click", "args": { "text": "Cook" } } }` | bridge returns both a successful HTTP response and `ok: true` |
| `markSkill` | `{ "markSkill": "skill.chemistry" }` | records exactly one accepted REBIRTH skill's level + fractional progress |
| `expectSkillGain` | `{ "expectSkillGain": { "skill": "skill.chemistry", "op": ">", "value": 0 } }` | change from its last baseline meets the condition |
| `expectChallenge` | `{ "expectChallenge": { "id": "rebirthlearn_chemistry", "state": "Completed" } }` | the native challenge state matches (use the actual ID from `state -Sections challenges`) |
| `console` | `{ "console": "buff buffDrugPainkillers" }` or `{ "console": ["cmd1", "cmd2"] }` | no `*** ERROR` output and no error-level log lines from the command |
| `wait` | `{ "wait": 2.5 }` | always (seconds of real time) |
| `waitFor` | `{ "waitFor": { "cvar": "myCVar", "op": ">=", "value": 1, "timeout": 30 } }` | the CVar reaches the condition before the timeout |
| `expectCvar` | `{ "expectCvar": { "name": "myCVar", "op": "==", "value": 0 } }` | comparison holds (ops: `== != > >= < <=`) |
| `expectStat` | `{ "expectStat": { "name": "food", "op": "<", "value": 50 } }` | stat (`health`, `stamina`, `food`, `water`) matches |
| `expectBuff` / `expectNoBuff` | `{ "expectBuff": "buffWellFed" }` | buff is / is not active on the player |
| `expectItem` | `{ "expectItem": { "name": "foodCanChili", "min": 1, "max": 5 } }` | toolbelt + backpack count is in range |
| `expectNoItem` | `{ "expectNoItem": "foodCanChili" }` | count is 0 |
| `ui` | `{ "ui": { "action": "open", "window": "backpack" } }` | window group exists and opened/closed |
| `expectWindowOpen` | `{ "expectWindowOpen": "backpack" }` | the window is currently open |
| `screenshot` | `{ "screenshot": "after_cooking" }` | PNG was written (path printed; open it to inspect visually) |
| `expectLog` | `{ "expectLog": { "contains": "[REBIRTH", "level": "warning", "min": 2 } }` | at least `min` matching log lines appeared since the test started (default 1) |
| `expectNoLog` | `{ "expectNoLog": { "contains": "task=break action=start" } }` | no matching log lines appeared since the test started |
| `expectNoErrors` | `{ "expectNoErrors": true }` | no error/exception log lines since the test started |

The runner prints one `PASS`/`FAIL` line per step, then a one-line JSON summary (including any new
error log lines), and exits with code 1 if any step failed.

Useful console commands for scenarios: `giveself <item> [quality] [count] [putInInventory]`,
`buff <name>`, `debuff <name>`, `settime <day> <hour> <min>`, `spawnentity <playerId> <entityClass>`,
`teleport <x> <y> <z>`, `cm` (creative menu), `dm` (debug mode), `killall`, plus any REBIRTH `rb*` command.

The runner saves full action replies in out/tests/<timestamp>_<scenario>/ for diagnosis. Use markItem with an item name, then expectItemDelta {name,op,value}, to check consumption without assuming an empty starting inventory. POST /skillsetup?confirm=CodexTest&skill=skill.ID&value=N is disposable local CodexTest fixture preparation only; never count its projected skill/challenge changes as earned progress.

For backpack cells at the bottom of a scroll viewport, `uitree` can report a cell as clickable even when its center is clipped. Scroll the backpack track until the whole target cell is visible before `drag`, and assert the resulting item positions; `ok: true` only confirms input dispatch. A `placedByClick: false` result may be either a successful mouse drop or a missed clipped cell.

Additional runner steps: look {yaw,pitch}; activateNearestEntity {contains,radius} sends a real activation input to the nearest living match (input success is NOT proof of gameplay success); expectLivingEntities {contains,radius,op,value}; expectThreats {radius,op,value} counts awake threats. For dominated allies also verify the control state: zero nearby threats alone can mean the player fled out of range.


Storage qualification (disposable CodexTest, game running):
- Current backpack model: 44 base slots (22 usable), eight tiers adding 11 each, Scavenger +11. Occupied legacy bags up to 169 slots are retained.
- `Tools/StorageGear/test_backpack_rows_live.py`: all eight current packs; shift and drag in/out,
  exact item counts, empty cursor, capacity and constant encumbrance. Attribute fixtures restore on exit.
- `Tools/StorageGear/test_backpack_rows_background_live.py`: Maintenance Technician +2 toolbelt,
  Scavenger +13 usable backpack slots; original background restored in finally.
- `Tools/StorageGear/test_backpack_rows_overflow_live.py`: occupied last pack cell and belt slot removal,
  recovery entity contents, configured 3600/1800 second lifetimes, real loot pickup.
- `Tools/StorageGear/test_backpack_rows_persistence_live.py prepare`, then quit/relaunch, then
  `verify`: Scavenger maximum 169 slots and three gold nuggets in final slot168 survive reload.
  Restore test background/attributes and recover the tail stack after qualification.
- Earlier `test_all_gear_live.py`, `test_background_storage_live.py` and `test_overflow_live.py`
  retain the historical 45-slot model and are not current backpack regression tests.
- `starter_belt_crafting.json`: 6 cloth +2 scrap iron consumed; one starter belt crafted.
These Python scenarios use the same authenticated bridge input endpoints and assert returned game
state. They are separate from the JSON runner because they iterate tiers, restore fixture state,
and compare capacities dynamically. Run with the installed Python runtime. Inspect the screenshot
paths in `_Documentation/BackpackRows/full_gear_results.json` and the background results.
Do not run these preparation scenarios on a real save.

Recent player-report regressions:
- `backpack_native_remote_drag.json`: on a disposable dedicated client after a server-issued campfire pickup, drag backpack slot 2 to toolbelt slot 2 and put the displaced coffee in backpack slot 2. Reconnect to the still-running server and verify both slots and item counts again; the JSON scenario alone does not establish server persistence.
- `crafting_keyboard_shortcut.json`: physical W crafts starter belt with exact material/output checks.
- `../test_motorcycle_mount_live.py`: nearby owned motorcycle, three real mount/dismount cycles, held item restoration and no console errors.
- `../test_resource_harvest_live.py stone|tree X Y Z`: aim at a real nearby resource surface; stone requires an empty active hand and verifies backpack gain with unchanged toolbelt. Tree requires a selected stone axe and verifies actual wood payout after felling (60-second bound). Use disposable CodexTest only.

Offline runner checks (no game needed):
`powershell -NoProfile -ExecutionPolicy Bypass -File Tools/GameBridge/test_assertion_guards.ps1`
extracts and exercises the production helper functions without executing dispatch.
Checks malformed responses, explicit null, valid empty/zero results and combined
inventory counts. Passing these checks does not validate gameplay scenarios.

- `mortar_discovery.json`: deferred shared-workspace milling qualification. Read explicit disposable-save/placed-mortar/unknown-recipe preconditions before running. Verifies grid drag, fuel-less PROCESS, ready/take, exact4 oats->4 rolled oats and screenshot/error evidence. Manual additional checks: unlock notification exactly once during ordinary play, persisted knowledge after reopen/reload, cancel/refund, existing queue migration, dedicated/remote synchronization. NOT RUN as of2026-10-04; game launch remains unauthorized.
- Offline station source checks: `node Tools/GameBridge/test_discovery_capability.mjs` and `node Tools/GameBridge/test_station_grid_ingredients.mjs`. Actual production evaluator/allocator/model extraction with disclosed doubles; not game, full native modifiers, UI or network validation. Before running these compiler fixtures, read HAV3N coordination and honor active QUIET.
- `node Tools/GameBridge/test_recipe_read_provenance.mjs`: actual discovery literature application and reading-marker methods with disclosed save/network/knowledge doubles. PASS recorded2026-10-04; does not validate native study timing, disk crash behavior, audiobook sound, or owner snapshot transport. Read HAV3N coordination before fixture compilation. Deferred mortar qualification includes configured read-only override, experiment-only knowledge, unread possession, completed real reading/equivalent audiobook, save/reload and remote admission; restore empty policy afterward.
- `node Tools/GameBridge/test_station_grid_queue.mjs`: actual generic queue binding/restore/refund and supplied native Recipe.Write/Read with disclosed ItemValue/ItemStack codec doubles. PASS recorded2026-10-04. Not actual native item serialization, chunk persistence, quality, cancellation or multiplayer validation; observe HAV3N restrictions before compiling fixture.
- `test_queue_custody_hold.mjs`: actual live tile authorization class with queue/world/capability doubles, checks preservation/log suppression/resume. Read HAV3N before compilation. `tests/workstation_queue_hold.json` is deferred observation only; its explicit native-data/operator preconditions must be fulfilled. NOT RUN; does not assert paid queue contents by itself. Full reconnect/XUi/cancellation/native persistence qualification remains.
- `workstation_open_queue_hold.json`: deferred open native workstation hold observation. Exact prepared-job/disposable/operator native-data preconditions apply. Two screenshots do not prove custody; qualify capability recovery/full-output retry/other remote owner/reconnect separately. Source gate fixture includes cache expiration and fresh output refusal; actual Harmony/native runtime NOT RUN.

- `node Tools/GameBridge/test_metabolism_owner.mjs`: actual client projection and GUID scope extraction with native world/stats/record doubles. Checks profile replacement, delayed/duplicate/wrong-owner snapshots and missing-player retrieval. Read HAV3N before compiling. Not native transport/stat events or P2P qualification. Metabolism wire extension requires matching protocol11 files.

- `node Tools/GameBridge/test_metabolism_packet.mjs`: actual model/legacy+scoped codecs and bounded-string helpers; every-field roundtrip, length, legacy prefix/reset, truncation/oversized identity. Native framing/transport/pool doubled. Read HAV3N before fixture compilation; no game required.

- `node Tools/GameBridge/test_poi_placement.mjs`: actual exterior placement predicate with native world/roof doubles; clear and changed geometry checks. Read HAV3N before compilation. Native route/placement/full POI flow remains deferred.

- `node Tools/GameBridge/test_music_snapshot_scope.mjs`: actual music Receive/character GUID scope with native owner/world doubles; delayed old-character receipt reset refusal and revision handling. Read HAV3N before compiling. Native transfer/audio/P2P remain pending.

- `node Tools/GameBridge/test_resource_item_fingerprint.mjs`: actual resource fingerprint helper with native ItemValue serialization double; exact payload changes and failure refusal. Read HAV3N before compilation. Full resource transaction/native/P2P custody remains unvalidated.
`n- `self_splint_quality_deferred.json`: NOT RUN; requires explicit gameplay authorization and a prepared non-Paramedic disposable CodexTest character with known Medicine and no existing limb treatment. Uses genuine splint input and verifies consumption, new splint, positive healing base and no Trauma attribution. It does not certify the exact Medicine multiplier, denied/cancel paths, cast cleanup, save/load or multiplayer; see `_Documentation/MedicalTreatment/SELF_TREATMENT_SOURCE_HANDOFF_20261007.md` for those remaining checks.

- `ui_feedback_f_station_capture.json` and `ui_feedback_f_creative_capture.json`: NOT RUN. Capture-only visual regression flows for feedback f. Station precondition: disposable workbench open, no recipe search/category filter, Chainsaw and 4x4 Truck present. Creative precondition: Creative already open with a boosted-quality Iron Pickaxe available. No launch or save setup is included. Screenshot success is not feature success: inspect header/icon/tool previews, attribute/unlock/ingredient bounds, inline star and creative header/search styling. Drag/split/merge and station transfers require the explicit manual custody checklist in `_Documentation/UiFeedback_20261010f/REPORT.md`; these capture scenarios do not certify those transactions. UI gameplay testing remains with the user.

Station observation: `GET /station?x=N&y=N&z=N` reads a loaded workstation tile without input, mutation or forced UI synchronization. Returns queue capacity/count, jobs with native slot/batch/output/time/owner fields, output capacity and occupied output/tool/fuel stacks, plus accessed/burning state. Coordinate arguments are required integers; missing tile is an error. When `accessed` is true, compare against UI observations separately: tile data can lag the open UI. Close through real input before using this snapshot to assert persisted station state. Endpoint compilation is verified; live qualification is pending.

`expectStation` asserts a read-only tile snapshot, with mandatory coordinates and expected station identity. Example: `{ "expectStation": { "x": 14, "y": 45, "z": 995, "station": "workbench", "field": "queueCapacity", "op": "==", "value": 16 } }`. Numeric fields: queueCapacity, queueCount, outputCapacity. For tools/fuel/output, supply `item` and optionally zero-based `slot`; the comparison uses the matching stack count. Example tool check uses `field: "tools", item: "FuriousRamsayScrewdriver", slot: 1, op: "==", value: 1`. By default the station must be closed, because its tile may lag open UI; `allowOpen: true` explicitly accepts tile-only evidence. Missing data, wrong identity and HTTP errors fail the step. Synthetic helper checks pass; gameplay scenarios remain to be qualified.
`click recipe=ammoGasCan exact=1` selects the exact recipe identifier, avoiding similarly named bundles. Omit exact for existing substring behavior. Exact matching also applies to queued recipe identifiers.

Tool durability fixture (local disposable CodexTest only):
`stationtoolsetup x=22 y=44 z=997 slot=0 item=FuriousRamsayCircularSawBlade durability=0 confirm=CodexTest`
Sets an installed degradable tool's remaining durability fraction (0=exhausted,1=full). Requires a closed station and empty queue, exact item identity, explicit slot and coordinates. This prepares a test condition; it is not evidence of normal wear or crafting. Restore durability=1 after the exhausted-tool check. Normal transfers and crafts must still use real UI input.

- `npc_persistence_update_idle_smoke.json`: NOT RUN. Requires the disposable CodexTest save already in-game, guard ON. Observes an ordinary 35-second update/save interval for errors. This is only an idle regression smoke; it does not inject damaged saves or certify retry frequency, persisted NPC contents, checkpoints or multiplayer. Deterministic scheduling/failure-path evidence is in Tools/PerformanceAudit20261010/PersistenceTickFixture and _Documentation/PerformanceAudit_20261010/PERSISTENCE_TICK_FIXTURE.txt.

- `npc_social_update_idle_smoke.json`: NOT RUN. Disposable CodexTest already in-game, guard ON. Observes a70-second interval crossing the social timer; only an error smoke. It does not create relationships, prove dispatch, or certify networking. Extracted actual queue/flush/batcher tests and documented missing adapter integration are in Tools/PerformanceAudit20261010/RelationshipFixture and _Documentation/PerformanceAudit_20261010/REPORT.md.

- `companion_registry_sort_capture.json`: NOT RUN. Requires disposable CodexTest already in-game with prepared following companions and visible HUD. Capture-only native regression; compare companion ordering and rendered entries against the fixture setup. Does not prove full registry ordering or population performance. Exact comparator equivalence and isolated allocations are covered by Tools/PerformanceAudit20261010/RegistrySortFixture.

- `waitForGuardIdle: {timeout:45}` polls the actual enabled/busy guard state until idle for one second (1..60s bound). Fails on disabled or malformed state. Use after fixture preparation and before UI input; does not disable guard or prevent a later emergency reaction. Assert the target window again after waiting.

- `waitForWindowOpen: {window:"workstation_campfire",timeout:5}` observes the window until continuously visible for0.5s (timeout1..30s). It never resends activation. Use after asynchronous opening; retain expectWindowOpen after observation intervals to detect closure.

Performance alert regression: `Tools/PerformanceAudit20261010/verify_alerts_live.py` uses the authenticated bridge and real Tab/Character input in disposable CodexTest. `/ui/alertaudit` compares the old full-tree text projection against the optimized alert reader, including order. The script checks idle, crafting and character windows, three comparisons each, and new error logs. It is not an FPS benchmark or active cooking-alert qualification.


Paused station reload comparison: `Tools/GameBridge/test_queue_reload_snapshot_live.py` reads the prepared `LIVE_QUEUE_RELOAD_PAUSED16.json` baseline and compares a different live process in the same CodexTest save before station activation. It does not prepare or launch anything. Requires the supported saw at22,44,997, paused16jobs, normal save/quit/relaunch without Reset. Saves full before/after/differences before asserting. Scope is exposed native station snapshot fields, not hidden item metadata or multiplayer.

Saved-queue completion: `Tools/GameBridge/test_queue_reload_completion_live.py` resumes the supported saw at22,44,997 in CodexTest. Requires existing paused paid plank jobs and a valid blade; adds gas via give plus real shift-click, switches heat on, closes the window, and polls until completion (420-second limit). Asserts exact output from saved multipliers, retained tool snapshot, positive Construction gain, unchanged Cooking, and stable output/skills for12seconds after drain. Writes observations even on failure. Does not launch/quit or assert an independently calculated skill amount, hidden metadata, multiplayer, or tool wear.

Mixer tool transfer: `Tools/GameBridge/test_mixer_tool_transfer_live.py` requires a supported cementMixer26,44,997, empty queue/tools, player in activation range, no open UI, CodexTest. Supplies one battery; uses real recipe selection and shift-click to verify matching tool destination, warning removal, reverse transfer and warning return. Records failure observations. Does not place/launch/quit, test completed crafting, or replace visual screenshot inspection. Added but not yet run in game.

- `purge_information_reading.json`: NOT RUN. Prepared local disposable CodexTest in enabled Purge, English UI, empty belt slot1 and no pending automatic teaching. Exercises all nine real reading-item primary actions, consumption, title/window admission, screenshots and dismissal. This is distinct from the presentation preview fixture. It does not award milestones or certify reward delivery, journal disk persistence, reload or networking. Inspect all nine screenshots; then qualify journal rereading and reload separately. Do not execute before Purge source completion or in a human save.

- Purge source-first qualification: `purge_journal_dismissal.json` prepares a real manual-reading -> popup dismissal -> Journal body scenario. NOT RUN; English disposable CodexTest and visible Journal row prerequisites apply. It does not certify automatic teaching/reload/network behavior. Full real feature flows and acceptance evidence are in `_Documentation/PurgeReaudit_20261010/GAMEPLAY_QUALIFICATION.md`, alongside TEST_MATRIX.csv. Execute only after remaining source is finalized and compiled.

### Purge structured observations and fast scenario wrapper (source preparation)
Console rbpurgetestobserve emits one schema-1 JSON object from existing state: theme/release flags, current store revision, containing prefab/manifest/record, scanner projection and registered-marker count, lifetime HUD, and the local supply account. It never pulses services, forces chunk loading, activates rooms or grants evidence. Only local disposable CodexTest with Bridge enabled is allowed. Scanner Observe returns the existing valid projection; the command reads it only.
Scenario step expectPurge accepts checks [{field,op,value}] and compares multiple dot-separated fields from one observation. Missing fields or unsupported schema fail rather than silently passing. Tools/PurgeQualification/run_scenarios.py wraps existing real-input scenarios, checks Purge prerequisites before input, retains before/after observations and scenario output, stops on failure, and restores rbpurgetestsetup in finally. Use REBIRTH_BRIDGE_NOCHORES=1 when launching to suppress chores before setup runs. No new shared route and no vehicle command edits.
Example (after source closure/build and disposable Purge world load): python Tools/PurgeQualification/run_scenarios.py Tools/GameBridge/tests/purge_prerequisites.json Tools/GameBridge/tests/purge_scanner_cabin_entry.json. The cabin scenario still requires its documented native state; select an accessible POI for actual clearance tests rather than reusing the previously blocked door route. Authored POI hostile animals are retained; only ambient hostile animals are suppressed. Existing hostile animals must be cleared from the setup area. No tests executed for this new support yet.

### Exact stack shape
`{ "expectItemStacks": { "name": "cntGunSafeOpenWhite", "counts": [1, 1] } }`
compares the sorted multiset of stack counts across backpack and toolbelt. It rejects
one stack of two even though the total count is identical. Cursor custody is excluded;
use transfer/cursor assertions separately. An empty counts array expects no matching
inventory stacks. The packed-return scenario assumes no pre-existing white safes,
empty storage and the documented two installed stations; do not rerun it blindly on
an already modified save. This assertion does not inspect packed payload metadata.

camper_repeated_shift_roundtrip.json: stateful CodexTest regression for camper718 with one installed storage module0, two existing stations0/1, a spare cntGunSafeOpenWhite and campfire in the backpack. Runs three rounds of storage/station Shift-click install/remove (12 transfers), asserting exact backpack count deltas, pending=false, capacity=cargoSlots, and native lockOwner=-1 after every close. Preserves installed modules and existing cargo. Preconditions fail closed; no fixture reset or save deletion. This covers repeated Shift-click, not drag/drop or remote networking.

`camper_armor_plough_visual.json` prepares both vehicle mods on dismounted camper268 in disposable CodexTest, captures pattern/plain variants and inspects the mod groups/collider count/capacity. It does not drive, attack, repair, or test progressive damage visibility. Match the current local fixture entity ID before running it; an ID in another save is not authorization. The user performs driving/combat and entry acceptance. Final camera framing is a separate manual visual-setup step.

Camper progressive armor: camper_armor_damage_inspect.json inspects the existing CodexTest camper (268) at its current owner-controlled condition. The owner performs damage and repair, then runs this scenario at chosen thresholds. Console receipt must show armorRanges=54 and visibilityValid=True. This scenario changes no health or mods and does not perform combat. Full threshold arithmetic and index restoration run in the isolated CamperProtection/Tests fixture.

## Exclusive prepared batches (2026-10-18)

Use `prepare_and_check_batches.ps1` before launch. `run_prepared_tests.ps1` defaults to preflight; `-Run` uses one loaded session, and `-Run -Launch` launches once. `-Case`, `-Group` and `-Ack` select fixture-qualified cases. The batch owns an exclusive in-game lease, so standalone scripts cannot interfere. Native guard stays on. Manual test recording is available through `record_gamebridge_manual.py`. See `_Documentation/GameBridge/PREPARED_BATCH_TESTING_20261018.md` for commands, evidence, limits and the coverage ledger. A category manifest is a selection catalog, not proof all its historical fixtures can run together. The external respawn watcher is retired.

### Camper presentation regression — authored 2026-10-21, NOT RUN

`camper_presentation_regression_live.py` uses the parent-pinned authenticated session and exclusive run token, refuses any save except CodexTest, and saves every real input/observation reply. Parent must commit/push the integration before executing; this script neither builds nor launches.

Commands (Python runtime, substitute observed IDs/coordinates and fresh report filenames):

```
python Tools/GameBridge/camper_presentation_regression_live.py --phase duke --report <fresh.json>
python Tools/GameBridge/camper_turret_cards_live.py --entity <id> --report <fresh.json>
python Tools/GameBridge/camper_presentation_regression_live.py --phase manual --entity <id> --corner 0 --report <fresh.json>
python Tools/GameBridge/camper_presentation_regression_live.py --phase wire --entity <id> --relay <worldX> <worldY> <worldZ> --wire-slot 1 --swap-slot 2 --report <fresh.json>
```

Prerequisites: REBIRTH player progression. Duke phase tests actual native buff start; English visible pinned message absence and screenshot, then removes test buff. It does not fake a callback. Cards phase needs actual management open and four installed unreserved turrets; real clicks toggle twice, acquire each CCTV and exit using E; asserts native third-person view during CCTV and exact previous-view restoration. Manual phase needs management open, powered loaded chosen turret aimed into the prepared test backstop; real gui.LeftClick input consumes ammunition and advances native shot counters three times. Wire phase requires closed UI, a real wire tool already in the specified unlocked belt slot, another real accessible swap slot, an existing camper relay physically within reach and its measured world coordinates. It uses real lookat and Secondary clicks, observes native WireNode source wire_mesh, checks same-relay cancellation and tool-swap cleanup. It does not grant tool/items or call custom link authority methods.

Coverage limits: screenshots must actually be inspected for complete head, smoke absence and hanging-wire quality; listen to shot and native wire connection sounds. State flag/capture existence is not a visual/audio PASS. Current-aim firing verifies real shots, not hit precision. Independently position a target along the muzzle ray and record actual target health for per-shot accuracy, plus shotgun-pellet/automatic comparison. External-device commit, moving vehicle endpoint, world shutdown/reload, remote peers and native-progression retained Duke pin require separate prepared flows. Never claim those tested from this driver. Guard remains enabled; script reads surroundings, clears the test area and restores health rather than disabling instincts.

### CCTV actual head-point hit qualification — authored, NOT RUN

`python Tools/GameBridge/camper_cctv_headshot_live.py --entity ID --corner N --target ZOMBIE_ID --shots 3 --report <fresh.json>` (up to10 shots). Parent prepares disposable CodexTest current CCTV lease, all mounted automatic toggles OFF via real UI, loaded powered single-projectile ammunition, clear backdrop and one durable zombie outside15m from player and within ammunition range. Guard stays ON. Parent must ensure target remains outside guard engagement range during the bounded run. Script does not spawn, freeze, teleport or aim the target. It uses exclusively the authenticated GameBridge /cctvmouse raw-axis transport under the existing exclusive batch and OwnedInputScope. One finite bounded raw-axis sample passes through the same native sensitivity/inversion/smoothing/aim math; ordinary physical input is unchanged and overrides test input. No OS mouse APIs, runtime Aim or pose setter are used. Small samples calibrate sensitivity/inversion, then feedback aligns the native getHeadPosition world point with CCTV forward. Native gui.LeftClick pulse fires; each shot must consume exactlyone observed native round and lower that exact target instance's health. Before/after screenshots and raw replies retained. Dead target before requested count fails and needs a more durable prepared target. Head-point aiming plus health loss does not prove the native damage region was Head; no damage-region claim is made. Missing mouse response, target/view change or no health loss fails closed. No run before parent commit/push.

`camper_presentation_regression_live.py --phase duke-native --report <fresh.json>` is the native-progression counterpart. First use existing persistent world/progression options UI to prepare Native mode in disposable save; do not synthesize a mode CVar. After the phase, restore the actual persistent option through the same UI and reload before REBIRTH testing. No existing supported mode-switch tester command was found, so the script does not invent one. The native phase expects the real English Duke pinned notification after native buff start, then removes that test buff. Capture and inspect it; run REBIRTH `--phase duke` after restoration to verify opposite policy.


### Actual cell charging and internal turret supply — authored, NOT RUN

`python Tools/GameBridge/camper_charging_regression_live.py --plan <observed-plan.json> --report <fresh-report.json>` uses current authenticated parent session/exclusive token and refuses non-CodexTest. `tests/camper_charging_plan.template.json` deliberately has no executable fixture actions: supply exact current entity and nonempty real-input plans after examining actual UI/radial. Allowed setup action endpoints are native activation/key/UI click/drag/look/select/press/move only. Do not put hidden energy setters or fabricated source callbacks into the plan.

Prepare real installed populated solar and enabled battery banks with partly depleted genuine carBattery cells, generator OFF, automatic turrets/POI/devices OFF, no external relay wires. `prepareSolarCamp` must use real source/component/turn-on controls and enter Camp, then close the UI so the simulation is unpaused. Driver verifies all those observed prerequisites; no manufactured charge. Native settime prepares midnight vs noon. Per-component native UseTimes and MaxUseTimes must exist; decreasing UseTimes demonstrates actual cell charge rather than CurrentPower buffer alone. Midnight no-demand sample must remain unchanged; noon must show measured solar production and decreasing cell UseTimes. A night-only Travel/mount phase then uses genuine driver forward input for20seconds and >1m actual travel, requiring battery charge gain with no solar; dismounting at night must stop charging. Parent provides a clear level test track, slowForward default.15 (bounded native input), and actual nonempty mount/dismount UI actions.

Finally `openCctv` supplies genuine management/CCTV clicks with no external relay links; a real gui.LeftClick pulse must increase native shotsConsumed. This verifies a mounted turret's internal power admission without an external cable. It does not claim power for a missing/unloaded/unprepared turret or remote peers. Raw /energy and /camperturrets replies retained for every sample; source identity/component count/type/maxuse changes fail. Missing fields, no sunlight, full batteries, unplanned demand, held state or no actual movement fail instead of producing false PASS. sampleSeconds bounded10..60; default20. Initial native world clock restored in finally and held input released, but parent still owns final player/source/UI restoration. No automatic save/reset/relaunch. Persistence/reload, daylight weather variants and exact watt balance are separate qualifications.

Charging plan actions optionally accept `waitAfter` (default0.15seconds), validated as finite numeric0..15 before dispatching that action. Use8.5seconds on the actual `energyModeToggle` click before a following native driver activation, and again when returning to Camp before opening CCTV. This only waits for native transition; it does not advance or set the mode. Internal turret quick-shot pulse is0.09seconds and still asserts only positive native shotsConsumed delta.

Radial (native activation menu) steps, added 20261022:
- `radial` (existing, GET /radial) lists the open radial: `entries[]` with `commandId`, `selectionText`, `selected`; read-only.
- `{ "radialSelect": { "command": "talk" } }` (CLI: `radial select command=talk entity=ID`) holds Activate (E) on the crosshair target (or `entity=ID` / `x,y,z,block=1`, which it aims at first), waits for the native radial to open, hovers the entry with the virtual cursor, releases E so the game runs the command, and reports `selected`, `method` and `openWindows`. Match by `command=` (native command id, e.g. `talk`, `service`; block commands use their text), `text=` (localized label) or `index=` (menuIndex). It FAILS (HTTP 4xx, `ok:false`, non-zero exit) when the radial does not open or the entry is absent; the reply then carries `available[]`. `ok:true` proves the input reached the radial, not that the command succeeded: assert the window/buff/item afterwards.
- Single-command targets: an NPC declares only `talk`, so the native radial (XUiC_Radial.SetCommonData, one enabled entry) dispatches it on the Activate press frame and never shows. `radialSelect` detects exactly one enabled entity command, taps E and requires `expectWindow` (default for `talk`: `rebirthCompanionInteraction`) to open; reply `method:"single-command-auto-dispatch"`. A different single command, or no window to prove, is refused before pressing. Multi-entry targets (vehicles) use the hold/hover/release path.
- `activateNearestEntity` accepts `command`; for NPC classes (npc*, *Survivor*, *SDCS*) it defaults to `talk` and goes through `radialSelect`. Other targets keep the plain E tap unless `command` is given.

NPC gear observation (read-only, added 20261022): `npcgear [entity=ID]` (GET /npcgear) returns for each loaded human NPC: entityId, class, stableId, `rbNpcBirthEquipment` / `rbNpcBirthTries` / `rbNpcBirthPending`, toolbelt slots 0-9 and equipment slots (name, count, quality, useTimes, meta, encoded frame), bag used/capacity, lockpick count, and for slots 4/5 the live native frame next to the saved humanEquipment binding. Runner step `{ "expectNpcGear": { "class": "npcRebirthSurvivorManSDCS", "save": "name", "compare": "name" } }` asserts exactly one living NPC of the class, marker 1, tries 0, a weapon in slot 4 or 5 and no repeated item name across equipment + slots 4/5; `save` writes `out/npcgear/<name>.json`, `compare` requires the same per-slot summary.
