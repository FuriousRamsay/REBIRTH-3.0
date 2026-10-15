# Game Bridge agenda, fixture and diagnostic sources — 2026-10-15

Audit only: no game, endpoints, console commands, tests, build, git or production edits. No guard settings changed. Prior GameBridge traces reused for core full body, HTTP authentication, Combat/World/Needs callers and owned input implementation. rg -uuu includes ignored sources. Title identifies this tranche, not proof that every GameBridge source has been reviewed.

## Full-read manifest

All paths below begin Scripts/GameBridge/. Entire current source bodies read, no truncation.

| Source | Lines | SHA256 |
|---|---:|---|
| RebirthGameBridgeAgenda.cs |124|5738108574FABDCA82FF9069B2073949F3C4062796251B76EC5074F1C127112B|
| RebirthGameBridgeLootBags.cs |141|FCF9FFD936E1AA9E779DEF1293D008277D6B9C8EA83857437ECA853D75DA4148|
| RebirthGameBridgeTerrain.cs |284|39ECD30749B0C27567C51E95C348F8B0B5950167A6BFAD5C658AFCF1144D0149|
| RebirthGameBridgeGearFixture.cs |46|9463F86E640726C75F6AC7C7E49C1F2FF942AD840A83A20EADA165E982B32338|
| RebirthGameBridgeStationToolSetup.cs |40|1C45A462D01A439543974A1B2B59E29B7626B1676B22AFB3D0E26004C487024B|
| RebirthGameBridgeNeedsSetup.cs |32|6D0027F7B0972DECF3BBE19408EC36A086922A0624DF569A0730B3FC55F22D14|
| RebirthGameBridgeSkills.cs |185|C7952620EA678275E4767CB97F6197C29AAC754D788DBB941E070EC54F73042A|
| RebirthGameBridgeVehicleSequence.cs |283|A015EB483CE1BB6B9865A9460ADADF19C3D2E71608D62D73CBABF4828765D68B|
| RebirthGameBridgeVehicleBounds.cs |25|7DFE97431C2AB0DBB2AC0D0D7F1F6BF7F7383949F099AD9A97A70059205DFCA8|
| RebirthGameBridgePoiTransferPhase.cs |88|C9241BBC43A770B38051E7134C9C713A2FBDF376E056F8266CD3154512E174D3|
| RebirthGameBridgeLog.cs |118|B3F3456F8FA821455E180788D8D108D2786EF3F5911EF8FF4DBE948570B31710|
| RebirthGameBridgeGait.cs |132|9EE9553CB7108BC86C2874545E35B38CD5C235D4EDB92AC1398C6292850EE0F6|
| RebirthGameBridgeFightRecorder.cs |62|A328AD6187CD9CD08195A069610DCF4D55CA2C6650F2913414F8554914C669C4|
| RebirthGameBridgeMeleeProbe.cs |180|8900E65EF334864A9EC25C311CF156140EA2A9C84BA03035B7911B81CE5946C0|

14 full compiled sources,1740 lines. No native full-source credit or execution qualification.

## Actual installation and scope

SDK compiles these outside Debug folders in both configurations. GameBridge.Start installs Log/Input/MeleeProbe/Pump after explicit bridge process option (core90–93, prior full body reused). Agenda is called only via opted-in GuardTick when quiet/request age>3/OpenCount0; LootBags via /lootbags, postfight humanStyle auto-loot and World. Terrain computes decisions only, no native mutation itself. MeleeProbe installs explicit Harmony patches through Start; attributes not relied on. It enables skill evaluation diagnostics and logs hitTarget/HarvestOnAttack globally once installed, while Raycast rows require local attacker and Target. Stop does not uninstall these patches/log callback or reset static recorder state (prior core), so diagnostic overhead can survive listener stop in same process. No independently active production automation found in these files; this differs from unconditional POI crate marker XML in prior dependency trace.

Handler dispatch: /gearfixture at Handlers69; Combat59–66 routes loadout/skillsetup/stationtoolsetup/setblock/testflag/craft. Restore needs=1 explicitly invokes NeedsSetup at Combat2747. ConsoleCmdRebirthVehicleSequence and ConsoleCmdRebirthCamperState rely on native console discovery and retain explicit running bridge/local CodexTest checks. No permission-level override in VehicleSequence; native console permission policy still applies. Calling via authenticated /console is not the sole possible native console route.

## Agenda and loot bags: confirmed weaker evidence/lifecycle

Agenda crafting/cooking uses native UI and actual resources. It counts every inventory slot rather than owned toolbelt bound, so dummy/expanded hidden slots may influence stock decisions (diagnostic heuristic, no grant). Craft reports success for any positive matching item delta, not requested3; count-up does not first normalize existing batch count. Cook confirms any text COOKING in opened station window rather than exact requested dish/queue identity. No deadline/request/current-world fences after yields; comment that any agent command takes over immediately is not implemented by these coroutine bodies. They use shared unowned cursor/actions and normal-tail cleanup, extending established concurrency/timeout family, not a new production authority bypass.

LootBags.Routine serializes through static busy, but wait is unbounded and busy reset is not finally. A nested exception can wedge all subsequent loot requests. Run bounds240s, death and14m danger; neither waiting nor active run propagates outer request abandonment/world change. Static Spots are plain positions, not world scoped. OpenBag permits generic interactionPrompt text Loot as an alternative to exact bag entity id; subsequent LootOpen only checks window IDs. After3s it presses R and counts the currently open LootContainer empty, without proving that container is the intended bag. Concurrent UI/user action or another loot target can therefore be operated/count as the selected bag. Native UI remains the transfer/permission boundary; no direct mint or demonstrated loss. Unlike PoiStorage, no combined-content fingerprint/conservation witness. It deliberately preserves encumbering loot and does not discard inventory.

## Fixtures: intended setup versus partial failure

GearFixture requires POST, local nonremote CodexTest, confirm=CodexTest and eligible character. It may intentionally replace immutable Origin.BackgroundId through reflection; records Touch and owner snapshot but background branch does not immediately SaveIfDirty. Attribute branch finite-bounds current/potential, Touch, SaveIfDirty (return ignored) then owner snapshot. Not a native skill award, not acceptance proof. StationToolSetup likewise POST/local CodexTest/confirm, finite0..1 durability, exact loaded closed empty-queue workstation/occupied matching tool slot; clones Tools then setter. It intentionally rewrites tool wear for setup, does not pay resource or qualify workmanship. NeedsSetup requires authoritative metabolism/CodexTest, explicitly clears ingestion and fills stats/energy, exposes save refusal in JSON and refuses normal restore continuation; changes already live on refusal, no rollback implied.

Skills.SetupSkill checks CodexTest/confirm/nonremote/eligible and finite[-100,100], writes floor+fraction, saves and returns saved flag. Skills.Loadout has only primary-player/HTTP gate, no CodexTest/confirm restriction. It moves belt weapons into bag incrementally and can return failure mid-loop, after earlier moves and before CallOnToolbeltChangedInternal. Unknown requested weapon/ammo is validated after moving old belt items (and weapon may already be granted before invalid ammo). Thus failed setup can partially mutate inventory and leave normal refresh tail uncalled. Existing source /give similarly intentional grant; classify failed setup behavior and broader disposable-save guard gap, not gameplay exploit. No transaction/rollback expectation offered.

Craft uses open native crafting group and exact workstation-area match, native queue cancellation, and optional stock grants before native AddItemToQueue. Ingredient stocking is incremental and unchecked multiplication ing.count*count can overflow for extreme positive count, so errors can leave partial test stock. Native AddItem/grid behavior for negative overflow not newly resolved; no production duping conclusion. SetBlock intentionally calls native SetBlockRPC, optionally with player entity to create actual provenance, default unowned. Testflag changes process static farming water bypass, no reset here. All are bridge diagnostic setup effects; core itself does not enforce disposable save. Do not treat setup writes as proof of ordinary gameplay success.

## Vehicle sequence/controller and reporting

VehicleBounds accepts absent bounds or explicit finite strict extents<=80m. Sequence requires local CodexTest, running bridge, exact mounted driver/entity/RB and no zombies within80m. It **requires all guard/bleed/heal/reload/needs/agenda/explore automation off**, which conflicts with current AGENTS.md guard-on rule; this audit did not invoke it and must not recommend running it without an explicit user override. Runtime scope has exact attached driver, run GUID,10s renewed supervisor lease, health/quality nondecrease, speed<=16m/s, displacement<=65m and optional safe rectangle. It supplies real vehicle actions, captures10Hz samples and screenshots, reports CAPTURED_NATIVE_INPUT_SEQUENCE_NOT_ACCEPTANCE; no visual/horn acceptance claim. Finally clears running/runId and writes result. It does not check IsRunning within ongoing admission predicate (only start/control), so listener stop alone is not immediate cancellation; lease eventually expires. Finally serialization dereferences vehicle/RB and writes filesystem without catch, so destruction/I/O failure can lose report while running already cleared; owned input scope disposal still releases actions. No native physics safety test executed.

CamperState requires local CodexTest/running bridge/non-dedicated, nearby camper and native AllowActivationCommand(service). Runtime.For and GetUpdatedItemValue observations can materialize/update runtime representation; it does not install/remove/debit/save modules. Report lists native slots, queues, cancellation checkpoint readiness, metadata presence; explicitly acceptance=false. Native receipt correctness remains previously audited camper services, not established by successful report.

## Dormant transfer helper / diagnostic accuracy

rg -uuu across Scripts finds PoiTransferPhase.Capture only at declaration. No active storage consumer currently creates it. It fingerprints exact original arrays/stack/value refs/counts/serialized keys, records awake threat versus forwarded/fault/cursor, Finish permits retry only unchanged and never forwarded. Strong proposed historical proof is dormant; do not credit current PoiStorage with it or infer an installed retry behavior.

Log locks bounded20000 ring, monotonically increasing seq, severity and filtered last-limit reads; no unregister/reset. GaitTracker has2.4s samples and bounded lag analysis, static learned period by walk type; active Combat Add precedes Predict, so empty sample index not a proven active exception. Terrain reads GameManager current World, not passed player's world; block-grid whole-column approximations ignore exact partial block collision shapes and water only penalizes route (Probe Water does not set Safe=false). Negative/NaN length can perform no samples and return Safe=true; actual movement still bounded by other controller logic, not a guaranteed collision-free contract.

FightRecorder uses single static writer and second-resolution filenames: overlapping Begin closes predecessor, same label/second overwrites, shared pending events not cleared Begin/End, locale-specific decimal ToString can corrupt comma CSV layout. MeleeProbe records lists without capacity until Take, retains Target/static writers across sessions; human_trace/swings streams never disposed here and use fixed names. Probes observe ray success and entity target, stronger than raw HP deltas but still not an authoritative award receipt. These are diagnostic data integrity/resource risks, not multiplayer cargo or combat authority defects.

## Actual remaining scope

This tranche does not claim remaining observations fully read: RebirthPurgeAudioTrace, Consumption/ConsumptionCodec, CamperProfile/RecipeObservation, Energy/EnergyMobilityObservation, EntityModelInspection, FetchObservation/FetchStackQualification, GearObservation, PurgeTestSetup/PurgeObservation/PurgeInformationPreview/PurgeDiscovery, RadialObservation, TargetCombatSnapshot. Need reconcile other agents' current full manifests before taking them. No scan-only credit.
