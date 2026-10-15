# Game Bridge combat and physical storage trace — 2026-10-15

Audit only; no endpoints, game, tests, build, profiling, git or production changes. Guard behavior left intact. Reuses GAMEBRIDGE_SECURITY_AGENT_TRACE, GAMEBRIDGE_REMAINING_AGENT_TRACE and GAMEBRIDGE_UI_WORLD_AGENT_TRACE caller/bootstrap/input evidence. No repeated source credit for those sources. Searches use rg -uuu including ignored source.

## Full-read manifest

Entire current bodies read in bounded sequential output, including blank trailing lines:

| Source | Lines | SHA256 |
|---|---:|---|
| Scripts/GameBridge/RebirthGameBridgeCombat.cs | 2817 | 0C5383621963B580B61572DB3E0736923D58A32ECA80CB77F8414BE9E498976A |
| Scripts/GameBridge/RebirthGameBridgePoiStorage.cs | 1022 | 37BB272924BEC3D5A9B2C610D180017F56DD5F604DF8FBA4866F011EF9F93D16 |

Two newly full sources, 3839 lines. Native Bag.cs AddItem method only read (242 onward), SHA256 E5DFF778263341CE0372983B9D0A3CA79F52981B8EA4CB2CCA49A0DA03188A2B; no full native type credit. Existing target assembly provenance remains FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705.

## Activation, topology and intended behavior

SDK includes these sources in Debug and Release; neither lives under omitted Debug directories. Prior core trace establishes explicit -rebirthbridge process gate, loopback token HTTP admission and pump installation. Core pump line360 invokes BleedReflexTick/GuardTick only while CachedState ingame. This is local primary-player test automation, not a remotely exposed multiplayer command permission bypass. No dedicated primary player means handlers refuse. Mutation/setup endpoints intentionally give, spawn, restore, clear enemies, alter spawning/speed, god/fly, mount and directly drive vehicle velocity. They must run only against the accepted disposable environment; transport itself does not enforce save identity (prior trace).

Fight selects real input actions; native health/ragdoll/swing state, crosshair, stamina dips and melee probe feed reporting. Controlled allies are excluded initially and released if allegiance changes mid-fight (710–719); cleararea also excludes them. Escape/backoff checks threats and terrain. Arrival scan, healing, reload and needs remain enabled as intended. /give checks backpack acceptance before replacing an occupied toolbelt slot (2801–2806), stronger than /treat below.

## Confirmed source failures / qualified lifecycle risks

1. **Treatment setup can destroy displaced inventory.** POST /treat defaults supply=true (1940). Missing medical item path clones last owned toolbelt stack into bag but ignores AddItem result (1963–1965), then replaces that stack with two supplied medicines. Native Bag.AddItem returns false when move forbidden or grid insertion fails. With a full incompatible bag, displaced stack is lost. This is a reachable disposable-test setup defect, not a regular production medicine producer. /give has an explicit refusal for the corresponding case. No execution performed.

2. **Request cancellation coverage differs by routine.** Fight loop tests req.IsAbandoned (707) and generation (667). Flee checks generation/death/threat/time but never req.IsAbandoned (1783–1808); treat, pillar, use and direct-drive likewise lack request abandonment. MaxSeconds/seconds can exceed the core HTTP wait, so accepted automation continues after timeout. Fight can itself spend time in nested HealRoutine/SelectSlot/PillarRoutine before its next abandonment check; no request predicate is passed into those children. This extends the prior timeout family with exact combat paths.

3. **Generation cancellation does not confer input ownership.** Superseded fight unconditionally releases global MoveForward/MoveBack/Run/Primary/Secondary (1469–1473); flee releases MoveForward/Run (1810–1811). Successor coroutine can already be holding those actions. Same family as prior unowned WalkTo cleanup; owned input scope elsewhere explicitly avoids this. No strong world/current-primary fence inside fight/flee themselves. After world change, exact effect depends on native coroutine lifetime, so stale-world mutation is a hypothesis, not demonstrated.

4. **Busy counters are not exception-safe.** Fight increments activeRoutines/fightsRunning before several dereferences and nested yielding routines; decrements only normal tail (1476–1477). BleedReflexRoutine, Busy, Watch, healing and GuardReact similarly lack finally. A coroutine exception can leave guard busy and bleed reflex suppressed. Actual throw occurrence is not proven; source exception recovery weakness is confirmed. PrepareHealRoutine has the same pattern but rg -uuu finds declaration only: its older unowned cursor path is dormant, not credited as active.

Fight reporting records target HP drops as hits (750–752), without attacker attribution; burning, allies or other players can contribute. It infers kill from dead/dying target, not sole player cause. The incident result therefore measures the automation's observations, not authoritative combat attribution. Traces use second-resolution filenames (1754), so same-source fights within a second can overwrite diagnostic output; no persistence gameplay claim.

## Physical storage conservation and recovery

World.LootPoiRoutine caches storage per POI id and clears the dictionary on world reference change (World650–652, reused full prior body). Storage additionally binds custodyWorld, exact current primary player, POI id/origin/size. Refresh validates bounded snapshots, duplicate IDs/positions and conflicts with remembered custody (30–74). Published expectations are required before PlacedLoot admits cargo (399–412); a matching crate at coordinates alone is insufficient. Native marker owner, GUID and POI matching authorize reuse. Replaced/unloaded/missing crates remain unresolved; no recreation or virtual cargo grant.

Pending placement recovery retains original feature/parent/marker references, waits for identity, bounds route/time, rebinds only matching owned GUID and confirms a durable expectation snapshot (128–234). Recovery scans loaded exterior chunks and reports incomplete chunks rather than assuming absence (237–282). Exact publication and owner/network/store behavior belongs to dependencies below and is not fully verified by this consumer.

Deposit acquires owned input scope with world/primary/custody/threat admission (671–719). Displaced supplies are fingerprinted and excluded from cargo, as are locked backpack slots and crates. CraftCrateOwned uses real recipe UI and checks admission after cursor movement and UI yields. Prepare explicitly passes null supply, disabling test item grant (774–775). Placement rechecks exterior/air/ground, exact held crate readiness and native item decrement; captures feature/parent, waits for durable GUID/bind ACK before cargo (810–879). Missing witness fails closed without manufacturing a replacement.

Storage opening checks exact feature and native LootContainer. Before shift-click, snapshots combined backpack+crate counts keyed by full native ItemValue serialization except ordinary metadata-free stack supplies (493–511). CanAccept rejects native-merge candidates with incompatible conservation keys. Click callback rechecks exact container, visible source coordinate and unchanged combined counts after cursor travel (939–952). Afterwards positive bag decrement must equal crate increment and combined key totals must match (966–972). Bounded 32 passes/200 moves/24 scrolls report remaining cargo, no false success. These checks detect concurrent native mutation; they do not overwrite either inventory to roll back. On conservation failure, actual cargo stays wherever native transfer left it and caller receives failure. No durable per-stack receipt/transaction is invented here.

Owned cursor failure is propagated, retry disabled, and original held item restored by serialized identity including explicit wear receipt adjustment; death/threat/context changes refuse further work. Scope disposal protects input cleanup. A request deadline is not directly an input predicate in this consumer; the prior World unowned outer request can continue storage after HTTP timeout while same player/world stays valid. Preserve this distinction from its strong custody fencing.

## Next exact dependencies / limits

Full pending consumer dependencies: RebirthPoiCrateRecovery.cs (request correlation/server snapshot), RebirthPoiCrateLedger.cs (durable expectation atomicity/recovery), RebirthPoiCrateIdentity.cs and TEFeatureRebirthPoiCrateIdentity.cs (placement owner/native persistence/binding ACK), plus Needs/Path real-input custody helpers not yet full-read in these traces. Need establish server binding writes expectation before ACK and load/save/reset failure handling. No native storage UI producer full read in this tranche, no runtime conservation proof, no cross-owner exploit promoted.
