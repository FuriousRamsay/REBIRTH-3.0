# Survivor complex skills, native producers and passive integration

2026-10-15 audit only. No production edits/build/tests/game/gameplay commands/git/shared report or ledger changes. All10 source files below fully read; searches include ignored Debug with rg -uuu. Existing SkillEventRouter, award service, companions and TheorySolo evidence reused without full-read credit. This tranche does not close the1626-line SustainedDps helper,599-line ProgressionPatches or661-line ServiceCraftSkillService; their named callers were inspected selectively.

## Full-read source manifest

| Current source | Lines | SHA256 |
|---|---:|---|
|Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs|208|69E480E7D050ACB6B5971954723BC9867AF3089935DA07953D6565E939F4A75F|
|Scripts/Survivor/Progression/RebirthComplexSkillSystemPatches.cs|21|9A3893C9CDD205E6C297EEE80473F921BE323A912C6CE528E7705673EF485244|
|Scripts/Survivor/Progression/RebirthComplexSkillSystemVectorHarness.cs|42|0DA2A8A4968AD8869A6B47070F9BA065EE67FDD6D0001310A411376D36455577|
|Scripts/Survivor/Progression/RebirthConstitutionTrainingService.cs|98|FAFFF4BF413B634BDDBAB519B2E0ECD6539CFA51A0619B8B581BE0B473EDB83C|
|Scripts/Survivor/Progression/RebirthDroneTravelTrainingService.cs|71|DCAF2CD696901FDB6B961467A52A790661AA897D62AD926648973B0BE0B08DFA|
|Scripts/Survivor/Progression/RebirthSkillOutcomeValueService.cs|65|7591633AF4CD0D6F41873CD4305B65B56DA8460DB959F891A3FC60BC52D9200C|
|Scripts/Survivor/Progression/RebirthSkillOutcomeValueVectorHarness.cs|26|F5DB543E1A78A3BFBE64D89095D9E3B8350CE1519D1489F82137BFD0B03217C5|
|Scripts/Survivor/Progression/RebirthStealthTrainingService.cs|42|AF131068871FFAD03DF66F92E447DDC1A139A9FB3294F15A318CF2FDCB6C29F6|
|Scripts/Survivor/Progression/RebirthWeaponFamilySkillService.cs|217|965425CF3E7FFD91E6D505524A516E25276F7F1ECB6798E754D53E6AAA953C3D|
|Scripts/Survivor/Progression/RebirthWeaponFamilySkillVectorHarness.cs|117|D677039C26B7B83DADB3C3CCB9328FBD7C563AA6BCA75EEF25F2341AE9AC1986|

10 C#907 lines. OutcomeVector and WeaponVector first32/rest85/end reread after broad output truncation. No tests/harnesses executed.

## Confirmed explosive-block award evidence defect

ComplexSkillService.OnExplosionBlocksCompleted78+ requires authoritative Rebirth mode, valid source classified explosives, loaded player and ONLY `ChangedBlockPositions.Count > 0` before ordinary TryAward(explosives,...minimumInterval0.05). Explicit ProgressionInstaller138 installs Explosion.AttackBlocks postfix. Count is not proof that any native block changed.

Exact installed native Explosion.AttackBlocks inserts a BlockChangeInfo into ChangedBlockPositions at172–180, BEFORE trader-area/out-of-bounds rejection182+, BEFORE hardness/protection effective damage computation, and BEFORE num7<=0 continue. Entries for protected/zero-effective-damage candidates are not removed. Therefore a blast that encounters qualifying nonair candidate blocks can satisfy this count without applied block work. This is stronger evidence than an absent success check: the native collection explicitly includes rejected candidates. Parent can classify this as source-confirmed improper successful-work attribution; no gameplay reproduction or unlimited farm claim made.

The patch executes at AttackBlocks return, before GameManager.explode later handles AttackEntites, copies the collection and invokes ExplosionClient/publication. It is not a post-commit receipt. The0.05 active-play interval is rate suppression, not per-explosion identity. This tranche does not prove that one ordinary native AttackBlocks call is duplicated; preserve the distinction between incorrect work evidence and duplicate physical callbacks.

Native evidence PARTIAL named-method reads from E:/_Haven/shared/REBIRTHTemp/MultiplayerAuditNative20261014: Explosion.cs SHA2562412127BEFE4B91D473E7FF8505AFA224BE5EF602F74F9EC4C60D2698E4B7964, full AttackBlocks73–260; GameManager.cs SHA2567BD2D1EC5C938E17E5EFF7F43DEAAD13190A1793329819AECE4A4AB4BAD2A63D, explode opening/copy/publication and ExplosionClient opening. Installed Assembly-CSharp hashFCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. No full containing native-class credit or new extraction.

## Release reachability and active/held features

ProgressionInstaller51/55 installs weapon/complex services and138/139 installs explosion/drone-stun patches. Services subscribe GameUpdate and start/shutdown reset. ConstructionWorkActionEnabled/ElectricalWorkmanshipEnabled/DeployableTurretPerformanceScalingEnabled true; ChemistryPayloadPotencyEnabled and DeployableTurretFeedReliabilityEnabled false. Diagnostic report strings are not qualification. No gate was enabled. Construction/electrical downstream actual commits belong Phase8/9/infrastructure dependencies, not proven by these flags alone.

Complex passive lookup -> ServiceCraftSkillService.TryGetSkillValue401 -> SkillOutcomeValueService.TryGetEffective. Remote owner reads scoped owner scalars with current projected primary attribute; authority reads complete server record. It computes bounded attribute contribution to effective skill without changing stored practical skill. This is an actual client projection integration, unlike the condition health capacity missing consumer.

WeaponFamily classifies held authored item, supports10 melee+8 ranged families, clears stale CVars/buff when unavailable, and applies handling/stamina/reload/spread/recoil endpoints plus Rage deltas for melee. Config/buffs.xml targeted groups consume them with held-tag/active requirements. It does not add direct firearm damage/RPM/magazine/projectile/jam logic. Buffs use remove_on_death=false, so reset/refresh correctness depends on the scheduled current-owner bridge; source does clear on unavailable state. Local owner and server mirror each compute, no new parallel client progression store.

## Combat producer and duplicate-event limits

ProgressionPatches first named combat class targets EntityAlive.damageEntityLocal: prefix captures alive/prehealth/stealth-before-awareness and Solo witness; postfix skips preexisting corpses, measures actual current-health loss, then sends stats and EventRouter. Its ordinary progression routing is not guarded by __runOriginal although the Solo delegate receives that flag; a skipped original normally has zero health delta but other patches changing health remain a qualified interpatch case. This partial class is not full599-file credit.

Previously full EventRouter limits credited damage by actual loss/prehit health and rejects remote world/null attacker/self; classifies attack source, resolves eligible authoritative character and asks sustained DPS profile/calculator. It queues combat progress rather than treating nominal damage as work. Stealth excludes deployed turret/drone source, records prehit undetected state before damage awareness, then adds separate stealth learning only on actual credited damage and valid live weapon profile. Rage is separate real-hit consumer. Multiple legitimate skill effects for one hit are intentional parallel effects, not automatic duplicate credit.

Neither these service inputs nor the visible router prove exactly-once physical damage dispatch; full SustainedDps batching and native damage call chain remain next dependencies. No existing duplicate physical-event evidence is weakened by rate limits, perframe queue coalescing, or an effect named 'completed'. Those mechanisms do not constitute original-event receipts.

## Drone shock, recovery and travel

Stun hook is an installed postfix on DroneWeapons.StunBeamWeapon.Fire. Handler resolves native weapon entity via reflection, authoritative owner belongsPlayerId and current skill, then scales current cooldownTimer. It awards only if target still alive and HasBuff(buffShocked), using owner+target anti-repeat interval. A preexisting buff alone does not prove that this particular Fire newly applied a successful shock; this source lacks a before/after effect witness. Exact native Fire body was not extracted/read in this tranche, so no confirmed failed-Fire award or repeated cooldown scaling is claimed. This is a precise remaining native dependency.

Meaningful stock recovery has sole production caller in CompanionService1454 (previous full companion audit), requires exact current drone.owner, Follow and minimum prior distance, and uses per-drone repeat key. Deployment/pickup/idle are not awarded here. New travel71 is called only after server/world authority gate, enumerates living Follow drones and living owner within30m, Seen admits at most one drone per owner. It resets on drone change/gaps>5s/large movement deltas and requires200m travel,60 moving seconds,100m net displacement. It awards one owner-scoped persisted cooldown event then resets trip even if the award fails. No durable original trip receipt/retry exists; work can be forgotten, not automatically replayed. Traversal does not require native walking/running tags or unattached owner: vehicle-assisted travel is not excluded by this code, but 'travel with drone' contract does not explicitly prohibit it. No fabricated bug from that distinction.

## Constitution direct-healing/exposure outcomes

Constitution training accepts actual restored health on a living player and finite positive native max, converts fractional healing through diminishing quarter/half/full-health weights, caps raw direct award in a persisted active-play window, and trains attribute directly. Exposure captures buff absence at Begin, requires buff presence on Complete, verifies eligible authoritative character and family cooldown, then saves award before separately marking/saving cooldown. Failure of first save returns false after in-memory training/window mutation; cooldown isn't set. This is not atomic exposure/award receipt admission; repeated same evidence under persistence failure could retry as another raw award until window cap if a caller redelivers it. Actual redelivery path was not proven, so retain as conditional failure-semantic risk, not confirmed duplicate reward.

Current MedicalPractice targeted Complete branches call OnHealthRestored for actual direct delta, then CompleteExposure once per mutually exclusive health/infection/general branch; deferred healing callbacks also invoke the health consumer. Multiple call sites are not evidence that the same restored health is credited twice. Full medical evidence ownership remains separate dependency. Exposure object itself has no current world/creation/one-shot token; normal caller evidence scope must supply it.

Window Reset treats started<=0 as uninitialized; at exact ActivePlaySeconds0 it can reset repeatedly. Current Condition clock advances soon after initialization, so no exploitable zero-clock healing loop established. State uses saved active time; known loaded-dead clock behavior belongs Condition trace. Direct finite input checking is weaker than unified calculator, but normal actual-health numerical range and repository validation must be proven before a corrupt-state risk becomes remote admission.

## Persistence/lifecycle and exact next work

Complex resets drone travel and passive cadence. WeaponFamily shutdown first calls FlushAllQueuedCombatProgress then resets DPS runtime. Ordering versus generic repository reset and failed flush cannot be judged without full DPS/lifecycle source; do not claim the call guarantees durable completion. Current SkillAwardService remains authoritative receipt/persistence boundary from prior trace. This tranche adds no new network mutation packet or inventory custody schema.

Harnesses check signed endpoints/constants/definitions and pure DPS math; PASS labels do not establish active runtime/native ownership. None run. Next large full dependencies: SustainedDps1626, ProgressionPatches599, ServiceCraftSkillService661, then Phase8 world-output358/Phase9 technical211 and their exact native work witnesses. Parent assigns findings/ledger updates; no shared files modified here.
