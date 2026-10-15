# Implementation and batched validation plan — draft

This plan is provisional until the project-wide audit is complete. The required order is audit report, final implementation/testing plan, Git checkpoint, all production coding, then testing. No production change is authorized by a speculative finding alone.

1. Complete coverage and classify confirmed defects, unresolved risks and existing controls. Map every correction to a finding and an acceptance assertion.
2. Review current shared changes and create a Git checkpoint before implementation. Preserve concurrent source/assets; exclude generated output and credentials. Record commit ID in the final report. No push requested.
3. Implement independent confirmed corrections in one coding batch. Coding agents may own disjoint files; parent owns integration and review. Add Bridge scenarios/observers during this phase. Do not run tests between each small edit.
4. Freeze/hash the source and build once when the game is closed. Run cheap offline/protocol/serialization checks first after coding. A build is not multiplayer proof.
5. Start one disposable CodexTest single-player/listen-host session and run all compatible scenarios in it. Prepare fixtures once, then use native Bridge inputs for actions. Separate setup cheats from the behavior being tested. Collect independent failures without restarting after each assertion; stop on broken session or item-conservation uncertainty.
6. Run the remote-client batch against an actual host or dedicated server with an independent client session. Record both endpoints, build hashes, world/save identity and player roles. Never relabel a simulated remote path as a real network test.
7. Batch concurrent access, delayed/duplicate responses, disconnect/reconnect and late-join cases. Assert item identity/metadata/count conservation, one-time effects, correct ownership, terminal response, and release of holds/leases. Capture authoritative state and both clients, not only the initiating UI.
8. Perform one deliberate save/reload checkpoint for all persistence scenarios, then verify all prepared durable states together. Additional reloads require a specific unresolved failure, not a default per-scenario workflow.
9. Consolidate failures into the next coding batch. Repeat only affected scenarios and justified shared regressions. Deliver report with PASS/FAIL/NOT RUN per topology, evidence paths and remaining limitations.

## Findings-to-batch mapping (provisional)

| Findings | Coding scope after checkpoint | Offline assertions after coding | Shared gameplay batch |
| --- | --- | --- | --- |
| MP-001 | Bind category replies to the original UI request/session | Closed/reopened UI, reordered reply, wrong target | Container management and transfer batch |
| MP-002 | Establish actual native fire event authority before selecting a correction | No duplicate mutation across local/native/server paths | Remote ignition/extinguish/remove, authoritative and observer state |
| MP-003 | Explicit independently validated Bridge endpoints and topology report | Wrong PID/token/save rejected; skipped prerequisites reported | Actual host plus remote client; dedicated server where available |
| MP-004, MP-013, MP-017 | Retryable staged installation, rollback-aware shared patch bookkeeping and accurate completion status | Inject patch/registration failure; retry without duplicate callbacks; inspect actual hooks after rollback | Startup status, concrete secure-object access and one world save/disconnect batch |
| MP-005, MP-006 | Null-safe legacy result timing; reconcile legacy entry points with accepted transfer protocol | Diagnostics off/on; exact pre/post inventory and repeated receipt | Quick Stack/restock with full bag and simultaneous participants |
| MP-007 | Original consumption operation/source binding and retained settlement | Changed source, repeated/late receipt, save failure | Food/honey consumption and reconnect; effects exactly once |
| MP-008, MP-009, MP-010 | Retain withdrawals/refund remainder through cancellation, full inventory and reconnect | Partial insertion, unknown outcome, eviction pressure, old-session delivery | Cooking/crafting/repair batch, then shared reconnect/reload checkpoint |
| MP-011, MP-012, MP-019 | Validated load state and durable file publication, including metabolism schema/recovery | Corrupt primary/backup, wrong schema, duplicate identities, failed replacement, surviving candidate | Names, metabolism and offline quest state checked together after reload |
| MP-014 | Balance Bridge request accounting, including health probes | Repeated ping and completed/abandoned/failed requests leave correct outstanding count | Verify batch runner liveness polls do not suppress idle agenda |
| MP-015 | Retain original workstation completion through deferred award and post-award stages | Award save fails then retries; each later effect is settled once | Native output completion, viewer/owner disconnect and shared reload batch |
| MP-016 | Verified world-scoped journal index with per-vehicle pending lookup | Publication invalidation, corrupt history, restart reconstruction and file-read counts | Multiple campers with growing retained history; measure main-thread cost alongside recovery |
| MP-018 | Hydration equipment original-owner transfer settlement and verified setter postimages | Exact item/liquid metadata, refused setter, partial save, repeated outcome | Host/remote equip/unequip, full bag, competing holds and shared reconnect/reload |
| MP-020 | Stable fire delta replay order through snapshot commit/abort/replacement | Same-position transitions sharing a batch revision, interleaved entries | Remote fire projection during reconnect and snapshot assembly |
| MP-021 | Fire failed-load write quarantine and preserved recovery candidates | Invalid/unsupported/missing primary and backup; shutdown following failed recovery | Fire persistence checked in the shared save/reload batch |
| MP-022 | Recoverable packed-container restore reservation/publication | Fail each publication stage; duplicate retry; preserve exact contents and settings | Pack/place with remote player, then shared reload and conservation check |
| MP-023 | Durable workstation security publication and recovery gating | Failed write followed by retry preserves usable backup; missing primary recovery | Owner/allowed/denied users and locks after shared reload |

These assertions are planned, not executed. Production coding remains gated on completing the audit and recording the Git checkpoint.

MP-024 adds verified original-action water/item settlement to the farming batch: collect/fill with buckets and jar stacks, refused setters, changed held slots, simultaneous players, repeated delivery and saved conservation. Share its reconnect/reload with the other persistence scenarios.

Farming follow-through also extends MP-004 to explicit farming patch-install failures and adds the Release diagnostic acknowledgment defect documented in FARMING_TRACE.md: disabled diagnostics must report unavailable, never started. Validate Debug/Release admission and partial installer failures offline before the grouped farming session.

MP-025 covers authenticated-requester diagnostic session isolation, bounded start frequency, and truthful Debug/Release acknowledgments. Offline cases should include two actors supplying the same label, repeated starts and disabled diagnostics. Real multiplayer evidence must identify independent endpoints rather than relying on HOST/SERVER log labels.

## Bridge runner requirements

MP-031 adds sink reconnect availability without weakening item conservation. Implement durable owner intent or an authenticated original-operation resume/cancel protocol after the audit/checkpoint gate. Preserve existing uncertain journals; never cancel solely because a receipt is missing. Code the interruption matrix in SINK_RECOVERY_TRACE.md before execution, and share camper reconnect/save/reload sessions. Completion requires both conservation and the ability to perform a subsequent transfer after recovery, including a replacement player entity ID. Runtime and independent remote/dedicated evidence are currently NOT RUN.

- Explicit endpoint/session files; validate PID/start time/token and disposable save before actions.
- Scenario time budgets and polling on observable completion instead of arbitrary long sleeps.
- Separate prerequisite failure from assertion failure; never report skipped cases as passed.
- One aggregate machine-readable report plus concise human summary, native-input action trace, errors since baseline, and screenshots only where visual assertions matter.
- Keep native guard/instincts enabled. Never launch over the user's game, manipulate a real save, or build/install while 7DaysToDie is running.
- First inspect existing scripts and reuse supported observations. Avoid duplicating runners or stale fixtures simply to increase test counts.

## Additional diagnostic implementation coverage — MP-026

Temperature tracking must participate in ResetAutomationState alongside suite/benchmark cleanup. Invalidate its generation before releasing the old world; check world/run identity before sampling, and check completion before taking another interval sample. Test an already queued callback after stop/world replacement and a no-world interval followed by a new world. These cases belong in the same diagnostic lifecycle batch, not a separate game launch.

Include the confirmed temperature-override lifetime extension: capture prior setup state for a diagnostic run, restore it on same-world completion/cancellation, and clear world-local override state on world replacement. Generation-bound cleanup must not restore an old world's override into the new one. Code cases for normal completion, explicit stop, exception, shutdown/start in one process and replacement of an active run. Use disposable worlds only; do not invoke mutating diagnostics in the user's test session.

After checkpoint, replace temperature-tracking thread-pool world access with bounded main-thread sampling and world-generation cancellation. Include other console timers in the same ownership review before editing, preserving useful diagnostics. Offline scenario checks: thread affinity, stop/replace, shutdown/start, stale queued work and duration boundaries. Batch one actual disposable-save lifecycle check with existing farming reload scenarios. No tests run yet. Old catchupwatch dispatcher is disabled; do not spend implementation time treating its dormant deadline mismatch as an active multiplayer failure.

Vehicle Studio has claimed RebirthCamperCampStationAppearance.cs and an isolated asset build for deployment/packing fades and door presentation; reserve that ownership and integrate only after inspecting its final manifest. No change to this audit-first/checkpoint-before-implementation sequence.

MP-026 scope extension: farming-suite phase transitions also run world scans, cache invalidation and growth-policy broadcasts from a System.Threading.Timer callback (ConsoleCmdRebirthAdvancedFarmingFresh 1764–1858). Migrate suite scheduling alongside temperature tracking. Preserve lifecycle cancellation already present for suite/autobench, add per-run generations to reject queued callbacks after replacement, and explicitly restore diagnostic setup policy. AutoBenchTick's observed path is snapshot/reporting, not the suite's world scan; review counter concurrency separately.

Vehicle wire-size investigation: native NetPackageVehicleDataSync uses a ushort entity-data length, while assembly persistence permits 262144 decoded bytes before Base64. Establish normal content reachability and constrain the complete native sync envelope before publication; include boundary fixtures for combined ItemValue metadata. Keep this distinct from native trusted-client authority and avoid broad inventory replacement. Evidence in VEHICLE_RESTORATION_TRACE.md; tests NOT RUN.

MP-026 diagnostic sampling follow-up (2026-10-15): FARMING_TRACE.md now records a separate Debug stutter sampling ownership defect. Start invokes StopLocked while inactive and overwrites the current sampling preference before capturing it; restoration via SetSampling(true) resets counters before the final report, and world reset can restore stale enabled state after disabling sampling. Include initially enabled/disabled sampling, repeated traces, final delta preservation and teardown ordering in the diagnostic lifecycle correction and offline scenarios. This tracer itself pumps on GameUpdate; do not replace it as though it were the temptrack timer. Gameplay/network corruption is not asserted.

MP-026 counter/switch validation additions: reset the block-light diagnostic bypass at world boundaries; qualify Debug and Release command availability explicitly. Give diagnostic samples a run/reset identity or terminate incompatible active sampling on reset, and reject cross-epoch deltas. Cover manual perfsample reset during trace, initial sampling on/off, repeated start/stop, teardown and replacement world. Treat GC.GetTotalMemory deltas as overlapping process-heap observations, not per-feature allocation evidence; reserve attribution for the later profiler goal.

Farming validation additions (2026-10-15): tank/collector pickup with full and empty storage, competing remote pickup, rejected/throwing delivery and persisted postimage; stove conversion fixtures for missing TE, legacy light, existing workstation inventory and unexpected tile type, plus remote late chunk arrival. Preserve unexpected saved data pending a justified migration policy. Do not accept AdvancedFarmingProtectedGrowingVectorHarness PASS as behavioral evidence: it tests literals only. Use actual production heat/enclosure services and grouped host/remote crop cases. See FARMING_TRACE.md for source evidence and unresolved native grant/drop boundary.

SUN/light validation additions (2026-10-15): distinguish unbounded pending SUN jobs from the bounded geometry projection queue. Exercise sustained neighboring door changes without clipping merged coverage, chunk readiness delayed past deadline, unload/reload with another farm active, rebuild failure after dequeue and late-join convergence. Confirm current-world/definition generation for ID-only caches before requiring reset changes. Compare actual light/thermal results for cube/frame, rotations and overlapping shape-name categories; static classification/harness constants are insufficient. See FARMING_TRACE.md; these remain qualification risks, not reproduced failures.

SUN rebuild fault cases: unavailable neighbor chunks during door refresh, lateral opening outside the local rebuild box, pre/mid-commit exceptions, and failure while publishing one crop's updated light state. Check eventual repair and host/remote eligibility independently. Preserve conservative deferred repair rather than silently dropping required world-mutation work. Source trace in FARMING_TRACE.md; no runtime incident claimed.

Dynamic-cover fixture additions: same door state before/after neighboring chunk arrival; ordinary door, powered door, shutter, curtain/half-curtain and centered nonglass cover with ambiguous aperture; multiblock child-parent rotation/damage; 1 damage versus actual model damage-state transition. Historical rotation comments do not replace current native/host-client evidence. Preserve existing matrices pending demonstrated failures.

MP-026 hover lifecycle cases: enable trace, switch worlds/reconnect, confirm tracing/counters reset and ordinary text caching resumes. Farming UI projection cases: differing host/client GameTimer epochs, delayed authoritative state, same-position crop replacement, backward same-day world-time adjustment, and local water/temperature arriving separately from authoritative light. Do not use countdown reaching zero as crop-completion evidence.

MP-032 implementation/test linkage: preserve the bounded search and distinguish exhausted work from refused admission. Either size admission storage to the bounded volume while retaining the processing budget, or explicitly propagate refusal of a new in-bounds unvisited cell as incomplete. Do not mark duplicate admission as overflow. Return incomplete without caching unless a valid exterior path is found. Add production-backed cases for genuine enclosed completion below budget, saturation with remaining cells, an exterior path among late-admitted cells, missing chunks, and repeated cached evaluation. Compare authority and remote projection with the same geometry. Code/scenarios after the inspected checkpoint; execute with the grouped validation batch, not during user testing.

MP-033: introduce an explicit deferred preparation outcome for incomplete thermal geometry in the live active path, preserving elapsed baselines and avoiding water/growth mutation. Keep genuine blocked-condition time discard semantics. Do not implement by returning ordinary false alone. Cover loaded plant with missing adjacent thermal chunk, block-light shortcut fallback, chunk arrival/retry and capped elapsed growth; compare with shared catch-up, checking provider balances and replicated plant progress. Diagnostics should expose incomplete status rather than imply certainty. Batch runtime fixtures after production/scenario code and checkpoint.

MP-034: carry explicit geometry availability through cover presence, block snapshots and sky-column search; do not cache unknown as proven open or conflate it with accepted no-cover trust. Coordinate incomplete-result handling with MP-033 so unknown is deferred without consuming growth/water or losing elapsed baselines. Test boundary unload/load with sufficient stored SUN and blocking covers, plus genuinely open loaded columns and ordinary no-cover cases. Complete native GetBlock reachability trace before implementation finalization; gameplay NOT RUN.

### MP-035 implementation and grouped validation requirements

Introduce crop-specific accepted-transition settlement while preserving native owner-local inventory delivery. Correlate each original harvest intent to authenticated owner identity, exact world/position and expected crop incarnation/state; authority admits only one transition for that instance. Deliver the accepted yield through an idempotent owner receipt, including overflow disposition; do not grant speculative output before admission. Retain unresolved custody through disconnect/reconnect and save/reload, and suppress fallback native harvesting while the action is pending. Do not implement a blanket server-only inventory rewrite. Include host-local actions in the same exclusivity rule.

Code all scenarios before execution: two independent actors hit one mature crop before projection convergence, host plus remote, two remote clients, repeated intent/receipt, delayed or reordered response, full/partial inventory, disconnect at admission/delivery/settlement, and replant/new incarnation at the same coordinate. Assert exactly one transition and conserved accepted yield plus overflow, no duplicated farming XP, and recovery without replaying later owner inventory changes. Single-player/offline checks cannot pass the competing-client case. Group with existing farming conservation tests in one authorized disposable-world session; genuine multiplayer cases remain NOT RUN until independent identities/endpoints exist.

### MP-036 implementation and qualification

After the inspected checkpoint, introduce explicit placed-workmanship recovery/publication state. Failed existing-store recovery must preserve all candidates and disallow destructive publication; absent stores may initialize normally. Make save outcome observable so shutdown does not silently discard failed pending custody. Preserve format compatibility, atomic temporary verification, author identity and pickup semantics. Code disposable offline cases for missing/valid/corrupt/unsupported primary and backup combinations, post-failure placement, repeated write/publication failure and shutdown/reload before executing them. Batch host/remote placement-pickup/reload checks with the other provenance scenarios; independent-client evidence remains required for multiplayer acceptance.

### MP-037 implementation and qualification

Unify provenance compatibility across partial/native and reservation replacement merges without losing cooking/liquid/packed-container or quest guards. Make incoming-context ownership explicit and exception/nesting-safe; account for Priority.First reservation loops instead of assuming the ordinary scope ran first. Preserve native remainder/event behavior. Code cases for identical/different/absent/unsupported metadata, electrical and ordinary placed-block provenance, both inventory destinations, partial/full capacity and reservation-active insertion. Then execute offline/native qualification and grouped UI pickup/quick-stack checks after all coding. Remote owner receipt insertion must retain metadata and conserve counts; server-only inventory rewriting is out of scope.

MP-037 sort coverage: activate metadata-safe combination for provenance-bearing ordinary items too; preserve ignored/locked slots and sorting behavior. Cases must include only ordinary stamped blocks, mixed stamped/unstamped blocks, differing authors, and the same inventory with an unrelated liquid added. Verify counts and per-stack metadata, not only the final total.

MP-037 UI qualification: include ordinary unlocked backpack whole-cursor merge and single-item placement with differing non-liquid provenance. Native methods bypass CanStackWith; verify incompatible whole stacks swap without metadata/count loss, single placement refuses or uses the agreed compatible behavior, half pickup preserves metadata, and reservation/lock/custody guards still take precedence.

MP-036 coverage extension: apply explicit recovery/publication and shutdown-save outcome handling to InfrastructureElectrical.dat too, retaining v1/v2 migration, finite/duplicate validation and shared durable publication. Use the same failure matrix with electrical condition/configuration records.

MP-038: unify electrical-store admission before paid or carried-state commitment; preserve existing records and unresolved carried metadata. Code boundary/reload tests at8191/8192/8193 and updates-at-capacity before execution. Also correct service efficiency feedback to compare one native baseline with one adjusted demand, retaining ReadCampPowerUsed capture semantics.

MP-039: implement authoritative committed source-removal/replacement cleanup with incarnation-aware validation, preserve unload and valid upgrade/pickup continuity, and qualify generator/battery normal damage, explosion, same-coordinate replacement by a different actor, downgrade, save/reload and record-count reclamation. Batch these with electrical capacity/recovery tests; do not clear records merely on missing loaded tile.

MP-040: implement original-operation per-source payment settlement for electrical service and review other TryConsume callers; conserve exact metadata/counts across proven and uncertain commits. Cover mixed-source rejection/exception/retry/reconnect and constrained compensation, with no whole-plan blind refunds.

MP-040 caller matrix: include upgrade/repair/paint wrappers that omit receipt outputs; separately validate inventory RemoveItems callers, cooking's explicit ReturnPulledIngredients compensation, and remote terminal outcomes that retain Removed evidence. Do not apply one undifferentiated refund to all callers or duplicate existing retained receipts.

MP-041: replace cooking's retained pre-debit NPC snapshot return with operation-bound authoritative refund settlement. Acknowledge only committed quantity, retain uncertain custody and allow fallback only after known refusal. Remove uncommitted/stale origin receipts without dropping proven debit evidence. Code logical companion/dog successful pull then full/partial cancellation, multiple ingredient types, concurrent revision change, source removal, repeated callback and constrained fallback cases before executing tests. Include authoritative cooking and separate remote-client receipt routes; do not assert one exercises the other.

MP-040 native caller qualification: upgrade and paint gate their effects on the removal bool; ordinary repair ignores it and proceeds to repair/XP/tool wear after preflight. See NATIVE_TRANSPORT_TRACE.md. Plan repair at whole-action granularity, including all materials and percentage bookkeeping; merely returning false from removeRequiredItem is insufficient. Offline/listen owner path confirmed; independent remote behavior remains separately qualified and NOT RUN.

MP-042: join personal native queue publication to resource settlement at the action boundary. Do not use ForceCancel as rollback for unpaid jobs: its recipe-derived refunds are not debit evidence. Preserve existing paid queue restore and native owner semantics; include queue-full-after-payment, failed companion commit, partial proven debits, unknown outcomes, cancellation/completion and quality ingredient cases. Share operation custody with MP-040/041, and separately qualify remote grant resumption.

MP-036 preferences extension: include RemoteResourceStateStore's primary/backup failure and failed final flush, preserving per-player exclusions and activation state. Distinguish new absent store from unresolved existing files. Make unload settlement observable before clearing state; preserve RBRR1 migration semantics and RBRR2 player-specific behavior. Validate corrupt/unsupported stores, failed publication, retry and world switch with disposable fixtures, separately from item-custody assertions.

MP-036 workstation-security implementation: replace direct-primary overwrite with verified temporary publication preserving a known-good backup; recover backup when primary is absent; quarantine unresolved recovery before placement/legacy migration can overwrite it; retain failed shutdown state. Preserve v1–v4 credential reset migration and fail-closed access. Code repeated write failure (including corrupt-primary-over-good-backup), missing primary/good backup, duplicate/trailing data, accepted lock/password then failed shutdown, and reload tests using synthetic credentials/disposable files only.

Diagnostic report correctness: replace stale literal all-flags-false/no-consumers claims in RebirthHotPathFeatureFlags and Debug RebirthRuntimeArchitectureHealth with observed values and explicit unverified integration status. MapMarkersReservationsEnabled currently=true with a boss-event marker consumer. Keep reports read-only and retain Release facades; do not use green labels as acceptance evidence.

### MP048–054 grouped implementation/test linkage (draft; audit still open)

After complete audit and inspected Git checkpoint: MP048 remote trading must share original owner settlement principles with MP007 consumables rather than copy live server grids. Implement producer/source binding, outcome/recovery records and remote publication together; MP050 wildcard exact-override resolution belongs in the same trade tranche. MP049 needs an all-store scope/reset matrix, with old failed-save custody kept under its original save identity; batch with MP045/046 fault recovery.

MP051 changes owner stamina projection consumption only after current owner/creation snapshot validation. Code offline snapshot/creation guards and independent host/remote energy scenarios before execution. MP052 must transport request/epoch even for empty native quest lists and reject retired responses; MP053 must authenticate event-specific initiating member/owner without weakening owner removal. Batch board refresh, shared party acceptance/removal and history/reconnect fixes in one authorized quest session.

MP054 requires per-iteration recycler dispatch preserving native clock carryover, plus ordinary required-tool degradation where tool has native degradation. Managed cooking's bypass of configured extra outputs (COOKING_AGENT_TRACE and COOKING_XML_AGENT_TRACE) joins MP028/029 output admission/frozen recipe settlement work: preserve every configured output and original input custody. Code native ordinary->scrap transitions, stalled output/negative carryover, broken/degradable/nondurable tools, bonus once, stew jar+can byproducts, open/closed/mobile queues, cancel and reload before grouped test execution. Source queue capacity hypothesis was disproved: custom workspace/mobile queue expansion16 covers authored maximum8 output rows. Do not add an unnecessary capacity redesign.

Separate offline/protocol fixtures from real independent-owner tests. No source-only evidence may pass actual remote multiplayer. No tests executed during current audit and no production coding begun.

MP055–059 added linkage: batch managed byproducts/recycler clocks/tool wear/fuel progress in one station session after all code/scenarios. Boss reward native lock tests require owner/nonowner plus unload/reload, not a UI-only assertion. NPC melee needs native actor tests for melee AND ranged and observer client evidence; distinguish menu/manual authored class from absent population spawner. Toolbelt clear needs capacity/locks/custody controls and exact slot conservation. Camper six-seat handoff is held until native rig composition and restoration implementation are reviewed and validated; merge explicit deltas only, preserve production XML and matching bundle ownership.

### Diagnostic validation safety requirement

NPC_DIAGNOSTICS_AGENT_TRACE.md establishes production Inspect qualification paths can award real shared progression, and NPC_CORE_PERFORMANCE_AGENT_TRACE.md establishes DEBUG scale benchmark clears shared live queues/leases/spatial state. Do not execute these as harmless source-qualification shortcuts or on user saves. Coding plan must isolate self-test services/fixtures or explicitly make destructive diagnostics operate only disposable isolated state, preserving intentional repair command behavior and native console permissions. Record test build/world identity and actual measured checks; hardcoded PASS text/conformance matrices are never multiplayer evidence. Automated tests remain gated until all implementation/scenario coding is done and user authorizes gameplay.

MP062 implementation linkage: enumerate clean/no-op SaveIfDirty callers and separate durable-state verification from new publication success. Correct exact pending study and duplicate-literature paths, retain owner creation/outcome/receipt checks. Offline fault fixtures must persist/load clean pending and applied stages then retry without unrelated dirty writes; grouped gameplay covers timed physical/audio/solo study, duplicate untimed QuickUse and reconnect. Do not solve by weakening shared save outcome globally. All production/scenario code before execution.

MP063 implementation/test linkage: bind original native kill witness before mutable killer reference is cleared, authenticate native successful outcome and preserve target-life dedup and receipt retirement. Include real melee AND ranged kill tests and host/remote actor/source exclusions in grouped combat session after all code/scenarios. Offline call-order fixture uses exact native cleanup sequence; it cannot substitute for actual remote damage transport qualification.

MP064 groups with MP051 owner-native Energy/Stamina projection correction. Shared current-owner/creation snapshot validation may be reused, but native Health/MaxModifier and stamina have different semantics; preserve single canonical native health blockage and god-mode behavior. Add independent owner/server snapshots and actual heal/damage/recovery assertions before execution. Remote topology remains NOT RUN without independent identities/endpoints.

MP066/067 grouped validation: repair qualification uses authenticated native upload blobs and exact original action/output, never stale server grids; stage deferred output and full inventory before testing replay. Salvage bonus negative/stale claims use disposable offline protocol fixtures, then real host/remote salvage actions with collected base-output witness; native source denial must produce no bonus. Complete all production/scenario code before executing any fixture/game session.

MP068 joins gear original/refusal/terminal recovery plan. Validate authored slot membership BEFORE owner hold publication, retain recovery for legacy held unsupported slots, keep all accepted slot identifiers consistent across item/profile/intent/wire/state/refusal/UI presentation and host effect code. Group with delayed prior-offer inbox stall evidence in SURVIVOR_GEAR_RECOVERY_AGENT_TRACE; distinguish no-item-loss liveness from conservation failure. Offline fixtures + real remote Use/reconnect planned after complete coding.

### MP069–070: camper physical scope and surviving sink HUD

After audit closure/checkpoint, repair remote request identity construction with existing projected local scope while retaining authoritative server validation (MP069); place sink HUD after final toolbelt replacement (MP070). Code scenarios before execution: native/Rebirth host and remote, held/replaced creation, reconnect, focused station versus camper M prompts, manual/automatic door behavior, sink500mL/10L transfer and30L gauge/modal visibility. Group with camper/energy session. Turret controller errors require current runtime qualification; current source/DLL contain types, so no speculative implementation task is authorized by old-log evidence alone.

### MP071: remote station failed-send state

Code exception-safe close/command submission with identity-correlated retry/recovery; preserve unresolved custody and successful station-switch cursor behavior. Offline failure injection and grouped camper remote session scenarios must cover send throw, accepted-but-reply-lost, duplicate close response, native closed window, reconnect/world change and reopening. Do not treat removal of pending flags alone as permission to replay payment.

### MP072: modern logistics owner custody and outcomes

Replace stale entire-owner-bag response application with existing native owner preparation/receipt patterns, exact operation-scoped state and saved outcome recovery. Mismatch refusal carries no stale inventory replacement. Preserve unrelated current native items, container protections, shared NPC transfer permissions and credits. Integrate exception-before-outcome and partial-credit retry evidence into same transaction design. Code offline conservation/failure cases and grouped remote logistics scenarios before testing; remote topology is NOT RUN until independent endpoints/identities exist.

### MP073: moved station carrier correlation and contents settlement

Unify discovery/final consumed-envelope correlation while retaining exact applied removal/install, owner, source, destination and saved paid-queue proofs. Implement original contents settlement recovery before attempting automatic replay; inventory/overflow partial grant must not redeliver complete parcel on retry. Code fixtures for legitimate stripped envelope versus changed station identity, ambiguous history, nonempty/empty extras, partial setter/drop failure and reload. Group gameplay with camper station removal/reinstallation; no additional load per scenario.

## Added confirmed-finding workpackages — MP074–085

Parent allocation supersedes provisional agent numbering. IMPLEMENTATION_WORKPACKAGES_AGENT_DRAFT.md stable keys provide detailed stages/dependencies; mapped here for final consolidation. Every scenario and fixture is coded before executing tests.

|Finding|Production correction|Acceptance batch|
|---|---|---|
|074|Gear original offer/current held transaction admission and independent terminal dedup|Offline A-terminal/B-original ordering; remote gear/reconnect with exact B custody|
|075|Rage temporary activation expiry/current creation and authoritative remote projection|Offline saved marker invalidation; host/remote reload/expiry/death|
|076|Black Magic read-only action projection and one completion per control generation|Host/remote valid/expired control, repeated conditioning, bind/release|
|077|Explosion actual accepted native block-work witness|Offline protected/zero/change sets; grouped combat blast once per accepted work|
|078|Dog auto-loot/detach explicit native hook installation|Installer failure/retry checks; host/remote native loot and ordinary-container controls|
|079|Cruise current native driver+vehicle/seat binding|Driver/passenger/seat switch/detach, first-person keyboard steering|
|080|NPC revisioned reset/tombstone publication|Offline delta ordering; cached UI, late join/reconnect|
|081|Semantic backup validation, atomic publication and cache convergence|Disposable recovery matrix, no real-save repair|
|082|Creator native key/controller completion owned session|Button/key/controller/name/cancel/save-failure and profile cleanup|
|083|Remote tether projection preserved|Host/remote tether/unlock, stale snapshot, wrong owner/reconnect|
|084|Manual host roster recall parity|All Stay/Guard, mixed roster, ownership; preserve automatic Follow-only|
|085|Heat-treatment full native stat correction|Exact original item/provenance/durability, siblings, retries and saved remote state|

Final plan must also incorporate FINAL_PLAN_COVERAGE_AGENT_MATRIX.md and PLAN_GAPS_WORKPACKAGES_AGENT_DRAFT.md dedicated027/030/043/044/045/046/047/060/065; no grouped row may drop their distinct assertions. TRANSACTION_PLAN_FINAL_AGENT_REVIEW.md supplies original-operation proof/recovery obligations, not a new global authority service.

### MP086–088 final additions

MP086 shared camper navigation definition must survive both native/Rebirth include composition. MP087 uses actual selected native-item data and established Rebirth inspection/action controls; controller/quality/drop/scrollbar/lock visual and input assertions remain distinct. MP088 scopes cached water provider to exact current loaded native chunk/tile incarnation, with unload/reload/catchup conservation. Code offline include/identity/failure assertions and grouped UI/farming scenarios before any execution. Genuine remote/dedicated cases remain separate NOT RUN until endpoints exist. These additions do not authorize geometry/asset changes owned by VehicleStudio.
