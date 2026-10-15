# Game Bridge observations and complete folder reconciliation — 2026-10-15

Audit only. No endpoint, game, console command, build, test, git or production modification. Prior seven GameBridge traces provide full caller/core/input evidence; no repeated credit. All17 remaining bodies read completely, bounded output; full current manifest below. Paths start Scripts/GameBridge/.

| Source | Lines | SHA256 |
|---|---:|---|
| RebirthPurgeAudioTrace.cs |79|DEF80E622A25636E7519D33D9C8F2DD575E9FBE0D247AA579955833D7A1EE0F9|
| RebirthGameBridgeConsumption.cs |58|9DCFDDA5A7FD57FBE085DF9EDC480A05D3F095218564A585AB53414899BB6D67|
| RebirthGameBridgeConsumptionCodec.cs |116|C7DBBE6427D451AACF13954A19BB243FF2DD778844029BB3F9C44B8B21E2A5AB|
| RebirthGameBridgeCamperProfileObservation.cs |206|C0B3EBF814FCA6A278F5FB345FC723EBB521151AD27EB3D62F0218661C7C7704|
| RebirthGameBridgeCamperRecipeObservation.cs |64|AC846A069A7B35ED2C3A022239E6674FEA71185C678AC819ECF1164DAD42D6F8|
| RebirthGameBridgeEnergyObservation.cs |59|EA637B2D1480A0928F04440FA51B7291448473E10457EB746D67C15C129D2350|
| RebirthGameBridgeEnergyMobilityObservation.cs |50|F9CD24A02ECDC78BF1A49FDB8393403156151D9BCAA516B6E4ED78BDE57E951F|
| RebirthGameBridgeEntityModelInspection.cs |392|B32546968BE0859A43191B0ACE50FEDD651C98605BB828196E8DA6E018D46595|
| RebirthGameBridgeFetchObservation.cs |173|708017F9168710BFFE1E6AB98592C8E4DF0F7FF39DCD1337289027DE7B34BBED|
| RebirthGameBridgeFetchStackQualification.cs |122|C910BCBF3447E9EF478187B6996A2E1075E0495E1C778DBAAAED4C72B198F2CE|
| RebirthGameBridgeGearObservation.cs |112|E144E55BF9F01F304D81E342FDCF4E5A521977CC32E5BAACDC3DCC6E73A8C77E|
| RebirthGameBridgePurgeTestSetup.cs |71|4B2659EA1066AA76A112A68705CE756E652D8FEE4E2C5B599D33D9749AB8926D|
| RebirthGameBridgePurgeObservation.cs |131|84EABC04240A5637148D877781B4869A74B68490405D153B52CBEC190CD554E9|
| RebirthGameBridgePurgeInformationPreview.cs |31|74E614FA6817AAF4D9EA64430BC31D0A734A8CE3451BEB07CEE9596D605E6D48|
| RebirthGameBridgePurgeDiscovery.cs |32|84841A123F8B36D9DEBB7BB9043DA95B3703C66FB86DDC9EBF5DD5ED54915584|
| RebirthGameBridgeRadialObservation.cs |72|3C6C0CE302EEB9DCFB938348222BE98FA423CB2B73C40B06CA000432A3CD5983|
| RebirthGameBridgeTargetCombatSnapshot.cs |114|A930413DBF3FE3EFDF191293BC98AADCA2578200D82DF3A144C78FD7A1918421|

17 complete source bodies1982 lines. Audio body conditional PURGE_QUALIFICATION: csproj Debug default RebirthPurgeQualification=true defines it; Release does not. Other16 outside Debug folders compile both. No compiled binary/runtime acceptance established.

## Install, command permission and topology

Existing core opt-in bridge gates HTTP transport. Handlers70–72 dispatch GET Gear/Energy/Radial;88 EntityModel;272 consumption state section;586 target snapshot. Synchronous main-thread dispatcher means these don't queue delayed physical actions. Native console classes are discoverable independently of HTTP and mostly explicitly require IsRunning+CodexTest and reject sender.RemoteClientInfo. Fetch/StackQualification permit a local client's remote world (owner-local evidence), while Purge/Profile require nonremote world. CamperRecipe permits remote world but local sender and computed owner recipe quantities; not server payment proof. ConsumptionCodec IsExecuteOnClient=true, requires bridge/main thread but no CodexTest name: detached values only, no ProcessPackage invocation. Native console permissions remain native defaults; not bypassed merely by class presence. All negative scope searches rg -uuu.

## Observation accuracy and explicit side effects

Consumption reads native inventory/buffs plus existing client metabolism projection, does not resolve/create metabolism state or consume. It expressly reports consumptionReceiptAvailable=false and says snapshot sequence is not receipt. Native item hash is over a detached clone codec. Client snapshot owner/creation fields included but Observe does not itself reject another owner snapshot; source repository fencing must support interpretation. No new stale snapshot demonstrated.

ConsumptionCodec clones foodHoney, adds probe metadata to detached item, roundtrips actual consume-result wire, rejects disguised/malformed/trailing empty after-images and checks six conditions; byte streams never ProcessPackage. Run reports pass only after predicates. It does not establish inventory application, network sender gates, save durability or real consumption success. No invocation performed.

GearObservation GET may Configure/clear RebirthGearTrace before inventory capture. This is a deliberate diagnostic tracing mutation despite read-only comment, not item mutation; a later capture refusal leaves tracing changed. It pins current world/primary/entity/main-thread, snapshots owned belt+bag/cursor, exposes exact offer/terminal/publication IDs and receipts without pumping offers. Recovery observation is only loaded entities and explicitly cannot prove missing/unloaded custody lost. Native empty positive-count ItemValue.None cursor is normalized in JSON only, raw fields preserved. No saving/uploading/disposition side effect in body.

EnergyObservation uses registered Observe, no Runtime.For grant, reports remote authoritativeKnown false and physics scope correctly via EnergyMobility. It calls vehicle.GetUpdatedItemValue and device/claim observation helpers; those downstream getters can update serialized representation/caches as previously audited, so comment alone is not a proof of zero representation side effects. No new admission/payment/save invocation here. Mobility directly reads exact current vehicle/seat reciprocal attachment and body fields, clearly labels remote client body versus server physics. No teleport/input/body setters.

CamperProfile traverses currently visible/native UI, correlates actual backpack slot and serialized payload/count rather than presentation index alone, lists explicit controller/frame vehicle identities and viewport state. Reads private selected recipe through resolved FieldRef; it does not invoke selection callbacks. Required-slot CanSwap uses detached tool stacks as admission query only. Bound1024 visible slots, though hierarchy/source/payload work happens before output cap and there is no global recursion-depth limit. No live drag, UI opening or inventory writes. CamperRecipe resolves unique canonical recipe and finite native modifier quantity/time, checked output total, actual workbench authored area properties; numbers are calculated expectation, not paid/produced receipt. Neither qualifies persistence.

FetchObservation reads genuine initialized expectedItem only, never SetupExpectedItem/Bind; full native item payloads, quest metadata, journal phase/state and storage access policy captured. Storage payload can be observed before QuickStack.CanUse returns refusal; this is a local opt-in diagnostic view, not an ordinary remote item transfer permission. Pair comparisons are quadratic in package count and response unbounded apart from physical inventories; no performance claim. StackQualification clones genuine current packages, requires initialized matching objective+ordinary control, checks all native stack predicates on detached objects, compares live-source and detached before/after witnesses. Not-qualified emits explicit failure marker after JSON; not silently pass when fixture absent. Proves predicate behavior only, not live custody transfer/network receipts.

RadialObservation reads existing already-filtered entries/selection/icons<=64, never opens/rebuilds/admission callback. Includes hidden/stale context and visible flags and qualifies them; no claim that mere context proves an open camper radial. TargetCombatSnapshot pins current world guid/reference and exact zombie reference before/after, weak object tokens not persistent identity. Converts nonfinite numeric fields to unavailable, explicitly events incomplete/no damage observer and movement request not intent/render proof. This is stronger evidence hygiene than broad Combat HP-drop attribution, no mutation.

EntityModelInspection walks one exact current zombie hierarchy, uses shared materials/mesh/collider material getters rather than instantiating material/mesh, bounds1024 transforms/4096 components, reads animator/joints/body/textures. No writes or renderer visibility changes. Bone/material/texture arrays aren't individually bounded; it may exceed core64KiB HTTP response truncation, yielding invalid diagnostic JSON (same core transport cap, no new game-state defect). No lifecycle yield, server/remote representation still only local observed hierarchy.

## Purge setup versus observer

PurgeObservation reads published world/map/supply and native manifest volumes without forcing chunks, resetting rooms or awarding progression. It purposely avoids history.Get recovery mutation. Observed pending maps/volumes are live current source, not future/save proof. InformationPreview requires POST/local CodexTest/confirm/no modal and opens a synthetic unauthored journal entry; no guide-read/reward mark. Discovery status read-only, preview/replay/cancel explicitly POST+confirm and calls actual maintenance, not a scenario assertion; replay can mutate discovery via that previously audited service.

**PurgeTestSetup is an actual fixture, not observation.** Explicit on local CodexTest saves prior Needs/Agenda/Explore/Heal/Supply/Glance flags, installs AnimalFilter Harmony once then suppresses hostile ambient animal decisions for exact owned world only. It disables those flags while leaving GuardEnabled unchanged. It does not award kills/clear/supply. Off restores saved flags; no automatic world/disconnect restoration hook in this source. Leaving setup on across world change makes animal suppression false for new world but globally disabled automation flags remain until off; on refuses a different world until restore. This conflicts with standing heal/instinct expectations and must remain unexecuted absent explicit user direction. No operational change made here.

AudioTrace compiled qualification body only; requires local nonremote CodexTest/running bridge. Bounded1..120s and4096 samples at0.1s, exact original world and generation, finally resets running; stop increments generation. It reads AudioSource clip/volume/listener state without playing sound or changing mixer. Clip isPlaying/timeSamples is source observation, explicitly not human listening or attribution. No audio playback/test performed.

## Reconciliation and remaining dependencies

Current Scripts/GameBridge contains45 .cs files. Explicit full-body ownership across SECURITY(4), REMAINING(Player1), UI_WORLD(2), COMBAT_STORAGE(2), POI_DEPENDENCIES(5), FINAL_SOURCES(14), this OBSERVATION(17) totals45. Current hashes match those manifests at reconciliation. GAMEBRIDGE_FULL_READ_MANIFEST.csv records exact source-to-full-trace mapping and current hashes; mapping uses actual full-read manifest groups, not filename mention or hash-only search. No remaining source paths in this folder without full-read body coverage. Audio Release exclusion recorded separately, not treated as an active Release command.

Coverage is source review, not functional qualification. Remaining external dependencies include native console/package discovery/permissions and target subsystem Observe/getter implementations already separately owned by parent/other agents. No new cross-owner exploit or production gameplay corruption found in these observers. Earlier confirmed setup loss, timeout/input ownership and diagnostic integrity findings remain applicable; this report adds exact mutation classification and closes folder source coverage only.
