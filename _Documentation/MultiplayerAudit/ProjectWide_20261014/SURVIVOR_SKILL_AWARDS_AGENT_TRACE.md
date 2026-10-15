# Survivor unified training, attributes and Wave A native award audit

2026-10-15 audit only. No production edits/build/tests/game/gameplay commands/git/shared report or ledger writes. Prior source manifests and ledger checked. TheorySolo/Teaching/Literature are reused as dependencies, not claimed again. All negative source caller conclusions use rg -uuu including ignored Debug.

## Full-read manifest

| Source | Lines | SHA256 |
|---|---:|---|
|Scripts/Survivor/Progression/RebirthSkillTrainingFramework.cs|273|F16D115E744B2D8221450139B5EDB46E6250938BC38E5D97F679166A46A1C0CD|
|Scripts/Survivor/Progression/RebirthAttributeProgressionService.cs|51|0506757F49666C57C351987198AF912F4170F75956A35CD7285D6FA051D1D727|
|Scripts/Survivor/Progression/RebirthSkillWaveAService.cs|724|7829DEBC55E7F81C6E5FB705FE274E7D0DF985ED3989AC56E260E3AB990C933E|
|Scripts/Survivor/Progression/RebirthSkillWaveAPatches.cs|409|F84184A4F229EA4AD707839120443E8F0ECDB73BEC500D91732165743D9390D4|
|Scripts/Survivor/Progression/RebirthSkillWaveANetPackages.cs|36|D8AB1F45BDC5284F5CD4137AC7B12755CEC126CCDFF969CDD80A9F11E8725CBC|

New unique full:5 C#1493 lines. Framework1–150/151–273, WaveA1–200/201–390/391–580/581–724, Patches1–210/211–409; latter and WaveA tail reread individually after combined output truncation.

Repeated full, zero new unique credit: AwardService343 current hash33A444C572E2CC08CC75CB9E0248516838232ACDA2039F55AB1D1F468B6656D7 already ledger full; CompositeLockpickPatches94 current hash555ABD786F1811FA715F62E499C95BD3CAC11240D43AC2FEB45920086189C596 already SURVIVOR_THEORY_SOLO_AGENT_TRACE full. EventRouter93 existing companion trace reused. Config/_Survivor/skill_training.xml298 hash380A66250FAD109AC20ACED3586ACF5960F097E75F82E18892B4653849D4B3A6: PARTIAL root/common/bands/profile nodes only; large unrelated technical-work XML output truncated, no full XML credit.

Native existing inspector read-only after HAV3N expired QUIET coordination check. Assembly hashFCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. New FULL native extracts under E:/_Haven/shared/REBIRTHTemp/SurvivorSkillAwards20261015: ItemActionEntryPurchase.cs202 hash6F129FD6A568D387E1FAA8C99A31C886E1833EC68D37E27E4B443854418DF410; ItemActionEntrySell.cs273 hash2A176B5103E10032E105FA3632E9C40AEB5394DF9D099146CF01B877058DDC10. Read bounded0–180/tail and0–150/tail respectively. Attempting exact NetPackageTELock failed type resolution before other requested types; that inspector process was interrupted with Ctrl-C, then known purchase/sell types extracted successfully. No build performed. No BlockSecureLoot absence claim from that aborted multi-type call.

## Confirmed continuous skill-band gap

TrainingProfileRegistry.GetSkillBandMultiplier compares continuous practicalSkill against inclusive authored integer min/max endpoints. Current XML bands are0..24,25..49,50..74,75..89,90..100. A value in an interval gap, e.g.24.5,49.5,74.5,89.5, matches none and falls back to the LAST band multiplier0.30. This is reachable because AwardService.TryAwardMigratedTrainingEvidence passes state.Value+state.Progress into TryCalculate, and continuous evidence calls GetSkillBandMultiplier with it.

Result: the last fractional level before each threshold receives0.30 rather than its preceding1.00/0.80/0.60/0.45, then rises again at25/50/75. At90 onward0.30 is expected. Negative values below first min deliberately receive first band, a separate defined fallback. The registry requires five bands but does not validate continuous adjacency. Harness tests integer50 and ordinary0, not fractional boundaries. Source-confirmed calculation defect; no runtime test executed. Parent assigns identifier. Correct interval semantics require review; this audit changes neither tuning nor live flags.

## Active versus held and numerical admission

ProgressionInstaller18 loads profiles,49 installs WaveA,122–126 and135–136 explicitly install lock/composite/buy/sell patches. XML root live_awards=false disables only generic TryAwardTrainingEvidence. Route-explicit TryAwardMigratedTrainingEvidence deliberately bypasses that global hold and is used by WaveA; do not describe all practical awards as disabled. StealthMovementTrainingEnabled=false and OnArmoredCombat no-op mean the old movement stealth/hit armor sources do not pay here. Separate actual combat stealth service remains outside this full tranche.

Registry validates finite/ranged common numeric values, LOCKED profiles, uniqueness and complete skill registry coverage. Profile descriptive formula/coefficient strings are not an executable proof of native work. Continuous calculator requires authoritative-success flag, finite practical/work/rate, positive work/rate, unit0..1 relevance/repetition/assistance and finite normalized/final raw gain. It caps equivalent seconds and discrete award. Unknown enum values take the discrete else branch internally; no client supplies this evidence envelope on the WaveA packet. Snapshots/lookup return mutable profile object references despite read-only comment, but no malicious runtime mutator was identified.

AttributeProgression.ApplyTraining applies diminishing rate relative to potential and authored caps, with primary attribute mapping from definition. It has no direct authority/finite guard, so its callers must supply validated server records/numbers. Caller search finds core AwardService and ConstitutionTrainingService; no arbitrary network attribute-point setter is introduced here. Core award uses calculated finite skill progress times runtime coefficient. Malformed coefficient/state finite admission remains configuration/repository boundary; no remote-input exploit inferred.

## Award receipts, persistence and native XP ownership

Core AwardService repeated full trace: only authoritative Rebirth nonremote record and no creation hold are eligible; no IsDead guard in the generic eligibility helper. Route producers still own success/death context. Receipt hit retries exact dirty character snapshot before returning true. Finite raw/projected gain and capped skill state precede primary attribute training and one record.Touch. Durable award returns false when save fails, retaining dirty original for replay; nonreceipt award returns true after in-memory mutation even if save failed, and queues owner publication retry. Final reset handling remains existing MP-036 family. No transaction rollback was invented.

Anti-repeat keys are skill+source on persisted character ActivePlaySeconds. Offline time does not consume them; Condition trace's loaded-dead active clock can advance them while dead. Receipts and effect live in one character snapshot; pruning uses TeachingOutcomeStore pending/backup/provider protection. Generic bounded ordinary receipt eviction is not permanent exactly-once memory for arbitrary old IDs; caller durable original/retention scope remains essential. No duplicate award demonstration added.

Owner publications coalesce by entity ID plus original EntityPlayer reference and world identity. Flush rejects rebound/reused entity objects, retries dirty storage, then force-publishes current owner state. This preserves native owner architecture; the custom skill system does not replace all native XP methods. Exact native Sell.OnActivated still invokes entityPlayer.Progression.AddLevelExp(...Selling) after actual NPC sale. REBIRTH hooks observe its outcome and add custom skill/attribute effects, not claim removal of native XP.

## WaveA movement and passives

Server samples qualified native walking/running-tagged horizontal displacement, rejects attached entities, tiny deltas and over-max samples, normalizes native speed/mobility to neutral references, accumulates Athletics and burden-weighted armor distance. It does not grant for jump-only spam or a flat incoming hit. Actual per-sample awards are discrete migrated evidence with no durable receipt; distance is consumed before award result, so transient eligibility/persistence behavior cannot be read as queued durable work. Long frame gaps are not integrated as offline work.

Native passives are explicitly applied to remote owner via scoped OwnerScalars, unlike the Condition health-capacity gap: current skill/attribute/background projection resolves the same endpoint calculations as server record. Unknown/unavailable owner state clears these CVars/buff. Buff XML176ff consumes LockPickTime/BreakChance, Bartering/SecretStash, Jump/Stamina/Fall and other CVars. Equipment burden is read locally on owner and from mirrored equipment on server; this tranche does not prove all equipment replication paths current. Process dictionaries reset at start/shutdown; server MovementByEntity/TradeWitness dictionaries do not prune absent players here, so entity-ID cache retention/reuse needs native identity lifecycle context, not an invented cross-owner award.

## Lockpicking current/legacy reachability

Legacy GameManager.TELockServer signature is absent from the existing full native GameManager extract, and exact NetPackageTELock type resolution failed on installed assembly. Those fallback adapters must not be called current live proof. However the explicitly installed current CompositeLockpickStartPatch uses TEFeatureAbs.OnLockResponseServer, main thread/current server, exact native single-lock owner/feature parent, finite positive timers/progress and nonair downgrade. Success adapter uses TEFeatureLockPickable.EventData_Event and native original execution. Existing TheorySolo trace owns deeper native feature transition proof; this audit does not repeat that scope.

WaveA stores one timed lock attempt per entity/position, expected original meta/rotation and downgrade type, earliest completion and expiry. Success packet sender is authenticated and must remain near target with matching current block/authoritative unlocked state. It removes attempt before technical award/insight, so repeated success messages cannot reuse it; no durable failed-award retry is provided by this transient attempt. Captured block incarnation is structural value, not a unique persistent block identity. No rapid replace/relock exploit proven.

## NPC commerce outcome and remote inventory witness limit

Buy/Sell Harmony hooks capture local requested item/count and native before/after currency. Native purchase/sale full reads show actual mutation occurs in owner XUi inventory/model, with TraderData.SetModified in finally. Purchases require currency and space; sales remove selected stack and add currency only on native successful swap. REBIRTH excludes vending/owner routes and reports only positive currency delta. It observes success without rewriting native custody.

Server packet validates sender and action enum, resolves live player, requires eligible record/nearby living trader, bounded count/value, neutral EconomicValue ceiling/minimum, and a sampled bag+belt item/currency delta within3 seconds. It consumes prior witness candidates even on failure, preventing reuse/probing one baseline. It then persists per-item/global/opposite-direction anti-loop state and variety attenuation. Anti-loop is consumed even if skill capped; reconnect cannot simply reset it once saved.

Important qualified functional dependency: ordinary remote commerce native mutation lives on owner inventory, while this witness compares server EntityPlayer bag/inventory. Existing NPC_TRADE/METABOLISM native analysis shows owner inventory uploads update Sender.latestPlayerData, not every live remote grid. Hence current witness is not proven to see an ordinary post-join remote transaction; same stale-grid limitation can reject valid remote training. No unqualified native inventory authority is inferred from the method name ConsumeAuthoritativeTradeWitness. Exact full trader upload-to-live-grid refresh chain remains required before upgrading to measured failure. A paired currency/item delta near a trader also is not an authenticated native transaction receipt; coincident nontrade changes could satisfy it, but no complete exploitable chain is demonstrated.

Committed stash sale explicitly bypasses bag-delta witness only through internal ReportCommittedStashSale, called from RebirthBackpackBatchSaleEffects after its own settlement chain; that caller's custody is existing parent scope. No client boolean selects bypass. This method still checks trader/value/anti-loop. No arbitrary-other-player grant or new duplicate-credit exploit established.

## Remaining dependencies

Parent should verify/publish fractional-band defect and integrate5 unique source manifests. Runtime host/remote native movement tags, trade-witness freshness, composite lock completion timing and faulted save/reconnect are unexecuted qualifications. Broader combat/world-output/technical/Constitution producers remain separate pending sources unless already covered by other agent manifests. No disabled live_awards/stealth gate was enabled.
