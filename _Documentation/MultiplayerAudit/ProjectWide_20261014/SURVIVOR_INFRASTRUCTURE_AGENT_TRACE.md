# Survivor infrastructure dependency and self medical audit

Audit-only, 2026-10-15. Current REBIRTH 3.0 workspace; installed native target SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. No production changes, build, tests, game, profile, commands or git execution. Only this unique evidence file written. Project operating directives apply. InfrastructureWorkService already fully read in NATIVE_TRANSPORT_TRACE.md (892 lines, current ledger hash 18d276413d0c7ba5b4b5adc9df09af082afe90dd127f7fe181bf423e532eeb69); no repeated full-read credit. MP036–040 electrical store/payment findings remain parent-owned. Scope chosen: remaining self medical practice, native consumption witness, authority and recovery. Background medical signatures excluded.

## Full-read manifest

All following complete current bodies read; eight files, 1553 lines. No existing trace matched these names before this tranche. Main coverage ledger was pending. Paths relative to mod root; SHA256:

| File | Lines | SHA256 |
|---|---:|---|
| Scripts/Survivor/Progression/RebirthMedicalPractice.cs |485|4D6DC2F7F29291395D189DD186A64A710E2925A62628253E6FC99636FA7A0E4A|
| Scripts/Survivor/Progression/RebirthSelfMedicalItemEffectScope.cs |143|E6155833A143EA9130D77E6F3E38B79587E71BEFC1A7E0709737BC139D09AA8C|
| Scripts/Survivor/Progression/RebirthSelfMedicalPracticeAwards.cs |218|412E647ACF10154704248AA744A02CD00B398CC0D30DCCFA66769D111F47E211|
| Scripts/Survivor/Progression/RebirthSelfMedicalPracticeDose.cs |193|8F1A248C0F63E3F39CAB0C886B31706EDA9B294DD31C9BAEEF4B831FF03C7A57|
| Scripts/Survivor/Progression/RebirthSelfMedicalReplicaLane.cs |357|53F8CA4D744DF58CE618AA272ECD2DA0545B6CECE57D782FA9477DDC4F2CC64C|
| Scripts/Survivor/Progression/RebirthSelfMedicalSimulationOwner.cs |52|3ABA94589A07E32CE646DCD0A24F49212880DBFE3E780AEAE4EAD2FF72498525|
| Scripts/Survivor/Network/RebirthSelfMedicalPracticeNetPackages.cs |33|20ABFA8E944924C6EAA18699EC8A1B30FABDF423075C517B68B71D06AE2AA715|
| Scripts/Survivor/Statistics/RebirthHealingDoseCheckpoint.cs |72|76D451B237E0D0ABB1011209A19DD74F6118C0D24D2A7C865D02EF31F86DE80B|

Targeted caller/dependency reads: progression installer1–125; StatisticsService276–310; prior full ServiceCraft/Award/ProgressionPatches reused. Searches used rg -uuu including ignored Debug. No negative reachability conclusion based on default rg.

## Active installation and exact native dose

ProgressionInstaller17 registers award update/reset handlers. Explicit lines90–115 install item effect scopes, added/start buff hooks, reliable and stats scalar receive, starter guard, spawn bootstrap, entity/owner/world/session teardown, Simple/Steam spawn send hooks, pending package queue hooks, instant/held dose transpilers, recovery, fatigue/delayed clinical events and death invalidation. They are active release registrations without DEBUG conditional in this slice. Optional installer failure before those lines remains a general startup dependency; actual installed runtime not observed.

Native ItemActionEat.cs at E:/_Haven/shared/REBIRTHTemp/DescriptionBindingsInspect, SHA256 3EEF3F24B1E7390974C3C7A338371A27E4ED779790B1986EB50A76513FF1D563: complete named ExecuteInstantAction196–249 and consume280–314 read, not whole class. Instant fires native start actions, performs its native UseTimes/decrement/count/controller changes, then end actions and jar output. Held clears eatingStarted, fires end event BEFORE consuming, then native decrement/use and jar output. Native accounting is preserved; wrappers invoke each original primitive once and do not retry consumption. Exact IL installation remains runtime-unverified.

Dose transpiler pins module MVID and exact assembly SHA256, compares incoming instructions/operands/labels with original, refuses exception regions, checks one DecHoldingItem and one UseTimes setter plus zero held/two instant count setters, and returns unchanged IL if mismatch. It logs bounded startup APPLIED/REFUSED. Item action must belong by reference to current ItemClass, be Consume and recognized treatment; nested actions revoke outer witness. Inventory/hand/slot/grid/item references, player/world/peer/life/creation/definition are fenced. Only one exact observed successful primitive and normal native return can confirm dose. Exceptions preserve native exception and release reservations. This is materially more specific than inventory-delta guessing.

Native Inventory.DecHoldingItem520–545 read at MultiplayerAuditNative20261014/Inventory.cs hash A73A0B1E1CFFD58A03A25183B16CF0F5320E1DE10D7D6342160C508C0E8231BF; ItemStack.Clear119–123 at same directory ItemStack.cs hash 1FD905EBAFFD5AD08795E57A4F823C6EDB10192DBA88786D903C923AEE0DF464. Native can clear ItemValue in place and reconcile hand. No claim that every last-stack completion remains reference-bound without exact Hand.Reconcile continuation; that is a pending targeted dependency, not confirmed lost award.

## Ownership and accepted remote report boundary

SimulationOwner follows native nonremote entity simulation. Offline/listen primary uses server world; dedicated remote replica cannot simulate. Remote primary requires live current world/player/connection/peer and scoped owner scalars. Writes use reliable native SET and request native local player save. No custom save acknowledgment is invented.

OwnerScope binds exact player/world/buffs/connection/life/definition plus remote peer+creation or server exact repository record+canonical/storage identity+creation. Death removes life stamp and owner queue; resets clear runtime queues. Server request requires current registered login ClientInfo, same primary sender entity, live peer, native sender check, complete current record and exact creation/definition. Response requires client world, null native Sender and current owner scope. Cross-owner request grant is not established.

Confirmed trust boundary: server ReceiveRequest accepts recognized treatment item and finite fraction0..1 from authenticated owner. It does not independently establish native consumption, injury or realized outcome. A handshake supplies epoch; exact next sequence then drives server AwardServer, including Medicine training and fracture insight. Fraction0 requests require any syntactically valid nonempty GUID and create healing-dose statistics receipt. Nonzero request receipt is derived from server epoch+sequence. Honest owner code supplies native proof; server trusts this report, consistent with native owner simulation but still relevant to a hostile-owner threat model. Do not describe this as server-verified native physical success or as a cross-owner bypass. Parent decides whether accepted owner architecture permits this boundary.

Session replay handling is real: immutable Pending body retained after failed save; Last exact body ACKs repeated prior sequence; unexpected order/body rejected; long.MaxValue rejected. Stable consumption GUID receipt survives delivery session. Clinical reward receipt uses delivery epoch+sequence; owner clinical queues are life/session bound, no durable clinical work checkpoint. On death/shutdown those observations/claims can disappear while native treatment state persists. This is an explicit source lifecycle limit; no observed reconnect duplicate clinical grant.

## Outcome allocation, persistence and failure custody

Medical Begin snapshots relevant health/reserve/infection/buff identities and captures scope before native action. Complete requires dose confirmation for self, current treatment and one completion. Immediate and delayed actual useful change feeds capped aggregate CreditedFraction. Pending claims max64 per patient/4096 global; queue64 includes reservations. Saturation preserves native treatment/quality but can refuse practice. Oldest-first health/infection tick budgets prevent stacked dose multiplying the same observed recovery. Painkiller/vitamin recognition is exact live buff event; infection aggregation burns admissions before writes/reentry and rebases every pending after partial transfer. On failed partial scalar write it refuses practice rather than count later recovery. It does not roll back already physical partial cure; no atomic cure claim.

Self clinical fractions are submitted once at FinishSelf; server calculates skill award rather than accepts raw XP. Native healing-dose statistics are separate fraction0 claims. SubmitDoseStatistics retains native CVar checkpoint before queue commit. Keys bind creation/definition/receipt/item; recovery reserves identical receipt, at most64 scoped live markers. Retirement reliable SET0 precedes local removal; full native save omits zero values. Retain returns void when checkpoint capacity full and cannot report durable persistence success: receipt retention is best effort native save/transport, not disk-confirmed exactly-once custody. Native save timing and receipt horizon in StatisticsRepository remain dependencies. RecordHealingDose targeted read persists count+receipt together before ACK; failed dirty save is retried. Metabolism has separate authority gateway, skips native Eat.consume, and calls RecordMetabolismTreatment; no duplicate native/metabolism dose counted from that source alone.

EffectScope stages quality work only during exact native item-action execution and commits on normal success. It binds MinEventContext.ItemValue and current stack/data owner; nested root revokes outer. Full quality adapter/new buff implementation is outside this tranche (installer proves registration, not full adapter correctness).

## Replica lane and reconnect/late join

Exactly21 medical scalar names, no reconstruction of Medicine/Trauma skill. SET requires nonnegative finite number and exact owner binding; server forwards changed SET once. Non-SET native operations run exactly once, then result observed (finite result; negative result allowed by observation path). Native stats reads protect admitted keys through thread-local scoped write guard. Unseeded native values remain native until bootstrap; RequestToSpawnPlayer restores initial valid scalar values; current record binds world/connection/ClientInfo/peer and is dropped upon substitution.

Spawn send postfix forwards current medical scalars to actual live recipient except owner, for both Simple/Steam. Pending entity ShouldProcess/HandleSkipped preserves package ordering by queuing medical scalars/stats while entity pending. Buff starter prevents late fracture initialization overwriting already admitted healing base during exact live buff start; unrelated nested starter masks outer. Entity/map removal, client removal/clear, world cleanup, both disconnects clear records. These are actual lifecycle hooks, not just conceptual latejoin support. Exact native queue drain ordering/current patch startup remains unexecuted dependency. Medical scalar owner reports can carry arbitrary finite nonnegative values within these21 names; source guards identity but not a server-derived quality maximum. Accepted native owner-owned scalar policy must be assessed before promoting exploit severity.

## Assessment and next dependencies

No new proven physical duplicate payment/mint or cross-owner mutation. Confirmed active owner-report trust and life-bound clinical custody limits; electrical known findings reused without duplicate credit. Pending: exact Hand.Reconcile terminal consumption binding, native saved CVar publication/flush timing, full new-self buff quality adapters and receipt bounded-history semantics. No redesign proposed or implemented; no runtime qualification performed.
