# Survivor condition, persistent health and native ownership audit

2026-10-15, audit only. No production edits/build/tests/game/gameplay commands/git/shared ledger changes. Read current coverage and agent manifests first; the Condition files below had no existing full-read manifest match. Source reachability is not measured multiplayer qualification. Existing repository, creation, metabolism and native lifecycle traces are reused without repeated full-read credit.

## Full-read source manifest

Every current file in Scripts/Survivor/Condition was read fully, together with the condition panel and authored runtime XML. Modifier309 was read in bounded overlapping segments; its115–165 boundary was reread explicitly after a broad search output truncated. StressPresentation30 was reread alone after truncation. No scan/hash-only coverage credit.

| Path | Lines | SHA256 |
|---|---:|---|
|Scripts/Survivor/Condition/RebirthClinicalMobilityTraitVectorHarness.cs|31|1FC25DB753DD8B1509E878011D37CAF9635C63B19276007BAD59EC82F5567849|
|Scripts/Survivor/Condition/RebirthConditionModels.cs|109|594B6A7F2DDED810F47916691F64C55DB3B1B06F0740BFD03F902E950D74BDB5|
|Scripts/Survivor/Condition/RebirthConditionResolver.cs|141|3BDC3C9D4309C0BCE196ABBFF0047F6279C216F511070A811E2E833B68D1A0A1|
|Scripts/Survivor/Condition/RebirthConditionRuntimeConfig.cs|175|0CB7508390A9CD24104C61BFAA5FAECB5D6903EA3838264A1DAF3DBD7EEEDB80|
|Scripts/Survivor/Condition/RebirthConditionTraitClosureVectorHarness.cs|35|E549729BD582FC721D186ABF95FC03E66C67234468362A17C5ACDD849CF81250|
|Scripts/Survivor/Condition/RebirthConditionTraitModifierService.cs|309|1CD44F5C048DA40A1F2C1AC26657A731B7B5D551C708023747A44837F309EC43|
|Scripts/Survivor/Condition/RebirthDietCompatibility.cs|152|4B6B61034A7537508327BBCCCBAE51E40F5C7EF0ABC8010D78A485345FD38BED|
|Scripts/Survivor/Condition/RebirthDietRuntimeReconciliation.cs|141|455B6FE42F27356691903DCBCCFE45360A1319C5DF8C6E6CA12F8199D157E6B2|
|Scripts/Survivor/Condition/RebirthDietSatisfactionService.cs|133|9360676E42974B66369F850640683C37B01ABF40657D598111A4123DC67C666D|
|Scripts/Survivor/Condition/RebirthFoodMoodResolver.cs|56|0FB6F40ECFB9335BDB03A8CE1D13FDFC8492E93D20589FC85F19EBCA07E28E75|
|Scripts/Survivor/Condition/RebirthHealthCapacityService.cs|204|E67757AA967B0047FED87AF691EACD03C8403D86583C7391782A6533A24D1A91|
|Scripts/Survivor/Condition/RebirthMoodService.cs|47|FCC36C95E621F0C1950CF65167E167782CD67A2A2B1ACA01AE274A0A3024AB3F|
|Scripts/Survivor/Condition/RebirthStressPresentation.cs|30|5DD81EB6C7AF1C5294EFD00F1D488AD915F6094A316EEB1F7B450C8E3E9CC551|
|Scripts/Survivor/Condition/RebirthStressService.cs|84|CA563C80FBDAB3CB47907DC280136DB02DB0A2973721FE65A88E10483BBA6022|
|Scripts/Survivor/Condition/RebirthStressState.cs|68|77A5B9A5CE96C97AD501BAA5265BFF67AAC9E277FABDC09DDD19835B498AE4AD|
|Scripts/Survivor/Condition/RebirthSurvivorConditionService.cs|167|C5512878551CA7B402197A68937D7B2A188570793DA25A8CE25C596EC92668AD|
|Scripts/Survivor/UI/XUiC_RebirthSurvivorConditionPanel.cs|91|AC0449282FE8C29E3A339F77CEBBE59C0B7098E1A9DBB96B062DC7CB7B0C2FC0|
|Config/_Survivor/condition_runtime.xml|39|28DBAF43F1C4CA32C63F7143DD70DC87820D2BFD49324882D6A4C8B5C89D9BEA|

17 C# files (1973 lines) plus XML39. Repeated WorldCharacterService70 is existing full coverage. Installer113, repository TryGet800+, stable identity resolver88+, metabolism absorption1310+, Network codec/models and native methods below are targeted caller/body evidence, not new whole-file credit.

## Confirmed remote Health Capacity application gap

Active production chain: SurvivorInstaller113 installs ConditionService; MetabolismInstaller170/183 explicitly patches native PlayerEntityStats.UpdatePlayerHealthOT prefix/postfix to ApplyNativeHealthCapacity. Server ConditionService ticks loaded players and persists HealthCapacity; NetworkService526 serializes that field and the condition/character UI displays it. These are not held/debug-only paths.

HealthCapacityService123–149 applies stored capacity only after WorldCharacterService.TryGet succeeds. That service resolves identity then Repository.TryGet; repository800–803 immediately rejects !serverAuthority. A remote client therefore always follows the fallback `Health.MaxModifier = -native HealthMaxBlockage`. ApplyNativeHealthCapacity does not consult a scoped owner projection. `rg -uuu` across all Scripts for HealthCapacity and both native application functions finds only these installed health OT calls, server condition Tick's current-value clamp, GameBridge restoration and projection/UI/persistence/creation/model consumers. No production remote owner projection application is present in that search.

Native PlayerEntityStats.TickWait at3 executes health OT, at5 uploads Changed Health; SendStatChangePacket sends ToServer in a remote world. The full native health OT body computes its native blockage and ticks the owner's Stat. Native NetPackageEntityStatChanged serializes Value/BaseMax/MaxModifier, assigns those fields to the target Stat and clears Changed, then rebroadcasts on authority. Its own-instigator primary-player guard skips an owner echo. Installed REBIRTH native reserve packet filtering covers Food/Water, not Health. Existing METABOLISM_AGENT_TRACE establishes ordinary Stats.Tick runs on nonremote living owner entities; server remote EntityPlayer is not a substitute local OT owner.

Server HealthCapacityService.Tick clamps the server copy's current Health, but does not set Health.MaxModifier or publish a native Health packet from this method. Its custom owner projection is not a native health application. Thus SP/listen owner can apply persistent capacity while a remote owner retains ordinary native max blockage even when projected HealthCapacity is lower from deprivation or trait-adjusted injury. The source-confirmed issue is divergent calculation/application, not an assertion of measured invulnerability or unauthorized damage. Current-value reconciliation and net timing need actual qualification before claiming a precise observed health outcome.

Correction boundary for parent: preserve native owner health and consume a validated current-owner/current-world/current-creation capacity projection in the owner calculation, with defined unavailable-state behavior; do not introduce competing server health simulation. No implementation done.

Native method evidence reused from E:/_Haven/shared/REBIRTHTemp/MetabolismAuditNative20261015: PlayerEntityStats.cs SHA256 C75ED0908719C18AAA5FE268370A570819C79BCD70B709D324D159D10F78B263, targeted TickWait/UpdatePlayerHealthOT/SendStatChangePacket; NetPackageEntityStatChanged.cs SHA2563146F66D13070831D59091159CE289427ABE93F7FFE1B0E6FF2D24E818F9786E full reread (already prior full native coverage). Installed assembly hash FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705.

## Active time, death, reset and persistence

Condition clock is server-only and world-mode gated. Clocks keyed by entity ID, resolved cache by stable key; GameStarting/world shutdown/game shutdown clear both. Missing/incomplete/creation-held players reset elapsed timing and do not evolve. Elapsed real time is capped to max(2,2*tickSeconds), avoiding offline catch-up after stalls; first observation initializes without an award. Pruning removes missing entity IDs, not stable-key cache entries until world reset. Reconnect entity-ID reuse can inherit a previous clock only if there is no intervening prune; no demonstrated owner mutation beyond bounded elapsed from that absence.

Confirmed dead-state behavior: TickPlayer has no IsDead guard. It increments ActivePlaySeconds/meal ages, ticks support/mood/HealthCapacity, and marks dirty/publishes on owner-sync cadence while a dead player remains in World.Players. StressService.Tick alone detects death and replaces Stress with a fresh state; returning from Stress.Tick does not stop the caller's mood/capacity tick. This differs from the stated active-play clock and can continue deprivation/recovery/aging while awaiting respawn. Native removal/respawn timing determines duration, not whether the loaded-dead branch is admitted. No game measurement performed.

HealthCapacity incorporates native injury/illness restriction once, applies sustained hydration/nutrition exposure grace, recovers slowly and never directly heals current Health. God mode restores live stats but keeps stored injury. Config currently minimum1, dehydration grace600s, malnutrition1800s, recovery0.5/min. Final condition mutation dirty marking occurs every OwnerSyncSeconds even if visibleChanged: two adjacent checks make visibleChanged unable to bypass cadence. Intermediate sub-5s changes may remain only in memory until another dirty writer or the sync tick; final repository failure/reset remains existing MP-036 scope, not a new finding here.

No condition death-reset hook was found by `rg -uuu`; initial conditions are created in starting-state initialization, while persistent condition is loaded by the previously audited repository. Stress.Read explicitly rejects NaN/Infinity and clamps nonnegative fields/Value<=100; Location is not serialized, and is recomputed on tick. Other condition fields' nonfinite admission belongs to prior SURVIVOR_REPOSITORY_AGENT_TRACE; runtime HealthCapacity/Mood Math/Mathf clamps are not finite validators. RuntimeConfig.F and trait component float parsing likewise accept nonfinite values; NaN passes ordinary comparisons. Current authored XML is finite. This is malformed authoring/save robustness, not a remote-input exploit. Config.Load mutates static settings progressively rather than committing a validated candidate atomically.

## Stress damage and state replication

StressService.Install directly patches EntityAlive.DamageEntity(DamageSource,int,bool,float) postfix under Harmony ID rebirth.stress. installed is set first, so an exception leaves retry blocked by this flag. Damaged admits positive-result player damage only in a nonremote world/Rebirth mode and only when the damage source entity resolves to another EntityAlive. Environmental/self/unresolved attacker damage is excluded. It adds capped stress and marks the server record dirty. Whether every native remote-owner damage path executes this particular authoritative postfix was NOT independently proven; don't turn this into universal damage attribution coverage.

Tick scans nearby living entities, attack/revenge targets, visibility, claim/trader/workstation camp, light and needs; it syncs stress/location CVars with network flag true and adds/removes stress buffs. StressState caps gain budgets and timer-driven decay; tea uses fixed authored item IDs through the existing metabolism consumption callback, not a new arbitrary network mutation endpoint. Stress clone is by value fields. No operation receipt exists here because this service is an effect consumer, not inventory custody; literature/profile/theory admission remains other agent scope.

## Diet/mood and replay boundary

Production meaningful-meal entry is metabolism nutrient absorption: entry.MeaningfulMealCredited is set before TryRecordMeaningfulMeal, and this server-authority/creation-hold guarded method resolves the player's committed record. No client-supplied record/trait numbers are admitted here. RecordMeaningfulMeal's public helper itself has no authority check; `rg -uuu` callers are the guarded entry, Debug console mutation and cooking preview using record.Clone. Debug paths must not be omitted from caller conclusions; no command was executed.

First-credit-before-evaluation means a failed resolution/exception will not automatically retry that meal during later absorption. Separate ingestion receipt and condition file persistence do not form a new atomic cross-file receipt here; crash conservation belongs existing metabolism custody audit. Repetition is bounded recent compatible-family history, not a permanent operation-ID ledger. Positive off-diet enjoyment is suppressed; unknown restrictive composition fails closed for compatibility without inventing penalty. Actual nutrition remains metabolism-owned. Runtime reconciliation builds read-only reports over loaded catalogue. FoodMoodResolver reads authored properties plus cooked metadata; no hardcoded item-name compatibility switch.

Trait modifiers process authored runtime components and clamp normal multipliers, skip aptitudes already baked into origin, and use native encumbrance/illness checks. HasNearbyOwnedCompanion uses native HasOwnedEntity plus radius, but no alive/type filter: any nearby owned entity satisfies that mood context. This is concrete predicate scope; whether a dead/persistent owned object remains eligible requires actual native ownership list lifecycle, not a new ownership exploit claim.

## UI/diagnostic scope and remaining dependencies

ConditionPanel is read-only snapshot text/cache invalidation; it does not apply health stats. Its source currently exists but the targeted Config `-uuu` search produced no condition-panel/rbcond_health binding hit, so do not call this legacy panel visibly active solely from Preserve attribute. Character-window source search independently finds HealthCapacity rendering. StressPresentation reads synchronized CVars and appends UI rows.

The two vector harnesses only assert supported-target predicates/live skill identifiers and return PASS/FAIL strings. They do not exercise damage, persistence, remote health application or native installed patches; no tests were run.

Remaining qualification: host+remote native Health capacity at deprivation/injury and snapshot arrival/reconnect/death; authoritative remote damage postfix dispatch; native loaded-dead duration; condition/ingestion cross-file failure scenarios if remediation is later authorized. Parent assigns any new finding ID and integrates manifest; this unique trace does not modify shared report/ledger.
