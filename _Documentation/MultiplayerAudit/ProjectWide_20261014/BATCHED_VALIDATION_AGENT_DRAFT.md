# Batched validation draft — MP-001–088

Planned only, NOT RUN. No scenario/fixture/production code created, build, test, game launch or endpoint action. Supplements EXECUTION_PLAN_DRAFT and finding-linked traces; does not authorize gameplay. Finish all approved production and scenario coding first, coordinate current shared source/asset checkpoint, then execute offline checks, then one consolidated gameplay qualification batch when authorized. A failure returns to one coding batch; do not repeatedly restart per finding. Prior historical fixture PASS does not count for the new checkpoint.

## Concrete execution order

1. Freeze source/project/config/assets and record hashes/HEAD/branch, approved checkpoint, native Assembly-CSharp hash and toolchain. Resolve pending external SeatPose freshness or explicitly exclude Debug authoring from qualified scope. Do not build while game is running; do not take over a user's existing game. Read HAV3N coordination before compiler/CPU-heavy fixtures and honor QUIET. Offline fixture compilation is execution and is deferred until coding complete.
2. Run one offline batch of applicable production-backed fixtures below, plus newly coded finding cases. Record fixture source hash, actual extracted production symbols, doubles/unsupported native callbacks, stdout/exitcode and per-finding assertions. Avoid static grep/literal/hardcoded self-checks as behavior PASS. Assert Debug/Release inclusion and required installers separately; don't enable release-disabled Work/other-player medicine merely to test them. Offline failures stop gameplay admission.
3. With later authorization, launch ONE disposable host CodexTest session, verify save identity/process-start/PID/token/native world and owner, guard enabled, then cleararea radius40 + restore as setup and read surroundings after every arrival. Never disable guard. Capture log baseline. Use real walk/look/activate/select/press/click/drag for feature actions; console/give/spawn/damage/skillsetup/stationtoolsetup only named fixture preparation, not proof of natural earning/payment/wear.
4. Run batches A→H below in that host session; stage durable before-state witnesses for all persistence cases before the single shared reload. Reuse current world/stations/players where cases don't interfere. Guard may intervene: waitForGuardIdle, reassert current window/scope; intervention is logged, not hidden or disabled. Restore each case's setup in finally on the same original world; replacement-world cleanup cannot restore stale policy.
5. Independent remote is a separate authenticated endpoint/process and owner, not simulated flags/HOST-SERVER labels. Coordinate already connected remote and host without launching one process per test. Extend runner session selection first; current Runner fixed session.json is insufficient orchestrator. Server/listen owner and remote owner observations both required. Dedicated topology, if unavailable, remains NOT RUN. No endpoint available means missing prerequisite/NOT RUN, not PASS or automatic launch.
6. Delayed/reordered/duplicate/cancellation/competing owners, disconnect/reconnect and latejoin run as grouped boundary cases in their appropriate batches. Retain original operation/creation/save-root/entity/source item snapshots before disruption. Clean disconnect is not a crash test. Native receipt/save failure injection must be a disposable production-backed fixture/explicit test seam coded first; don't corrupt user files or invoke admin reset as equivalent.
7. Close owned windows through input, wait for original durable saves/retirements or explicitly record retained uncertain custody. Capture host and owner exact witnesses. Perform **one deliberate shared save/quit/relaunch/reload** for all prepared persistence groups; verify original operation outcomes plus ability to perform a new ordinary transaction. This is the one justified persistence restart; separately necessary process-crash cases must be explicitly listed and cannot silently reuse a clean-reload result. Reconnect peer transport and latejoin observers against the same continuing host where possible.
8. Produce one machine-readable aggregate report keyed finding/case/topology/phase with PASS/FAIL/NOT_RUN/BLOCKED_PREREQUISITE, source/native hashes, endpoint PID/start identity without tokens, original op/owner/world identifiers, before/after native snapshots, saved witness, trace/log/screenshot paths and timing. Redact credentials/session tokens. PASS requires all mandatory assertions for that topology; a screenshot/HTTP200/ok:true alone is not transaction success. Inspect visual screenshots. Repeat only changed/failed cases and justified shared regressions after final coding batch.

## Existing interfaces to reuse, with limits

JSON runner documented Tools/GameBridge/tests/README.md: action command/args; markItem/expectItemDelta/expectItemStacks; markSkill/expectSkillGain; expectChallenge/Buff/Cvar/Stat; expectStation with exact coordinates, station and field; waitForGuardIdle; waitForWindowOpen; screenshot; expectNoErrors. ExactText/id/window and exact recipe matching prevent substring false success. Require actual stable observations rather than long fixed waits. GET station snapshot defaults closed; open tile can lag UI. Current JSON runner lacks generalized cross-peer saved-image/receipt equality and fault scheduler assertions: code these in orchestrator/helper, don't fake them using item counts.

Existing standalone Python: camper_consolidated_regression_live.py prepare PLAN REPORT / verify-reload REPORT requires current selectors and owns no launch; gear_remote_batch_live.py PLAN REPORT expects already connected remote and imports fixed-session Runner; qualify_consumption_live.py / qualify_consumption_batch.py require original remote source/nutrition proof. Rework explicit session-file/endpoint selection as needed without printing tokens. Camper template has TODO recipe/path/entity and empty action arrays; fail prerequisites until populated from current real UI, never inject queues. Existing recycler_camp_migration_deferred.json is diagnostic log+screenshot only, not functional PASS.

Concrete offline starting points (all existence checked; relevance/body compatibility must be checked before modifying/running): test_assertion_guards.ps1, test_atomic_xml_file.mjs, test_metabolism_packet.mjs, test_resource_item_fingerprint.mjs, test_station_grid_ingredients.mjs, test_station_grid_queue.mjs, test_station_publication_hold.ps1, test_station_publication_receipt_retry.ps1, test_station_refund_preparation.ps1, test_station_refund_delivery.ps1, test_station_refund_archive_service.ps1, test_vehicle_journal_load.mjs, test_vehicle_journal_commit.mjs, test_vehicle_journal_publication.mjs, test_companion_snapshot.mjs, test_companion_roster_failure.mjs. Fixtures using codec/world/save/network doubles establish only those boundaries. No blanket all-Tools tests/build needed; group exact relevant cases.

## Required coding packages and golden contracts

A — Harness/diagnostic/install lifecycle. Extend existing assertion guards with balanced ping/open/request accounting, malformed input, timeout/cancel/world-replacement queue cases; patch-class partial failure→rollback→retry must inspect real patch bookkeeping with correct doubles/native status boundary. Add proposed audit_harness_lifecycle.json and production-backed lifecycle fixture; rbsys reports are declarations, never execution. Sampling reset/generation/thread affinity/override restoration and actual persistent admin mutation require separate disposable tests. Reuse smoke, guard_idle, fire_visual_idle_capture, npc_social_update_idle_smoke only as smoke, not full proof.

B — Owner custody/storage/resource/consumption. Add proposed audit_owner_custody batch plan + offline phase matrix: exact native ItemValue/metadata/quality/count, original slot, refused setter, full inventories, partial insertion, duplicate/reordered outcome, same/different owner/world/creation, delivery deadline and reconnect entity replacement. Retain uncertain intent and exact remainder; no blanket inventory repair. Reuse food_consumption_prepared_remote/honey, p2p_gear_custody, container_transfer_observation, container_pack_up_observation and remote gear runner with stronger assertions. Count-only UI is insufficient for metadata/bound receipt.

C — Stations/cooking/recycling/repair. Add proposed audit_station_transactions.json plus Python saved-state comparer: all four named placed stations + Camper and native/Rebirth progression branches; paid batch, cancel partial/full, native extra-output capacity split across bag/belt, output-full retry, changed byproduct definition, mixed ordinary→scrap and ordinary→ordinary native catchup, active tail fuel, required durable tool healthy→break/exhaustion and nondurable controls, modifier/native quality, exact native productive seconds. Reuse crafting_audit_queue16/outfit_tools/saw/gas_alias/mortar_closed_batch, workstation_queue_hold/open_queue_hold, cooking_save_failure_admission and strict runner. Admission-only checks don't prove per-row wear or output custody. Add actual player UI actions and snapshots to current recycler observation scenario.

D — Camper transfers/energy/physical/presentation. Extend concrete Camper runner plan using observed selectors, all station input/tools/fuel/output, module install/remove/reinstall contents envelope, original owner energy prepare/apply/retire, network bound8MiB, failed send→close→reopen, sink jar/bucket exact conversion, owner physical mode/door/sink input. Reuse camper_management_dashboard_ui, camper_station_dashboard_ui, camper_energy_bounds_ui, camper_entry_seat_target_deferred, camper_cooking_take/cancel/publication, preserve gesture cases. Add proposed audit_camper_recovery plan with failure-stage witnesses/rejoin/new transaction and dual progression navigation/indicator bindings. Asset fade/pose screenshots separate from transaction truth.

E — Farming/fire/world. Add proposed audit_farming_fire_lifecycle plan + offline production geometry/provider/save cases: same-revision ordered transitions/snapshot abort, original ignite/extinguish authority once, failed-load quarantine, water and harvest competing owners, thermal saturation/incomplete/crop elapsed baseline, missingchunk light classification, native unload cached provider no remaining authority, current chunk reload/latejoin. Reuse farming_door_visual_retry/fire_visual_idle_capture as visual smoke only. Craft/config fixture cannot reproduce native unload callback ordering; native targeted evidence + runtime unloaded-border case required.

F — NPC/companions/traders/party. Add proposed audit_npc_owner_lifecycle plan: full sender/owner epochs, trade quote→payment/output source binding, follower/dog/drone inventory commands, hire save/compensation, remote consume original source, death/dismiss/recall/Stay/Guard, owner swap and reconnect, reset revision and invalid parseable backup quarantine, melee actual hits/native target/stamina, dormant hooks installed only after approved fix. Reuse npc_eai_combat/target_task, dog_reward_*NOTRUN, companion captures/trader_job_refresh/quest_turnin_owned_actions_deferred with exact action/output receipts. Work MP047 is held: offline lease/capability assertion only, active gameplay marked NOT_RUN_RELEASE_HELD, not enabled.

G — Survivor/progression/health/skills. Add proposed audit_progression_native_events plan: verify actual physical successful action/output before award, event and settlement identities, XP/multiplier/native modifiers, replay once, durable sideeffects after failed skill save, owner native Health/Capacity/Stamina, original injury acquisition, repair deferred output and signature, electrical mixed-source payment/config destruction, theory pending clean record/death killer, Rage saved markers/BlackMagic remote/control-window, explosion successful changed blocks (not attempts), creator actual Apply hotkey. Reuse combat_melee AND combat_ranged when combat touched; horde_* attribution, electrical_service_repeat, self_splint_quality_deferred, other_player_injury_treatment (release held, do not enable), library_study_activity/teaching_theory_insight/character_launcher_presentation only applicable scopes. Fixture setup projected XP/health is never earned result.

H — Shared persistence/native topology. One pre-reload manifest spans applicable B–G: stores primary/backup/version/quarantine, original native player/item/station/vehicle sidecars, epoch/cache replacement, exact boss ownership restrictions and pending job/reward state. Offline fail every publication stage without deleting original candidates; timeline rollback must never delete uncaptured sidecars. Save/reload assertions compare native authority and owner projections, not only logs. Latejoin verifies fresh role and original public state; no arbitrary lower revision acceptance. Proposed audit_shared_reload_manifest.json is a planned report/fixture schema, not an existing runnable scenario.

All proposed audit_* filenames are **missing code to implement**, not created or runnable by this draft. Every finding below must have at least one production-backed offline assertion and its applicable real topology case, or explicit held/capability NOT_RUN. Per-finding exact injection/action details remain in its source trace and implementation workpackage.

## Complete MP-001–088 assignment

Table assigns every current report finding exactly once to primary batch; cross-batch H reload applies to durable rows. Existing report title supplies exact regression trigger, and batch contracts above supply evidence/assertion requirements. Parent workpackages retain specific edge cases beyond primary assignment.

| Finding | Primary batch | Exact report regression |
|---|---|---|
| MP-001 | B | Quick Stack category response can reopen or retarget stale UI |
| MP-002 | E | Remote fire mutation requests explicitly rejected |
| MP-003 | A | Real multi-peer test orchestration gap |
| MP-004 | A | Bootstrap records failed fire integration as completed |
| MP-005 | B | Legacy Quick Stack result throws with diagnostics disabled |
| MP-006 | B | Legacy Quick Stack/restock commands bypass modern bag exchange |
| MP-007 | B | Consumption can leave an unreconciled owner debit |
| MP-008 | B | Remote-resource refunds can discard unplaced items |
| MP-009 | B | Remote-resource outcome journal can forget unresolved removals |
| MP-010 | B | Cooking pull has no delivery retry or scoped callback retirement |
| MP-011 | H | Container-name load failure permits replacement of unreadable saved state |
| MP-012 | H | Offline quest-grace save deletes the primary before publishing the replacement |
| MP-013 | A | Survivor persistence installation becomes non-retryable before its hooks exist |
| MP-014 | A | Bridge health polling permanently increments outstanding-request accounting |
| MP-015 | C | Native workstation completion loses deferred awards and post-award effects |
| MP-016 | D | Camper update rereads and hashes the entire energy transfer history |
| MP-017 | A | Installer rollback leaves the shared patch cache claiming removed hooks exist |
| MP-018 | B | Hydration equipment moves bypass the original-owner settlement path |
| MP-019 | H | Metabolism recovery ignores backups and accepts structurally wrong stores |
| MP-020 | E | Fire snapshot replay does not preserve ordering within a delta batch |
| MP-021 | E | Failed fire-state recovery remains writable |
| MP-022 | B | Packed-container restore consumes receipt before recoverable publication |
| MP-023 | H | Workstation security save retries can overwrite the last usable backup |
| MP-024 | E | Farming water exchanges do not verify item publication before settlement |
| MP-025 | A | Farming diagnostic sessions are not isolated by requester |
| MP-026 | A | Farming temperature diagnostic crosses thread and world lifetimes |
| MP-027 | C | Placed recycler return/settlement exception gap |
| MP-028 | C | Additional-output native owner prediction and partial-write recovery |
| MP-029 | C | Regular byproduct definition omitted from paid-job binding |
| MP-030 | C | Restoration publishes after unverified native debit |
| MP-031 | D | Sink preparation can remain permanently pending before owner receipt |
| MP-032 | E | Thermal queue saturation misreported as completed enclosure |
| MP-033 | E | Active crop processing ignores incomplete thermal geometry |
| MP-034 | E | Missing light-search column classified as open sky |
| MP-035 | E | Competing owner-local crop harvests lack exclusive settlement |
| MP-036 | H | Placed workmanship recovery remains writable after failed load |
| MP-037 | B | Non-liquid provenance bypassed by partial inventory merges |
| MP-038 | G | Electrical record admission exceeds its durable capacity |
| MP-039 | G | Electrical configuration can outlive destroyed source blocks |
| MP-040 | G | Mixed-source electrical service can retain partial payment on failure |
| MP-041 | B | Cooking returns acknowledge stale companion snapshots without restoring items |
| MP-042 | B | Personal craft queue survives failed remote-resource payment |
| MP-043 | F | NPC companion metabolism Use debits the wrong custody |
| MP-044 | F | Dog hire compensates behind an already committed ownership save |
| MP-045 | H | Timeline restore rollback can delete uncaptured sidecars (high) |
| MP-046 | H | Independent shutdown handlers bypass coordinated save retention (high) |
| MP-047 | F | External NPC transfers reject their Transfer-only authority lease (high functional) |
| MP-048 | F | Remote NPC trade mutates an unbound server inventory replica (high) |
| MP-049 | F | Exceptional world transitions retain NPC backing caches across saves (high) |
| MP-050 | F | NPC wildcard trade catalogue cannot quote its displayed defaults (medium functional) |
| MP-051 | G | Remote owner stamina ignores authoritative Energy recovery penalty (medium functional) |
| MP-052 | F | Remote random trader refresh loses required correlation on native wire (high functional) |
| MP-053 | F | Shared-quest member notifications check the wrong authenticated actor (high functional) |
| MP-054 | C | Mixed ordinary/recycling queue bypasses recycler processing on native catch-up (high functional) |
| MP-055 | C | Managed cooking omits configured additional outputs (medium functional) |
| MP-056 | C | Workstation fuel classifier examines a future row instead of active native row (medium functional/performance) |
| MP-057 | F | Boss reward ownership restriction disappears after native disk reload (high) |
| MP-058 | F | Authored human NPC melee task stops on optional native stamina (high functional) |
| MP-059 | B | Creative toolbelt trash clears native bar width rather than displayed REBIRTH capacity (medium functional) |
| MP-060 | F | Party broadcasts can reuse pre-leave identity snapshots (medium functional) |
| MP-061 | A | Inspect-class qualification commands mutate persistent progression (medium diagnostic integrity) |
| MP-062 | G | Pending theory study cannot resume from an already-saved clean record (high functional recovery) |
| MP-063 | G | Theory combat evidence checks killer after native death clears it (high functional) |
| MP-064 | G | Remote owner native Health Capacity ignores authoritative Survivor condition (high functional) |
| MP-065 | G | Combat progression frame-settlement hook is not installed (high functional) |
| MP-066 | C | Remote repair signature observer reads nonexistent inventory fields (high functional) |
| MP-067 | G | Salvage bonus requests can grant output without an actual harvest (high authority) |
| MP-068 | B | Authored weatherproofing gear slot is rejected after remote custody starts (high functional/liveness) |
| MP-069 | D | Remote Rebirth camper physical actions resolve authority-only character identity (high functional) |
| MP-070 | D | Sink gauge XML is removed by later toolbelt replacement (medium functional) |
| MP-071 | D | Remote station send failure retains closing session and prevents normal reopening (medium availability) |
| MP-072 | B | Modern remote logistics mismatch can overwrite native owner inventory with stale server contents (high conservation) |
| MP-073 | D | Relocation recovery rejects a legitimately stripped station contents envelope (high functional) |
| MP-074 | B | Completed gear offer can block a later held transaction (high availability) |
| MP-075 | G | Saved Rage markers outlive transient authoritative activation (high functional) |
| MP-076 | G | Black Magic remote actions and one-window training diverge (high functional) |
| MP-077 | G | Explosion skill awards count attempted block candidates (medium progression integrity) |
| MP-078 | F | Dog auto-loot hooks are declared but not installed (medium functional) |
| MP-079 | D | Passenger input can apply driver cruise movement (high local ownership) |
| MP-080 | F | NPC progression reset publishes an unchanged revision (medium synchronization) |
| MP-081 | F | NPC backup repair accepts parseable but semantically invalid state (high recovery) |
| MP-082 | G | Creator Apply hotkey bypasses owned completion flow (medium lifecycle) |
| MP-083 | F | Remote drone tether projection is overwritten locally (medium functional) |
| MP-084 | F | Host manual companion recall excludes all-Stay/Guard roster (medium topology parity) |
| MP-085 | C | Remote heat-treatment correction omits native durability modifiers (medium functional) |
| MP-086 | D | Camper station navigation template is absent in native progression mode (medium functional) |
| MP-087 | D | Management inspector authors indicators without binding their values (medium UI functional) |
| MP-088 | E | Farming cached water provider can remain usable after chunk unload (medium lifecycle/conservation boundary) |

Assignment coverage: 88finding headings/88unique IDs/88primary rows. All planned executions NOT RUN. Independent remote/dedicated capability remains NOT RUN until separate genuine endpoint identity, disposable-save authorization and paired observations exist. Guard remains ON throughout.
