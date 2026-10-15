# Ordinary station admission, adoption and cancellation source trace

2026-10-15. Current cumulative REBIRTH3.0 workspace, audit-only. Operating directives/current coverage and NATIVE_TRANSPORT_TRACE/CRAFTING_REMAINDER_AGENT_TRACE/PERSONAL_CRAFTING_AGENT_TRACE consulted. Parent retains RefundPreparation and camper refund chain. No production edits, builds/tests, game control, Git or shared report/ledger edits. Only this trace created. Full source means actual content read, not search/hash coverage. Negative production caller searches used rg -uuu against all *.cs excluding documentation snapshots and Tools; Scripts subtree searches also used -uuu. External binary mods/runtime patch metadata remain uninspected.

## Full scope

24 full reads: PreparationService531, PreparationReservation153, GridAdmission444, GridQueue268, PlayerIngredients101; ObservationDispatcher255; eight Discovery helper files plus RecipeDiscoveryRecord (binding,authority scope,canonical recipe,frozen creator,integration,pair,scope,witness,record); PreparationPersistence; CancellationAttemptService/Refund/Request/Session; TerminalIntentService/TerminalSnapshot; CompletionCapture/Receipt. Current hash manifest below identifies actual read versions, including any shared changes. Native codec/live-access/input helpers and paid-completion transformer reuse earlier full evidence only at matching source; ordinary/camper caller boundaries inspected separately. RefundPreparation, refund archive internals, full repository, snapshot publication implementation and camper ownership not newly full-reviewed.

## Actual installed versus dormant path

ProgressionInstaller16 installs ObservationDispatcher; lines34-42 install native publication/reward holds and input/queue/snapshot/region hooks. Dispatcher Install registers GameUpdate; it records exact current authoritative thread/World in Tick and invalidates caches on world/authority/Survivor scope change. Existing early installed=true before event registration is same installer staging qualification as MP-013, not runtime evidence of a missing callback.

No production caller found for ordinary PreparationService.TryPrepare, TryPrepareDiscovery, TryPublishPrepared or TryConfirmPublicationAttempt. CancellationSession.TryBegin has no production caller; Request.TryRequest is invoked only inside that uncalled session. Helper paths are code-complete source, not installed ordinary UI transactions. Existing ordinary native crafting action/queue behavior is therefore not protected by claiming these helpers ran. Existing workspace/QueueEntry still delegate native actions/cancel; previous MP-042 native payment evidence applies. Mobile TryPrepareMobile has explicit camper action/network callers; those are separate live integration, not evidence ordinary publisher is installed.

Ordinary RebirthStationPaidCompletionCallsite is explicitly inactive: no Harmony attribute or installer, and -uuu production references show its Transform reused by camper transformer, which substitutes camper Insert/Complete. Do not infer generic original Insert/Complete execute for ordinary station because the transform exists. Prior NATIVE_TRANSPORT full-read classification is corroborated by current caller search. The ordinary full CompletionCapture/Receipt helpers below therefore do not establish a live ordinary output receipt producer.

Installed Dispatcher can nevertheless adopt existing saved, marked paid jobs with matching native publication evidence. It is an observer/adoption hold, not a caller that creates/prepares/debits/re-enqueues ordinary jobs. Native publication hold passes unmarked jobs; malformed/unverified marked jobs stop before HandleRecipeQueue. Native tool hold applies again for ordinary active nonscrap Survivor jobs. Definition resolution/payment/adoption are separate checks. This preserves accepted native owner action architecture; no unrequested general paid-job migration is recommended.

## Immutable payment and recipe binding

GridQueue marks detached paid ingredient fragments, never the catalogue item instances. Full original Recipe native serialization plus craftingToolType, ingredient-modifier flag, tags and forge/campfire material/category flags form SHA definition key. Marker captures version/definition/batches/tier/job. Bound one concrete generic batch contains aggregate output/XP/count and multiplier1; per-batch versions3/4 are mobile-compatible and retain original batch multiplier. Native serialization limits enforced before marking: <=9paid fragments, ushort fragment counts,32767output count,metadata byte capacity and tier0..6. Molten units are metadata counters while native stack count stays1. Unknown/ambiguous changed catalogue fails resolution rather than guessing.

TryPrepare creates detached queued recipe and debited remainder then ConservesPayment compares every changed slot against exact ordered original fragment; RefundSnapshot strips only reserved station namespace and restores material units. Input namespace collisions are refused before stamping. Definition restoration copies only missing current native runtime definition fields, retains serialized paid fragments/timing/totals. HasResolvedDefinition is weak-table state after actual resolution, not a payment certificate. Existing MP-029 byproduct definition drift remains relevant because native source serialization does not itself bind external additional-output tables; this tranche does not supersede it.

GridAdmission preserves immutable stored/effective XML images and parsed paid snapshots. Ordinary schema1preparation/2attempt, mobile/material/per-batch schemas and relocation/player-payment wrappers12-14 are structurally distinguished. Strict shape/id/count/finite-time/tier/fragment checks; TryReadStored retains custody even missing current recipe, while TryMaterialize requires current exact definition and conservation. A stored hash is not authenticated payment. Queued matching validates job/count/xp/time/area/paid metadata and quality/tier; initial observation additionally matches physical station input afterimage. Later legitimate additions to station input do not rewrite paid payload. PlayerIngredients is detached local mobile payment plan only: requires exact active mobile session/window/inputWindow discriminator, bag then owned belt, exact captured grids/locks, modified items excluded and malformed multi-count nonstackables refused. No ordinary station player payment is thereby enabled.

## Preparation state machine and concurrency

Ordinary TryPrepareCore resolves exact current authority player/station/creation through LiveAccess, requires unmarked canonical recipe reference, correct current unlock or discovery scope, reading policy, actual CraftingAreaRecipes and tools. It allocates physical station input using current modifiers/time/tier, re-resolves same station/record/input before staging. Discovery additionally freezes exact policy/owner/save witness pair. No native debit/queue effect occurs while merely preparing/saving.

PreparationPersistence validates owner creation, maximum64retained records, unique job, exclusive station; failed/uncertain save retains original intent. Reservation is World weak-table + station/address key, maximum1024; full immutable owner/job/image must match. Cross-owner current/durable exclusivity delegates HasExclusiveStationPreparation, not dictionary presence alone. Mobile player-payment preparations additionally share owner bag/belt custody through SharesStation. A timeout or UI closure cannot release claim.

TryBeginPublicationAttempt confirms original saved intent, physical beforeimage, unchanged queue snapshot/vacancy, reading/tools/discovery and final-file preparation/binding witnesses before staging attempted phase. Reservation updates both cached admission/claim then saves; only original successful save sets one-use NativeAttemptReady on same thread. Failed save retains attempted record without minting replay permission. TryConsumeNativeAttempt consumes before effect and never restores on exception. TryConfirmPublicationAttempt is save-only recovery, not new debit permission.

Ordinary TryPublishPrepared would merge only physical payment into native input preserving material tail, build a replacement queue from native vacancy/snapshot, consume original attempt permission, suppress intermediate modified checks, assign input and queue and register publication watch, then SetModified in finally. Exception/false leaves attempted obligation, does not blindly restore inputs or replay job. This whole ordinary mutation method is uncalled by current production source; do not report its source guards as active ordinary payment settlement. Mobile branch checks exact vehicle address/lease and owner saved payment phase separately, and is parent scope.

## Native publication adoption and recovery

Dispatcher Tick keeps per-job watch plus verified station/object, original actor id, cloned admission and current cached owner record. Authority/thread/world guards apply. TryWatchQueued requires saved/exclusive original preparation and exact live initial queue observation; captures chunk, station block and exact owner. Observe requires one job occurrence, StartingEntityId owner.entityId, current creation/record, physical-slot length/input afterimage and canonical paid queue match. It also refuses conflicting online owner preparations; durable all-owner exclusivity is separately queried.

SnapshotEvidence.Watch observes actual native input+queue serialization and persisted region publication via already installed hooks (earlier NATIVE_TRANSPORT evidence), not caller bool alone. PersistPublication creates publication record only from supplied published proof, marks owner dirty, flushes and requires HasSavedStationPublication final-file witness before adding verifiedQueuedJobs. Pending watch60s expiry disposes observer only; original persisted admission remains. Tick can rebuild a watch later; no debit/re-enqueue. Failed publication save retains exact pending phase for retry.

After reload, TryRevalidatePublishedPreparation requires current live queue/owner and exclusive saved preparation plus final-file publication witness and publication.Revalidate(saveRoot). Tick re-adopts only then. The authority owner must be present for scanning; exact actor-id match can fail after entity reassignment. Native actor identity across reconnect and loaded station incarnation/path changes must be tested before claiming offline/reconnect recovery succeeds. Cached verified binding checks station object, actor,admission quality,owner record and no terminal intent each queue pass; duplicate marked job refuses queue. It does not prove output delivery or completed award.

Native exact installed Assembly-CSharp identity SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. Earlier native full queue/TileEntityWorkstation methods establish HandleRecipeQueue executes active tail, mutates output then records native completion; current Hold prefixes and installed snapshot hook names were inspected. No new decompile/build or runtime observation. Serialized witness implementation and repository final-readback are reused dependencies, not newly broad certified native durability.

## Cancellation and terminal release (ordinary helper remains dormant)

TerminalIntentService persists one completion/cancellation choice for attempted admission. Cancellation first captures native queue/output/complete-list and refuses existing matching/malformed reserved receipt. It stages retained refund obligation and intent, saves and rechecks exact owner/station/queue/terminal/witness. It never credits items. Failed/uncertain stage stays dirty; no optimistic rollback. TerminalSnapshot uses bounded native wire output+CraftCompleteData bytes/digest; snapshot is not itself persisted proof.

CancellationRefund derives exact paid payload from retained admission, strips queue metadata and validates against original conserved payment. Per-batch remaining quantity comes from integer per-type full/original*batches and deterministic original-fragment prefix; not a current recipe reconstruction. Ordinary aggregate job requiresremaining1; mobile material counter returns separate from physical item refund. Read/write require matching original intent/admission, <=64records and exact serialized payload. No destination or settlement authority supplied by detached obligation.

CancellationAttemptService saves cancellation intent/refund then captures before/after queue and unchanged terminal witness, final-file checkpoint and owner/station/claim. Original successful BeginRemoval mints session/thread/actor/world-bound one-use RemovalPermit. TryConsume burns permit even on failure and checks exact unchanged retained data, no native job receipt, native queue/terminal digests and cancellation replacement. TryApply assigns only replacement queue under modified-check suppression and registers cancellation watch; any ambiguous partial effect retains attempted record and cannot be replayed. Recovery TryConfirmCheckpoint/TryConfirmPublication save retained evidence only, never remints permit.

CancellationRequest accepts primary local player; remote cancellation explicitly requires separate owner inventory protocol and is not admitted by this helper. Session.Advance is explicit, no automatic dispatcher/UI hook: waits exact cancelled publication, then invokes parent-owned RefundPreparation ApplicationPermit once, verifies saved owner image and calls archive. Dispose drops transient watch/permit but leaves durable pending journal. Those refund application internals are outside this tranche and must not be inferred safe merely from calls.

Reservation.TryReleaseSettled checks same authoritative World/owner/creation/original immutable claim; requires preparation no longer retained and successful terminal final-file callback, then removes claim. Missing claim alone is insufficient: fallback requires matching archive original admission and final saved witness. Targeted RefundArchive tail45-66 shows it saves/readbacks exact archive before this release callback. Parent owns full archive/refund review. Completion archive has separate camper caller. No source timeout/missing queue/UI closure release was found in the full reservation class.

## Completion receipt helper semantics

Receipt reserves rebirth.station.receipt namespace. Native reward use refuses any reserved marker, malformed included, to avoid generic entity-id award path. Identity requires job/creation/definition/tier/count/xp,exact CraftCompleteData recipe/count/no scrap and optional additional-output receipt. TryCreate clones output, stamps markers and additional-output receipt; it neither inserts output nor awards. MatchesAdmission couples original paid queue and exact output quality/type. Full current source hashes account for shared extra-output changes.

Capture obtains opaque original authority scope, exact active native queue object/recipe and output-before clone; later Match requires actual physical output delta. One-use thread observation is consumed before appending replacement completion list. SetModified failure keeps uncertain native row effect, not replay. SuccessfulOutput mint requires exact original row still present and frozen physical before/after delta; further claim/reentrant guard prevents repeated event issue. Parent native settlement/original-scope helpers remain dependencies. Ordinary callsite is inactive, so this is qualified helper behavior, not proof ordinary output currently uses it.

## Findings and remaining qualification

No new independent live gameplay issue proposed from dormant ordinary helpers. Important reachability correction: ordinary preparation/publication/cancellation source is not an installed UI transaction; Dispatcher/holds are active. Existing MP-013 installer stage and MP-029definition/MP-042native-owner findings unchanged. Full-read methods retain uncertainty conservatively; generic absence of live effect is not permission to refund/re-enqueue/release.

Outstanding outside24file source scope: ordinary integration decision, full persisted publication/cancellation observation hooks and schema implementation, exact final-file repository and durable cross-owner claims, parent refund application/archive, camper, native binary patch metadata, all XML/recipe/authored input/tool bindings and runtime qualification. Test afterward: save failure before/after phase,changed input/queue/tool/owner/current policy,duplicate jobs,cross-owner station claims,publication timeout/unload,reconnect entity reassignment,definition/byproduct changes,cancel vs completion concurrent observation,partial native setters/SetModified exception,original permit consumed once,failed refund save/archive and held claim. NONE RUN.

Scope matrix: full24source reading PASS; actual caller/install/hold/release static tracing PASS with explicit dormant integration; architecture preserved PASS; docs PASS; implementation N/A; native/persistence targeted dependency review only; XML/localization/assets N/A to this source tranche; compilation/in-game NOT PERFORMED per gate; packaging N/A. COMPLETION VERDICT: COMPLETE for this recorded source tranche, NOT COMPLETE for whole ordinary-station feature/fixes or runtime acceptance.

## Full new source manifest

Each file actually read in full, with current line count/SHA256. Shared sources preserved. Hash alone does not replace semantic reading.
Scripts/Crafting/UI/RebirthStationPreparationService.cs | 531 | 661E2D1F63FB0FF1B5F80CF85A953FA75EAAF4F75FA302C9929E8B084D93C0C5
Scripts/Crafting/UI/RebirthStationPreparationReservation.cs | 153 | F62E6D06AB965CC80681160BF3E71F045D1117B14EAC73EE5AA44E0087D2B6D0
Scripts/Crafting/UI/RebirthStationGridAdmission.cs | 444 | B3FDD5D3D883733855CFDD837B63DFC01DE65C0D9AC1C08297014E85E20A68C9
Scripts/Crafting/UI/RebirthStationGridQueue.cs | 268 | AFB195120763C80C65F2CBC127023C9E2E4EF7C475E8E77DEAB10119E815C6F9
Scripts/Crafting/UI/RebirthStationPlayerIngredients.cs | 101 | 5F1D9FE114A939E3D6B16AB63210E14EF9B0681DF127EB10F4A27E60C2B962AD
Scripts/Crafting/UI/RebirthStationDiscoveryAdmissionBinding.cs | 49 | 6D22D2D0F97E6828516A5122E6DE3430C61B901FA29A74A368B1691DD5767CDE
Scripts/Crafting/UI/RebirthStationDiscoveryAuthorityScope.cs | 83 | A50A866B792841A1AB6E99E1EF9E324DF6751CA03C72CCCEB9FC66692AD8EDAE
Scripts/Crafting/UI/RebirthStationDiscoveryCanonicalRecipe.cs | 49 | CA9AFC7D1368E8E26EF4C9B6B61CD5CABD877CF6B78C5C72DB9523B3D5B3A549
Scripts/Crafting/UI/RebirthStationDiscoveryFrozenBindingCreator.cs | 35 | 287035ED454E91D22318453923D80E7D5D7DA559ED243145B2EC8FFE2CC1B936
Scripts/Crafting/UI/RebirthStationDiscoveryPreparationIntegration.cs | 53 | BA5C1FC6A091228D2A4ED9F39738D952F3DB73564727BE7548A61EAA440F1A50
Scripts/Crafting/UI/RebirthStationDiscoveryPreparationPair.cs | 44 | BFD1A302880CC8E6F532CCD5EF088B2D99F6006412EA9F9CA2BA572CB9E6EF2B
Scripts/Crafting/UI/RebirthStationDiscoveryScope.cs | 36 | B57D22FA611F579397C803FB42AD5D0C09D96D6A252437F404176FE5C0E33AD8
Scripts/Crafting/UI/RebirthStationDiscoveryWitness.cs | 90 | 36E853CD2959B3CA90AEBFA201B7E36CE42E9FDEDCD937056402EA84FBD11282
Scripts/Crafting/UI/RebirthStationRecipeDiscoveryRecord.cs | 86 | 4794E337B9899D357E8C33F169F8250BD08796BDDE49735C8DFCA11B4D1BEBEA
Scripts/Crafting/UI/RebirthStationCancellationAttemptService.cs | 218 | BA644862A7D8E33FD3C326C6E7C34AADF50557555BBE7C6251D180E775D55F29
Scripts/Crafting/UI/RebirthStationCancellationRefund.cs | 161 | B2B61CA7F1BDDC184DD52BC6108464DE80E68306A8AFFC552FE5969F42CA1D03
Scripts/Crafting/UI/RebirthStationCancellationRequest.cs | 23 | 5DDF3AC61BE02132A9B21EBC51E151CCBFD039DCDC3D8E476BB91420EA1B4A7D
Scripts/Crafting/UI/RebirthStationCancellationSession.cs | 57 | 71E6B50D93126EA8403C26AD5FC403C594B1AD6EB1F296F9874C228697B8BB77
Scripts/Crafting/UI/RebirthStationTerminalIntentService.cs | 76 | 0A818DC6B8DC8876E90208008FFCC121785C7152E2AA3AA3867A2D8D416F4322
Scripts/Crafting/UI/RebirthStationTerminalSnapshot.cs | 61 | 21A94400686C1522CEFC173A774C7DB7FD90AF3E96A47518B90C24D7A9F035CD
Scripts/Crafting/UI/RebirthStationCompletionCapture.cs | 213 | 0C306DFCC0534A20F86AB0866A8C22A9FBDA968C4FDF55F025F284AEC1633001
Scripts/Crafting/UI/RebirthStationCompletionReceipt.cs | 117 | B0065EAA401E42A37AD416916A2A44A57192D3426C6799A4270E5A56B41B8A81
Scripts/Survivor/Progression/RebirthStationObservationDispatcher.cs | 255 | 7C9BEB63B6E53893A2109055B2D7664C350B3EAAA4BFD101C2725E356FFD2C0A
Scripts/Survivor/Persistence/RebirthStationPreparationPersistence.cs | 68 | E46321ADE1389F06110CF4883249A882948DCE2B6AB69D81FF4BFE74F4A8DF2A

Prior full/helper or targeted dependency current identity (not new full coverage here except tiny Hold prefix reread):
Scripts/Crafting/UI/RebirthStationPaidCompletionCallsite.cs | 6391A94C1969D5C68B22A09AA545794D16F8A1738F55354CD755F1450DA86F5C
Scripts/Crafting/UI/RebirthStationNativePublicationHoldPatch.cs | B94D1E3D012F8BBCB0E33DB2A74C3EC15F429CB1C7A01688B499DFEEEE9327DA
Scripts/Crafting/UI/RebirthStationRefundPreparationService.cs | 19BA4950F3EA9AF583F3A014FD9BF03759BB151FB4347213EEA6C5CE2521E6C5
Scripts/Crafting/UI/RebirthStationRefundArchiveService.cs | A84E98A72482A55B675F129ED877E6334684267C307F646A473E406087C91516
Scripts/Crafting/UI/RebirthStationLiveAccess.cs | B046CE61A46B680A0AF68C2047728072EDCF113C77952C9AE73BFAFB709E56AC
Scripts/Crafting/UI/RebirthStationNativeQueueEntry.cs | 35022F54D36666C5600E854F12967B013025D4140696CAE1AC154BF140A2CD32
Scripts/Crafting/UI/RebirthStationGridIngredients.cs | 2BFF7B9AD3341F56FBEF7E0E01EC92215E57FD8E25C24C5668E522F21419E0D7
Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs | E7CA3AD69BBAC7E6EB946E6384AE6C8C55CD3BE2AAC03B475EAA7BB97462E745
