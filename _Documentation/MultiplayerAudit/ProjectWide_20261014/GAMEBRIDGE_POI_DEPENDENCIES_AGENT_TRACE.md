# Game Bridge POI dependencies — 2026-10-15

Audit only. No game/endpoints/commands/tests/build/git or production edits. Prior GameBridge traces supply core HTTP gate, input scope and Combat/World/PoiStorage consumer evidence. rg -uuu includes ignored source. Guard behavior untouched.

## Full-read manifest

| Source | Lines | SHA256 |
|---|---:|---|
| Scripts/GameBridge/RebirthPoiCrateRecovery.cs | 199 | 2481EFE9F36D4924FEE536E5EF94AF7E40F5EF088095EAA9424487333B9FFEB8 |
| Scripts/GameBridge/RebirthPoiCrateLedger.cs | 189 | 8B59A2045A466F26EE9D18FBE988108CCCD84CDBBDEE75AA9D9F93D76C6D1652 |
| Scripts/GameBridge/RebirthPoiCrateIdentity.cs | 223 | 8B5C6F0C44245C220C70C225B8761B628EDED34C57961F4BC1A559C101220982 |
| Scripts/GameBridge/RebirthGameBridgeNeeds.cs | 910 | 7504A13DF837553EC49CEE33116E5A9493AB3BCA9D1816455972FE34B4574352 |
| Scripts/GameBridge/RebirthGameBridgePath.cs | 327 | 0202AECF06E7F6368AAB9F2ABFA3076BC558C9BAC4795634565C942A820B14EF |

Five new complete compiled sources, 1848 lines. Initial combined output truncated a boundary; Path end and Needs beginning were read again explicitly. Full XML Config/_GameBridge/blocks.xml 6 lines SHA256 35826EDD1A4BE1B687F38C080D54F812FC9A2ACB74E1AA0855C26DE989DDBC41. Shared RebirthDurableFileCommit.cs50 also full-read but already BOSS_EVENTS_AGENT_TRACE full same hash 2A40E5CA425BF0592BFA22A39F96CF80016202267849F10501299C0434B13FF8, zero new credit.

Native TileEntityComposite.cs selected OnBlockAdded and serialization body only, SHA256 D72F13D696C96667DD2D52F33AE29937CEB1940C8B140E82A1D37714098A2443 in existing MultiplayerAuditNative20261014; no full type credit. Target assembly provenance as prior trace.

## Production versus diagnostic activation

HTTP automation and Needs.Tick/Path actions require the opted-in pump from prior trace. Their supply=true behavior is intentional diagnostic setup: ordinary Prepare may add two missing food/water/bandages to bag through checked AddItem; owned Prepare explicitly never supplies regardless passed supplyItem (Needs140–249). Do not classify this opt-in assistance as a production item-mint defect.

**Marker integration is unconditional production configuration.** Config/blocks.xml:3 includes _GameBridge/blocks.xml; its append adds TEFeatureRebirthPoiCrateIdentity to every cntWoodWritableCrate CompositeFeatures. OnAdded assigns a fresh GUID on server when native Parent.Owner exists outside edit mode, with no -rebirthbridge predicate. Copy/upgrade/reset clear identity; Read restores it. Native TileEntityComposite.OnBlockAdded assigns Owner before invoking feature OnAdded (404–409). Native write emits every feature hash/payload for disk/network (630–650); current v18 read uses size-delimited feature hashes, missing new features default rather than inheriting an ID. Existing saved crates lacking the marker payload therefore cannot become recovered owned placements merely by matching coordinates. This is a real added native serialized footprint on ordinary crates, not dormant code.

Binding/recovery NetPackage ProcessPackage handlers likewise have no bridge-enabled gate. They can run on production server when native package dispatch routes their types. ToServer direction, nonremote world, Sender and ValidEntityIdForSender constrain requests. Marker binding also checks alive player, exact placement GUID, native persistent PrimaryId matching Parent.Owner, distance<=8m, POI distance<=12m, crate block name, and prevents reassignment to a different POI. Recovery returns only this native owner's scope. No cross-owner grant or crate inventory mutation found. Native package discovery/registration mechanism not newly full-read here; prior project network installation applies, so distinguish handler reachability from demonstrated runtime packet delivery.

## Durable binding ordering and recovery

TryBindServer calls LedgerService.Reserve before setting poiId/origin/size and SetModified. Reserve writes immutable owner+POI id/origin/size+position+GUID expectation and verifies readback before true. Replay of matching bound marker reserves/confirms same expectation, never reassigns. A save refusal leaves native binding unchanged. An expectation published before interrupted native bind is deliberately recoverable by matching owned GUID. Actual native chunk save remains separate; expectation is not an item inventory receipt.

Ledger service is server-only, keyed to exact world reference plus save path RebirthData/GameBridge/PoiCrates.xml and persistent PrimaryId (local fallback only). Ledger gate serializes reserve/snapshot. Bounds128/scope,4096/world,4MiB; duplicate GUID conflicts and same-scope coordinate conflicts refuse. Position x/z is within POI exterior12m (long arithmetic), sizes positive<=2048. Append-only unresolved expectations block silent replacement/recreation; this can reach capacity intentionally and requires explicit external recovery policy, not silent garbage collection.

Save writes complete temp, flushes(true), shared helper moves initial file or File.Replace with backup and no primary deletion, then parses and compares published contents. Dirty intent remains memory on failed publication; duplicate reserve and snapshot retry Save. Load unions primary/backup/temp only when schema1 and all records consistent; corruption/unknown version/conflicts refuse all custody. Primary missing while backup exists refuses because latest intent cannot be proven. Failed initial temp can be recovered without primary when no backup. Sticky loadFailure refuses until store recreated; safe availability loss, not cargo erase. No load migration or schema downgrade.

Remote recovery has GUID correlation, cap16 pending,5s wait, finally removal; response pins original World/player entity identity/POI geometry and rejects late unmatched replies. Packet count<=128, exact16-byte GUIDs, unique IDs/positions and validated scope. Client returned owner placeholder is not authority; marker native local owner check still required by consumer. Request coroutine itself only checks player.world/death while waiting, not GameManager current world, but consumer RefreshExpectations revalidates current-world/primary context before consuming results (prior complete PoiStorage read). No stale recovery mutation shown.

## Needs physical custody and remaining weak paths

Owned Prepare/Drag pin exact arrays, source object, source count+serialized key, empty target index, current displayed source/destination coordinates, cursor fingerprint and witnessed deposit. They reject swaps/merges/changed grids and propagate unresolved cursor custody to parent; no speculative inventory rewrite. Owned Equip runs guarded scope and reports false on native re-equip failure. This closes the strong storage dependency from prior trace.

Ordinary guard/heal/meal and production-test /use use unowned Equip/UseHeld/Prepare. Equip after WaitHandsFree may force OnSlotChanged; completion validates selected slot and item name. UseHeld then separately waits for free hands and presses current Primary without rechecking selected item/name (251–267). Concurrent user/HTTP/other automation can change held item between successful equip and press, so wrong held action is possible; no occurrence demonstrated. UseHeld reports consumption from aggregate same-name count drop OR any stomach volume rise, not an exact native dose receipt. This is diagnostic success-predicate weakness, not evidence that native metabolism accepted an invalid dose. Unowned Prepare uses global cursor lock/drag with cleanup only normal tail; this extends prior unowned UI lifecycle family. Owned storage does not use that weaker overload.

Meal projections use actual metabolism pending/stomach values, bounded10+4 uses,85%full cutoff and native consumable definitions; supply additions explicit/logged. Static digestion estimates/timers have no world reset here and can influence another session's meal decisions. No durable player stat rewrite in these helpers. Native consume remains the item action.

Cooking rescue finds ready native queues/output and clicks real TAKE. It verifies only station no longer ready after click, not item count/receipt; station removal/burning/concurrent collection can satisfy this without this player receiving food. It does not pin exact opened station feature or owner before clicking visible TAKE. HTTP/pump opt-in required; native UI remains permission boundary, no permission bypass proved. Fallback nearest station scans (2r+1)^2*13 blocks synchronously radius60, a potentially expensive diagnostic scan, no performance measured.

## Path integration and limits

Plan uses collider floors/capsule checks, bounded7000 A* expanded nodes, doors as native activation waypoints,1.1m rise/3.2m drop and diagonal orthogonal checks. It never teleports or writes blocks. StandSpot picks floor near block height. Unowned FollowPath (World navigate/loot) has timeout/death but no world/request cancellation and global MoveForward release; same established timeout/concurrency family. Owned FollowPath/GoTo used storage have nested guarded scopes, parent failure/cursor propagation and finally done(false) on interruption. Admission rechecked before camera/input after door yields.

Arrival uses horizontal distance only, even final node (210–212/278–280). A player below/above final floor at same x/z can report arrived without required elevation; with a single-node snapped path loop starts at1 and immediately succeeds. Native activation and storage exact-target checks later can still refuse, so do not promote to item custody failure. No performance/game test executed.

## Remaining dependencies

No new cross-owner exploit or native item-mint defect established. Strong durable expectation-before-binding contract confirmed. Remaining exact native gate if needed: NetPackage registration and TileEntityComposite FromClient sender/owner validation; this feature Read explicitly ignores client identity fields but parent native Owner deserializes separately, so cannot claim complete owner immutability from feature alone. Agenda/LootBags/Terrain plus fixture/controller sources still need independent full-read manifests. Preserve native owner architecture and opt-in guard automation.
