from pathlib import Path
import csv,json,hashlib,collections
root=Path('.')
out=root/'_Documentation/PerformanceAudit_20261010'
rows=json.loads((out/'ENTRYPOINT_INDEX.json').read_text(encoding='utf-8-sig'))
# These entries were directly inspected in the current follow-up. Older reviews
# remain in REPORT.md; they are not silently promoted to current coverage.
# Current network/persistence callback review; helper payload costs remain explicit gaps.
reviewed={
 ('XUiC_RebirthTraderJobPopup','Update'):('LIVE_REGRESSION_VERIFIED','Authored-view lookups cached; real trader hover/accept/reopen checked, no FPS claim','LIVE_TRADER_TEXTURE_RETEST.txt;REPORT.md'),
 ('XUiC_RebirthWaypointList','Update'):('SOURCE_REVIEWED_CANDIDATE','300ms map-visible population; full filter/sort and caller closures require measurement','REPORT.md'),
 ('XUiC_RebirthInviteList','Update'):('SOURCE_REVIEWED_CANDIDATE','300ms map-visible population; visible rows and async name callbacks traced','REPORT.md'),
 ('XUiC_RebirthChallengeGroupList','Update'):('SOURCE_REVIEWED_CANDIDATE','250ms native/presentation admission; repeated background lookup unmeasured','REPORT.md'),
}
reviewed.update({
 ('RebirthNpcInteractionNetworkClient','Update'):('SOURCE_REVIEWED_BOUNDED','Per-update expiry sweep <=128 pending; allocations only on expiration; removes before callbacks; remote stress pending','REPORT.md'),
 ('RebirthNpcNetworkBaselineService','Tick'):('SOURCE_REVIEWED_CANDIDATE','Fair one-client page <=32 actors/update, <=64 pending clients; up to3packages/actor; snapshot/payload/population cost pending','REPORT.md'),
 ('RebirthStationCancellationObservation','Tick'):('SOURCE_REVIEWED_CANDIDATE','<=4 watches,1s retry,60s expiry; immediate world teardown; XML/readback helper costs require measurement','REPORT.md'),
 ('RebirthStationCancellationObservation','TryWatch'):('SOURCE_REVIEWED_CANDIDATE','Event-scoped admission bounded by4 serializer requests; captures immutable comparison images; no runtime stress proof','REPORT.md'),
 ('RebirthStationSnapshotWritePatch','Prefix'):('SOURCE_REVIEWED_CANDIDATE','Demand-scoped publication lookup before scope; watched snapshots clone bytes; no active serialization benchmark','REPORT.md'),
 ('RebirthStationSnapshotWritePatch','Finalizer'):('SOURCE_REVIEWED_BOUNDED','Restores previous thread-local publication scope and returns original exception','REPORT.md'),
 ('RebirthStationRegionPublishedPatch','Prefix'):('SOURCE_REVIEWED_CANDIDATE','No active scope returns before payload work; active copy/inflate/compare/region lock requires measurement','REPORT.md'),
 ('RebirthStationRegionPublishedPatch','Finalizer'):('SOURCE_REVIEWED_CANDIDATE','Active publication performs region readback/digest; lock released in finally; native I/O cost unmeasured','REPORT.md'),
})
reviewed.update({
 ('XUiC_RebirthToolbeltItemSlot',"Update"):("SOURCE_REVIEWED_CANDIDATE",'Native base.Update plus no-op timing; selection and binding routes inspected; inclusive sample is not exclusive custom cost',"REPORT.md"),
 ('XUiC_RebirthToolbeltLayout',"Update"):("SOURCE_REVIEWED_CANDIDATE",'100ms cached layout with pool identity and changed setters; native reconstruction pending',"REPORT.md"),
 ('XUiC_RebirthVersionLabel',"Update"):("SOURCE_REVIEWED_CANDIDATE",'Cached successful discovery and1s invalidation retry; window-map identity checked',"REPORT.md"),
 ('XUiC_RebirthCraftingWorldStatus',"Update"):("SOURCE_REVIEWED_CANDIDATE",'200ms context refresh; dynamic time visibility and temperature; formatting cost unmeasured',"REPORT.md"),
 ('XUiC_RebirthCraftingTopTabs',"Update"):("SOURCE_REVIEWED_CANDIDATE",'Visible-only projection; Journal internal2s poll and unread revision; full poll helper unqualified',"REPORT.md"),
 ('XUiC_RebirthStationQueueWindow',"Update"):("SOURCE_REVIEWED_CANDIDATE",'Visible shared HUD scope caches13descendants; restores on final release',"REPORT.md"),
})
# Farming Pumps have custom names omitted by the entrypoint heuristic, recorded
# below from METHOD_INDEX so the omission is visible rather than ignored.
extra_names={'PumpPendingVisualRefreshesForGameUpdate','PumpPendingGeometryDrivenPlantLightRefreshes','PumpPendingPlantStateSnapshots'}
allmethods=json.loads((out/'METHOD_INDEX.json').read_text(encoding='utf-8-sig'))
helper_entries={('RebirthNpcRuntimeRegistry','GetSnapshot'),('RebirthNpcReplicationPerformanceService','QueueRelationship'),('RebirthNpcReplicationPerformanceService','FlushRelationships'),('RebirthNpcRelationshipBatcher','Acknowledge'),('RebirthWorkstationCraftCompletion','Counts')}
helper_entries.update({('RebirthCookingSessionService','PumpReplies'),('RebirthMetabolismStateRepository','EnsureLoaded'),('RebirthFireVisualManager','BuildDesiredSets')})
rows += [r for r in allmethods if r['name'] in extra_names or (r['type'],r['name']) in helper_entries]
reviewed.update({
 ('AdvancedFarmingDoorVisualRefreshService','PumpPendingVisualRefreshesForGameUpdate'):('ISOLATED_VERIFIED_FIX_COMPILED','Retry-age persistence fixed; native visuals and timing pending','DOOR_RETRY_BEFORE.txt;DOOR_RETRY_AFTER.txt;BUILD_DOOR_RETRY.txt'),
 ('AdvancedFarmingSyncService','PumpPendingGeometryDrivenPlantLightRefreshes'):('SOURCE_REVIEWED_CANDIDATE','One region per update does not bound crop evaluations; dense runtime measurement pending','REPORT.md'),
 ('AdvancedFarmingSyncService','PumpPendingPlantStateSnapshots'):('SOURCE_REVIEWED_CANDIDATE','All due client snapshots processed together; remote arrival stress pending','REPORT.md'),
})
reviewed.update({
 ('RebirthNpcEquipmentPersistenceStore','Tick'):('ISOLATED_VERIFIED_FIX_COMPILED','Automatic retries on 30s save cadence; explicit loads unchanged','PERSISTENCE_TICK_FIXTURE.txt;BUILD_PERSISTENCE_TICK_FINAL.txt'),
 ('RebirthNpcInventoryPersistenceStore','Tick'):('ISOLATED_VERIFIED_FIX_COMPILED','Automatic retries on 30s save cadence; explicit loads unchanged','PERSISTENCE_TICK_FIXTURE.txt;BUILD_PERSISTENCE_TICK_FINAL.txt'),
 ('RebirthNpcSettlementPersistenceStore','Tick'):('ISOLATED_VERIFIED_FIX_COMPILED','Store retries on 30s cadence; simulation may retry each 2s','PERSISTENCE_TICK_FIXTURE.txt;BUILD_PERSISTENCE_TICK_FINAL.txt'),
 ('RebirthNpcSettlementSimulation','Tick'):('ISOLATED_VERIFIED_FIX_COMPILED','Loads only on due simulation work; needs/assignment scheduling matched; population stress pending','PERSISTENCE_TICK_FIXTURE.txt;REPORT.md'),
 ('RebirthNpcExecutionPersistenceStore','Tick'):('SOURCE_REVIEWED_CANDIDATE','Per-update load also enforces world/save scope; do not move behind save timer without preserving invalidation','REPORT.md'),
 ('RebirthNpcDecisionEngine','Tick'):('SOURCE_REVIEWED_CANDIDATE','96 discovery/24 work limit; cached membership; active task/context allocation stress pending','REPORT.md'),
 ('RebirthNpcSocialService','Tick'):('SOURCE_REVIEWED_CANDIDATE','One-minute full memory sweep, 256 memories per NPC; population stress and missing dispatch adapter pending; retention fixture passed','REPORT.md'),
 ('RebirthNpcFollowRuntime','Tick'):('SOURCE_REVIEWED_CANDIDATE','1Hz full session snapshot and navigation requests; helper/large-population measurement pending','REPORT.md'),
 ('RebirthNpcGuardNavigationRuntime','Tick'):('SOURCE_REVIEWED_CANDIDATE','1Hz full session snapshot and navigation requests; helper/large-population measurement pending','REPORT.md'),
 ('RebirthNpcPatrolRuntime','Tick'):('SOURCE_REVIEWED_CANDIDATE','1Hz full session snapshot and navigation requests; helper/large-population measurement pending','REPORT.md'),
 ('RebirthNpcTravelRuntime','Tick'):('SOURCE_REVIEWED_CANDIDATE','1Hz full session snapshot and navigation requests; helper/large-population measurement pending','REPORT.md'),
 ('RebirthNpcMissionRuntime','Tick'):('SOURCE_REVIEWED_CANDIDATE','1Hz full session snapshot, heartbeat and executor calls; executor costs remain open','REPORT.md'),
})
reviewed.update({
 ('RebirthNpcRuntimeRegistry','GetSnapshot'):('ISOLATED_VERIFIED_FIX_COMPILED','Numeric stable-ID comparator preserves ordering; native population qualification pending','REGISTRY_SORT_FIXTURE.txt;BUILD_REGISTRY_SORT.txt;REPORT.md'),
 ('RebirthNpcReplicationPerformanceService','QueueRelationship'):('ISOLATED_VERIFIED_FIX_COMPILED','Replay recorded only after admission; native adapter gap remains','RELATIONSHIP_AFTER.txt;BUILD_RELATIONSHIP.txt'),
 ('RebirthNpcReplicationPerformanceService','FlushRelationships'):('ISOLATED_VERIFIED_FIX_COMPILED','Acknowledge accepted prefix; limits/refusal/reentrancy tested; native adapter gap remains','RELATIONSHIP_AFTER.txt;BUILD_RELATIONSHIP.txt'),
 ('RebirthNpcRelationshipBatcher','Acknowledge'):('ISOLATED_VERIFIED_FIX_COMPILED','Head identity removal retains unapplied items','RELATIONSHIP_AFTER.txt;BUILD_RELATIONSHIP.txt'),
 ('RebirthWorkstationCraftCompletion','Counts'):('ISOLATED_VERIFIED_FIX_COMPILED','Removed temporary bag/belt concatenation; actual native multiplayer pending','COMPLETION_COUNTS_FIXTURE.txt;BUILD_COMPLETION_COUNTS.txt'),
 ('RebirthCompanionSnapshotService','Update'):('SOURCE_REVIEWED_BOUNDED','At most four pending requests; removes before timeout callbacks and rejects retired world','REPORT.md'),
 ('XUiC_RebirthPartyCompanionHudList','Update'):('SOURCE_REVIEWED_CANDIDATE','Style gate; 500ms list plus membership changes; full registry discovery reviewed','REPORT.md'),
 ('XUiC_RebirthSimplePartyCompanionHudList','Update'):('SOURCE_REVIEWED_CANDIDATE','Style gate; 500ms list plus membership changes; population stress pending','REPORT.md'),
 ('XUiC_RebirthFollowingCompanionEntryList','Update'):('SOURCE_REVIEWED_CANDIDATE','500ms list discovery; native/drone/NPC order preserved; population stress pending','REPORT.md'),
 ('RebirthFrameScheduler','OnGameUpdate'):('SOURCE_REVIEWED_DORMANT','No production registration caller found; advisory budget is not enforcement','REPORT.md'),
 ('RebirthProtectCrateService','Update'):('SOURCE_REVIEWED_CANDIDATE','128 recovery entities/update; one-second crate/player work still population-dependent','REPORT.md'),
 ('RebirthMetabolismService','Tick'):('SOURCE_REVIEWED_CANDIDATE','Readiness before configurable process cadence; diagnostic traversal gated; full helper reconciliation pending','REPORT.md'),
})
reviewed.update({
 ('XUiC_RebirthCraftingRecipeCatalogue','Update'):('SOURCE_REVIEWED_CANDIDATE','Dirty-event rebuilds; native recipe projection preserved; large-catalogue burst measurement pending','REPORT.md'),
 ('XUiC_RebirthCraftingRequirements','Update'):('SOURCE_REVIEWED_CANDIDATE','200ms revision gate before material projection; source-cache dependencies reviewed; populated-storage measurement pending','REPORT.md'),
 ('XUiC_RebirthCraftingRecipeDetails','Update'):('SOURCE_REVIEWED_CANDIDATE','200ms tools and dirty availability plus1s safety; inventory subscriptions and remote intent preserved','REPORT.md'),
 ('RebirthCookingJobRuntime','OnGameUpdate'):('SOURCE_REVIEWED_CANDIDATE','1s authority gate and per-tick station reuse; active-job population cost pending','REPORT.md'),
 ('XUiC_RebirthCookingWorkspace','Update'):('SOURCE_REVIEWED_CANDIDATE','Closed guard, coalesced station invalidation and750ms render; native input retained','REPORT.md'),
 ('XUiC_RebirthCookingStation','Update'):('SOURCE_REVIEWED_CANDIDATE','Stable queue controllers, changed clock formatting and1s queue synchronization','REPORT.md'),
 ('XUiC_RebirthCookingHud','Update'):('SOURCE_REVIEWED_CANDIDATE','Modal/dead guard,2s requests, changed XML parse and dirty rendering','REPORT.md'),
 ('RebirthCookingNavigation','Tick'):('SOURCE_REVIEWED_CANDIDATE','Context guard and station validity; no timing changes','REPORT.md'),
 ('XUiC_RebirthCookingScroll','Update'):('SOURCE_REVIEWED_DORMANT','No source/config reference beyond definition found; not an observed active bottleneck','REPORT.md'),
})
for type_name,note in {
 'XUiC_RebirthCraftingActions':'Input style change-gated; geometry and shortcut paths inspected at entrypoint',
 'XUiC_RebirthCraftingInventory':'Native input retained; projection coalescing; capacity checks and lock-mode persistence',
 'XUiC_RebirthCraftingInventoryScroll':'Cached view discovery; 1s wiring; change-gated geometry; per-frame viewport finishing remains',
 'XUiC_RebirthCraftingInventorySlot':'Hidden unpinned presentation suppressed; visible and pinned native input retained',
 'XUiC_RebirthCraftingItemActionEntry':'Change-aware shortcut normalization; native hover/press remains',
 'XUiC_RebirthCraftingItemContext':'Per-frame selected fingerprint/profile work; 500ms repair materials; deeper profile cost pending',
 'XUiC_RebirthCraftingOutcome':'Recipe/batch/revision invalidation plus 500ms skill refresh',
 'XUiC_RebirthCraftingQueue':'Native progression before visibility guard; 100ms presentation and per-frame smooth scrolling',
 'XUiC_RebirthCraftingQueueEntry':'Active native timing/full-output retry retained; visible card presentation only',
}.items():
 reviewed[(type_name,'Update')]=('SOURCE_REVIEWED_CANDIDATE',note+'; not full helper/runtime closure','REPORT.md')
reviewed[('RebirthBlockPickupModApi','OnGameUpdate')]=('SOURCE_REVIEWED_CANDIDATE','Notice/reply caps32/16; coalesced access flush may spike; manual diagnostics disabled fast paths','REPORT.md')
reviewed.update({
 ('RebirthCookingSessionService','PumpReplies'):('ISOLATED_VERIFIED_FIX_COMPILED','Lazy expiry allocation and callback identity checks; local idle verified; remote timeout coverage pending','COOKING_REPLIES_FIXTURE.txt;LIVE_COOKING_REPLY_OPEN_WINDOW.txt;REPORT.md'),
 ('RebirthMetabolismStateRepository','EnsureLoaded'):('ISOLATED_VERIFIED_FIX_COMPILED','Five-second failed-path retry; Reset generation rejects stale loads; native recovery pending','METABOLISM_RETRY_FIXTURE.txt;BUILD_METABOLISM_RETRY.txt;REPORT.md'),
 ('RebirthFireVisualManager','BuildDesiredSets'):('ISOLATED_VERIFIED_FIX_COMPILED','Single global sort preserves band order; native fire workload pending','FIRE_SORT_FIXTURE.txt;BUILD_FIRE_SORT.txt;REPORT.md'),
 ('RebirthBossEventDirector','Update'):('SOURCE_REVIEWED_CANDIDATE','Authority/one-second/options gates; player-scaled scheduling work','REPORT.md'),
 ('RebirthBossEventLifecycle','Update'):('SOURCE_REVIEWED_CANDIDATE','One-second event snapshot; targeting/health/reward maintenance traced; workload testing pending','REPORT.md'),
 ('RebirthBossEventRewardService','Update'):('SOURCE_REVIEWED_CANDIDATE','Lifecycle-gated entity lookup and legacy migration; unloaded reward expiry preserved','REPORT.md'),
 ('RebirthBossEventNativeMapMarkerService','Update'):('SOURCE_REVIEWED_CANDIDATE','Feature/one-second gates; temporary live-ID set; population testing pending','REPORT.md'),
 ('RebirthBossEventOwnerNetwork','Update'):('SOURCE_REVIEWED_CANDIDATE','Ten-second sync; reward access broadcast restricted to legacy block rewards','REPORT.md'),
})
# Current helper coverage; source review and isolated checks are distinct from runtime proof.
helper_latest={('RebirthBossEventPersistence','SaveIfDue'),('RemoteResourceSnapshotCache','InvalidateSources'),('RemoteResourceSourceDiscovery','ResolveNearby')}
rows += [r for r in allmethods if (r['type'],r['name']) in helper_latest]
reviewed.update({
 ('RebirthBossEventPersistence','SaveIfDue'):('ISOLATED_VERIFIED_FIX_COMPILED','Failure respects existing60second cadence; dirty and explicit save semantics retained; native fault injection pending','BOSS_SAVE_RETRY_FIXTURE.txt;BUILD_BOSS_SAVE_RETRY.txt'),
 ('RebirthFireService','Update'):('SOURCE_REVIEWED_CANDIDATE','24due attempts and64cooldown pops; selection and heap compaction population-dependent; visibility pruning per-frame candidate','REPORT.md'),
 ('RebirthFireSleeperActivation','Update'):('SOURCE_REVIEWED_CANDIDATE','Queue128,starts2,attempts8; prefab-expiry scan and event-path queries traced; native large-fire workload pending','REPORT.md'),
 ('RebirthFireModApi','OnGameUpdate'):('SOURCE_REVIEWED_CANDIDATE','Server simulation/autosave and client visual delegation; helper review recorded; not whole-fire qualification','REPORT.md'),
 ('RebirthNpcActivityRegistry','Tick'):('SOURCE_REVIEWED_CANDIDATE','One-second watchdog scans active leases and rechecks before termination; temporary list per admitted sweep','REPORT.md'),
 ('RebirthNpcActivityRecoveryRegistry','Tick'):('ISOLATED_VERIFIED_FIX_COMPILED','Previously verified16node cursor includes held entries; current source rechecked; runtime stress pending','RECOVERY_FIXTURE.txt;REPORT.md'),
 ('RemoteResourceSourceDiscovery','ResolveNearby'):('ISOLATED_VERIFIED_FIX_COMPILED','Authorization before expensive NPC inventory construction; native population measurement pending','NPC_DISCOVERY_FIXTURE.txt;BUILD_NPC_DISCOVERY.txt'),
})
# October10 continuation coverage: record only directly inspected surfaces.
continuation_helpers={
 ('RebirthNpcCommandGateway','TryDequeueNextLocked'),
 ('RebirthNpcCommandGateway','MoveDueRetriesLocked'),
 ('RebirthNpcExecutionLeaseRegistry','GetActiveDispatchSnapshot'),
 ('RebirthOreSenseService','SetCube'),
 ('RebirthSurvivorNetworkService','PumpLocalCreationQueue'),
 ('RebirthSkillAwardService','FlushOwnerPublications'),
 ('RebirthLearningChallenges','FlushPendingCredits'),
}
rows += [r for r in allmethods if (r['type'],r['name']) in continuation_helpers]
reviewed.update({
 ('RebirthNpcCommandGateway','TryDequeueNextLocked'):('ISOLATED_VERIFIED_FIX_COMPILED','Direct eligible-head dequeue; held-prefix ordering retained; native stress pending','COMMAND_DEQUEUE_FIXTURE.txt;BUILD_COMMAND_DEQUEUE.txt'),
 ('RebirthNpcCommandGateway','MoveDueRetriesLocked'):('ISOLATED_VERIFIED_FIX_COMPILED','Sorted deadline early exit and lazy list; due queue semantics compared; native pending','COMMAND_RETRY_FIXTURE.txt;BUILD_COMMAND_RETRY.txt'),
 ('RebirthNpcExecutionLeaseRegistry','GetActiveDispatchSnapshot'):('ISOLATED_VERIFIED_FIX_COMPILED','Only active clones/sort; general history preserved; population timing pending','DISPATCH_SNAPSHOT_FIXTURE.txt;BUILD_DISPATCH_SNAPSHOT.txt'),
 ('RebirthNpcActivityDispatcher','Tick'):('ISOLATED_VERIFIED_FIX_COMPILED','Active-only projection; held checks and dispatch order unchanged; native pending','DISPATCH_SNAPSHOT_FIXTURE.txt;BUILD_DISPATCH_SNAPSHOT.txt'),
 ('RebirthOreSenseService','SetCube'):('ISOLATED_VERIFIED_FIX_COMPILED','Reusable geometry scratch; exact coordinates matched; native rendering pending','ORE_GEOMETRY_FIXTURE.txt;BUILD_ORE_GEOMETRY.txt'),
 ('RebirthOreSenseService','OnGameUpdate'):('SOURCE_REVIEWED_CANDIDATE','Active-only96block scan;120ms visuals; cache prune/list sort population cost','REPORT.md'),
 ('RebirthSurvivorNetworkService','OnGameUpdate'):('SOURCE_REVIEWED_CANDIDATE','Seven delegates traced; refusal snapshots and network populations unmeasured','REPORT.md'),
 ('RebirthSurvivorNetworkService','PumpLocalCreationQueue'):('SOURCE_REVIEWED_CANDIDATE','Empty exit then full local request drain; no explicit admission cap','REPORT.md'),
 ('RebirthSkillAwardService','FlushOwnerPublications'):('SOURCE_REVIEWED_CANDIDATE','Pending-only1s, owner coalescing, identity recheck and save/send recovery retained','REPORT.md'),
 ('RebirthLearningChallenges','FlushPendingCredits'):('SOURCE_REVIEWED_BOUNDED','Empty/null manager exits before snapshot; removes before native callback; no failed callback replay','REPORT.md'),
 ('RebirthBackgroundStarterItems','Tick'):('SOURCE_REVIEWED_CANDIDATE','2s recovery, completed receipt before snapshot, fitting batch halving','REPORT.md'),
 ('RebirthMusicTransferClient','Tick'):('SOURCE_REVIEWED_CANDIDATE','2s gate; original offer retained until owner receipt and acknowledgement','REPORT.md'),
 ('RebirthGearClientTransferPump','Tick'):('SOURCE_REVIEWED_CANDIDATE','Immediate scope checks;750ms transfer/5s bootstrap gates','REPORT.md'),
 ('RebirthGearDeferredInitialDispatcher','Tick'):('SOURCE_REVIEWED_CANDIDATE','Single pending intent; refused admission may recapture inventory everyframe','REPORT.md'),
 ('RebirthGearSandboxAdjustmentDeferral','Tick'):('SOURCE_REVIEWED_CANDIDATE','Owner-null exit; scope/custody checks;750ms replay retry','REPORT.md'),
})
# HUD closure review on October10; findings are source evidence, not runtime timings.
reviewed.update({
 ('XUiC_RebirthHeatMapHud','Update'):('SOURCE_REVIEWED_BOUNDED','Option gate;3s native/network poll;1s changed-text projection; reader helper inspected','REPORT.md'),
 ('XUiC_RebirthMiniMap','Update'):('SOURCE_REVIEWED_CANDIDATE','Hidden terrain/render skip;500ms movement/network terrain gate;50ms marker updates; material cleanup inspected; GPU/native map helper costs unmeasured','REPORT.md'),
 ('XUiC_RebirthTargetBar','Update'):('SOURCE_REVIEWED_CANDIDATE','Native selection retained;200ms or target-change projection;9visible buff limit but full hidden-buff scan possible','REPORT.md'),
 ('RebirthCruiseControlHud','Update'):('SOURCE_REVIEWED_CANDIDATE','50ms binding refresh including inactive state; local player captured only OnOpen; respawn qualification pending','REPORT.md'),
 ('XUiC_RebirthOreSenseHud','Update'):('SOURCE_REVIEWED_CANDIDATE','100ms unconditional bindings; inactive constant fields; no world scans in HUD','REPORT.md'),
 ('XUiC_RebirthStudyHud','Update'):('SOURCE_REVIEWED_CANDIDATE','100ms state polling; player refreshed after respawn; binding refresh only on changed projection; session helper costs not qualified','REPORT.md'),
 ('XUiC_RebirthMusicHud','Update'):('SOURCE_REVIEWED_BOUNDED','Lifecycle/modal suppression;250ms playback projection; changed label writes; no playlist scan in controller','REPORT.md'),
})
reviewed.update({
 ('XUiC_RebirthCompanions','Update'):('SOURCE_REVIEWED_CANDIDATE','Closed guard;750ms one-pending snapshot;6direction arrows; population-dependent roster build traced','REPORT.md'),
 ('XUiC_RebirthCompanionInteractionWindow','Update'):('SOURCE_REVIEWED_CANDIDATE','Closed guard;350ms render and1s one-pending remote request; repeated action projection remains','REPORT.md'),
})
recent_helpers={('RebirthGearInventorySnapshot','TryEncode'),('EntityRebirthDogCompanion','GetRebirthCollisionBlocker')}
rows += [r for r in allmethods if (r['type'],r['name']) in recent_helpers]
reviewed.update({
 ('RebirthGearInventorySnapshot','TryEncode'):('ISOLATED_VERIFIED_FIX_COMPILED','Removed intermediate byte copy;1000byte-equivalence cases; native ItemValue qualification separate','INVENTORY_ENCODING_FIXTURE.txt;BUILD_INVENTORY_ENCODING.txt;REPORT.md'),
 ('EntityRebirthDogCompanion','GetRebirthCollisionBlocker'):('ISOLATED_VERIFIED_FIX_COMPILED','Per-instance positive cache; isolated invalidation checks; one native friendly crossing; replacement/recall pending','DOG_BLOCKER_FIXTURE.txt;LIVE_DOG_CROSSING.json;REPORT.md'),
 ('RebirthCruiseControlHud','Update'):('ISOLATED_VERIFIED_FIX_COMPILED','Change-driven explicit bindings; current player refreshed; native vehicle/respawn pending','IDLE_HUD_FIXTURE.txt;BUILD_IDLE_HUD.txt;REPORT.md'),
 ('XUiC_RebirthOreSenseHud','Update'):('ISOLATED_VERIFIED_FIX_COMPILED','Change-driven explicit bindings; cadence/base calls retained; native UI pending','IDLE_HUD_FIXTURE.txt;BUILD_IDLE_HUD.txt;REPORT.md'),
 ('RebirthMetabolismInstaller','OnGameUpdate'):('SOURCE_REVIEWED_CANDIDATE','Authority/mode/configurable cadence before player loop; individual Tick overlap gate; population timing pending','REPORT.md'),
 ('XUiC_RebirthMetabolismHud','Update'):('SOURCE_REVIEWED_CANDIDATE','250ms journal/accent/snapshot polling; revision gates binding after snapshot acquisition','REPORT.md'),
 ('XUiC_RebirthMetabolismWindow','Update'):('SOURCE_REVIEWED_CANDIDATE','Character hidden guard;200ms snapshot polling; feedback refresh and repeated parent lookup remain','REPORT.md'),
 ('XUiC_RebirthMetabolismInformationWindow','Update'):('SOURCE_REVIEWED_BOUNDED','Base update then parent/hotkey cancel checks; no custom snapshot work','REPORT.md'),
})
reviewed.update({
 ('RebirthNpcWorkOutcomePersistenceStore','Tick'):('SOURCE_REVIEWED_CANDIDATE','15s deadline before save; version skip; one-time load; release-gated caller; enabled-work recovery pending','REPORT.md'),
 ('RebirthNpcWorkCheckpointPersistenceStore','Tick'):('SOURCE_REVIEWED_CANDIDATE','15s deadline before save; revision skip; once-only512row reconciliation; release-gated caller','REPORT.md'),
 ('RebirthNpcConcreteWorkRuntime','Tick'):('SOURCE_REVIEWED_CANDIDATE','Release/registration gate then every10updates; package budgets inspected; helper scans remain unqualified','REPORT.md'),
})
rows += [r for r in allmethods if (r['type'],r['name']) == ('RebirthDogLifecycleService','TickGhostRepairRetries')]
reviewed.update({
 ('RebirthNpcPreparedSchedulerAdmission','Tick'):('ISOLATED_VERIFIED_FIX_COMPILED','Empty callback avoids array; real production fixture covers retries, reentrancy, budget, world hold and retirement','ADMISSION_IDLE_FIXTURE.txt;BUILD_ADMISSION_IDLE.txt'),
 ('RebirthDogLifecycleService','TickGhostRepairRetries'):('ISOLATED_VERIFIED_FIX_COMPILED','Due batch allocated only when needed; extracted callback fixture covers recovery, retry and authority; native recovery pending','GHOST_IDLE_FIXTURE.txt;BUILD_GHOST_IDLE.txt'),
 ('RebirthMetabolismService','Tick'):('ISOLATED_VERIFIED_FIX_COMPILED','Diagnostic-only snapshot skipped normally; native diagnostic smoke16PASS; cached/remote publication and timing unverified','METABOLISM_DIAGNOSTIC_STATIC_CHECK.txt;LIVE_METABOLISM_DIAGNOSTIC.txt;BUILD_METABOLISM_DIAGNOSTIC_SNAPSHOT.txt'),
})
rows += [r for r in allmethods if (r['type'],r['name']) in {('RebirthTheorySoloService','OnUpdate'),('RebirthTheorySoloService','Eligible')}]
reviewed.update({
 ('RebirthTheorySoloService','OnUpdate'):('ISOLATED_VERIFIED_FIX_COMPILED','Active snapshot retained; live eligibility allocation removed; actual service60PASS with native/storage doubles','THEORY_ELIGIBILITY_FIXTURE.txt;THEORY_SERVICE_REGRESSION.txt;BUILD_THEORY_ELIGIBILITY.txt'),
 ('RebirthTheorySoloService','Eligible'):('ISOLATED_VERIFIED_FIX_COMPILED','Bounded16 reserved ordinals; uncached current evidence;10000parity/zero new allocation; native pending','THEORY_ELIGIBILITY_FIXTURE.txt;THEORY_SERVICE_REGRESSION.txt;BUILD_THEORY_ELIGIBILITY.txt'),
})
# Reconcile completed follow-up evidence without promoting isolated checks to native proof.
followup={
 ('AdvancedFarmingActiveAreaRegistry','RebuildCoarseInfluenceSnapshotLocked'):('ISOLATED_VERIFIED_FIX_COMPILED','Occupied-chunk expansion;2000 actual extracted mutation comparisons;dense isolated96x operation speedup; native stress pending','FARM_CHUNK_PRODUCTION_FIXTURE.txt;BUILD_FARM_CHUNK.txt;REPORT.md'),
 ('XUiC_RebirthDialogResponseList','UpdateJobAcceptanceStatus'):('ISOLATED_VERIFIED_FIX_COMPILED','Formatting cache includes culture and bypasses mutable cultures;1000 parity cases; native conversation pending','DIALOG_STATUS_FIXTURE.txt;BUILD_DIALOG_STATUS.txt;REPORT.md'),
 ('RebirthCookingBatch','CopyOutputData'):('ISOLATED_VERIFIED_FIX_COMPILED','Removed per-field exclusion arrays;isolated allocation reduction;one native potato output delta; metadata and multiplayer pending','COOKING_OUTPUT_FIXTURE.txt;LIVE_COOKING_OUTPUT_COMPLETION.json;BUILD_COOKING_OUTPUT.txt'),
 ('RebirthTraitSupportService','IsEffectStateActive'):('ISOLATED_VERIFIED_FIX_COMPILED','Immutable definition normalization;196 normalization combinations;native effects pending','TRAIT_NORMALIZATION_FIXTURE.txt;BUILD_TRAIT_NORMALIZATION.txt'),
 ('XUiC_RebirthTraderJobCard','Update'):('SOURCE_REVIEWED_CANDIDATE','Visible-card gate;250ms semantic checks;crop on texture identity change;completion helper/native population cost unmeasured','REPORT.md'),
 ('XUiC_RebirthTraderJobCard','ApplyPreviewCrop'):('SOURCE_REVIEWED_BOUNDED','Cached view;null/repeated texture exits;UV update only on replacement;no recurring allocation found','REPORT.md'),
 ('XUiC_RebirthTraderJobCard','GetCardData'):('SOURCE_REVIEWED_CANDIDATE','Context identity then250ms hash/completion check;rebuild only on semantic change;native completion count helper remains unqualified','REPORT.md'),
}
followup[('RebirthTraderJobCompletionStats','GetCompletionCount')]=('SOURCE_REVIEWED_CANDIDATE','Per-card250ms full history traversal; duplicate classification and prefab normalization; mutable recognition/policy dependencies traced; native magnitude unmeasured','REPORT.md')
reviewed.update(followup)
rows += [r for r in allmethods if (r['type'],r['name']) in followup]
merged={} 
for r in rows:
 k=(r['file'],r['type'],r['name'],r['line'])
 if k not in merged: merged[k]=dict(r,configs=set())
 merged[k]['configs'].add('Debug' if r['debug'] else 'Release')
result=[]
for k,r in sorted(merged.items()):
 p=Path(r['file']); current=hashlib.sha256(p.read_bytes()).hexdigest().upper()
 state,note,evidence=reviewed.get((r['type'],r['name']),('EVIDENCE_NOT_RECONCILED','Current method/helper review or prior evidence reconciliation still required',''))
 result.append(dict(file=r['file'],type=r['type'],method=r['name'],indexed_line=r['line'],configurations=';'.join(sorted(r['configs'])),indexed_hash=r['hash'],current_hash=current,index_current=current==r['hash'],status=state,disposition=note,evidence=evidence))
with (out/'ENTRYPOINT_REVIEW_LEDGER.csv').open('w',newline='',encoding='utf-8') as f:
 w=csv.DictWriter(f,fieldnames=result[0].keys());w.writeheader();w.writerows(result)
counts=collections.Counter(r['status'] for r in result)
(out/'COVERAGE_STATUS.md').write_text('''# Performance coverage ledger â€” NOT COMPLETE
Source: installed cumulative REBIRTH 3.0 / native V3.3.0(b18).

ENTRYPOINT_REVIEW_LEDGER.csv records current source identity separately from index identity. A changed hash requires index refresh before relying on its line or calls. Source-review status is not runtime validation. Existing historical review evidence remains in REPORT.md and prior dated audits; EVIDENCE_NOT_RECONCILED means it has not been reconciled to this method/current source, not that no prior work exists.

The callback/Harmony syntax heuristic is incomplete: custom farming pumps and selected reviewed helpers were added explicitly from METHOD_INDEX. This ledger therefore is a review queue, not proof of full-code coverage. Constructors, property accessors, other callbacks, registration/delegate/native edges, configuration branches, persistence/network and event-driven expensive work require separate tracing. Do not derive an overall completion percentage from these rows.

Outstanding runtime workloads include dense immature crops and simultaneous remote arrivals, larger NPC populations, active conversation presentation, long-session allocation/memory, multiplayer and enabled Purge. Ordinary UI captures and isolated fixtures do not certify these workloads.

Current ledger statuses:
'''+''.join(f'- {k}: {v}\n' for k,v in counts.items()),encoding='utf-8')
print(json.dumps({'rows':len(result),'status':counts,'stale_index_rows':sum(not r['index_current'] for r in result)},indent=2))
