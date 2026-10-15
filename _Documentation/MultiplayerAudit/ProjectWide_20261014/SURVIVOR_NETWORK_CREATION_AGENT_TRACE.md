# Survivor network, creation UI and owner projection audit

Audit-only, 2026-10-15. No production changes, build, tests, command execution, game or git operations. Complements SURVIVOR_CREATION_AGENT_TRACE.md and SURVIVOR_REPOSITORY_AGENT_TRACE.md; no repeated transaction/repository credit. Project directives were read. Source conclusions below are not runtime observations.

## Full-read manifest

All lines of the following current files were read. FirstEntry was read in bounded chunks; the 515–575 boundary was explicitly reread to close an output gap.

| Source | Lines | SHA256 |
|---|---:|---|
| Scripts/Survivor/Network/RebirthSurvivorNetworkService.cs |639|4E6E9EBE3E745F50D26C86E9FD04A3F76F3445414FF06C39E0CB3900F56F1D13|
| Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs |1406|2D44DB03416D0DB2521E4DB6A275786EFB81A15E3DCB459489AC13A68D12D63C|
| Scripts/Survivor/Network/RebirthSurvivorOwnerSendHeader.cs |108|124B481A15ED833174C69DBFD291D92AE95522E3AB0D2EDFF8AB2742361DCD62|
| Scripts/Survivor/Network/RebirthSurvivorClientState.cs |358|B2A156B1069349315E69235CE0D60C67F67770C5B05D2159D06092827E3F8CC4|
| Scripts/Survivor/Network/RebirthSurvivorOwnerProjection.cs |76|C1DD1B3BF7AFFFC11765C0857BC27B5EE9010C147C74CB40BA93ADF85F61DC41|
| Scripts/Survivor/Network/RebirthSurvivorNetPackages.cs |237|46EA51CA5DA467C8D91961C9A7434636C2AD515CC168253573912F6F46B16BE4|

6 files, 2824 lines. Installer/csproj/caller searches were selective evidence, not new full-read credit. Codec/models and native queue/dispatcher were not newly full-read in this tranche.

## Reachability and authority

SurvivorInstaller lines110–111 calls NetworkService.Install and FirstEntryUiService.Install. The non-Debug sources contain real ModEvents registration, packet types and UI entry points; this is an active production integration, unlike held NPC work. Network Install tracks each successful stage independently, making failed registration retryable. FirstEntry installation has a single installed flag and subscriptions to owner/result events and game lifecycle.

`rg -uuu` finds creator/chooser/profile-manager calls into StageNewSurvivor, StageExistingProfile, ReturnToSelector and LeaveWorld, plus native spawned bridge in RebirthSurvivorPreSpawnPatches. These UI consumers were caller evidence only; their entire bodies were not reread. Harmony attributes at FirstEntry's end are handoff diagnostics, not proof of installation on their own.

CreationRequest.ProcessPackage requires nonnull authoritative world, ValidEntityIdForSender and an existing EntityPlayer. Server NetworkService then derives stable identity from that player and invokes the previously audited transaction service. StateRequest uses the same sender/entity gate and protocol check. No client owner ID selects a repository record. ToClient result/state packages call ClientState directly; their declared direction is not a newly verified native dispatcher guarantee. Native sender-validation semantics remain the existing parent native baseline, not independently re-proven here.

Listen server uses a main-thread deferred queue so stagedRequestId is set before result delivery. Queue entries hold entity ID and cloned request; Pump resolves the current world entity. Remote uses SendToServer; dedicated executes the server path without local UI. PlayerJoined sends an early ClientInfo owner projection before spawn. PlayerSpawned remembers identity, offers starter items and force-publishes owner state. These are owner-targeted sends, not broadcasts. The ClientInfo and entity send paths remember suppression headers only after a successful send; caught failures return false. Creation response send itself is not enclosed with the subsequent force-owner publication, so transport exceptions can prevent that immediate publication; later explicit state requests are the recovery mechanism.

## Projection/cache and persistence boundary

CaptureOwnerState exposes a bounded owner projection, definition IDs/hash/version, creation/revision state, skills/theory/attributes, selected knowledge/discipline/trial fields, condition/support/gear and physical-bag desired state. Repository read failure with unresolved issues produces RecoveryRequired, not a new creation invitation. It does not persist a client snapshot back into authority. Prior repository/transaction trace remains the durable custody evidence.

LastSentHeaders includes record/origin/world/definition references, revisions, condition token and copied condition/support/gear fields. Oversized meal/support/gear counts mark a header incomparable, disabling suppression. Progression changes depend on authoritative revision changes; this tranche does not prove every writer touches revision. Force/first send/unknown revision bypass suppression. The bounded dictionary clears wholesale at 1024 rather than growing indefinitely.

OwnerScalars copies private maps and a read-only trait list. ClientState scopes ordinary scalar, gear and creation consumers to current world/player, lazily binding an early unbound projection. Headers additionally include generation, world/player/definition object references and definition hash. Returned snapshots are clones. Pre-spawn GetOwnerStateSnapshot and selected creation/bag APIs intentionally remain unscoped.

ReceiveOwnerState validates protocol, character request-scope shape and nonnegative gear revision; same creation ID/mode/character packets reject regressions in either character or gear revision. It does not reject a different creation ID merely because its numeric revision is older. A no-character Rebirth packet cannot clear an already committed projection; explicit Base Game mode can clear it. Incoming packets bind to the *current* world/player, not a serialized session epoch. Consequently cross-session delayed packets are not independently rejected at this layer; transport teardown/ordering is a required native dependency, not a confirmed remote exploit.

GameStarting clears transaction/header/local queue state but preserves any nonnull early client owner snapshot when AsServer is false. WorldShuttingDown and GameShutdown reset ClientState, transfers, starter state and queued requests. Reconnect correctness therefore relies on shutdown events and native delivery boundaries. Queue entries have no world/owner-generation token; ordinary lifecycle clearing is the protection against entity-ID reuse. No stale-world mutation is established without missed/interleaved lifecycle delivery.

## Confirmed UI behavior and qualified risks

FirstEntry locks unknown/required creation state, supports integrated native pre-spawn chooser and post-spawn fallback, and stages a cloned selection. Ready removes the hold and restores control only if creation UI was pending, avoiding routine owner refresh stealing unrelated UI control. It retains staged creation while awaiting server commit; authoritative transaction idempotence remains the duplicate-creation barrier.

Confirmed retry defect: OnCreationResult lines424–438 clears stagedRequestId and, for Created/AlreadyCreated, sets stagedWaitingForOwnerReady=true while leaving stagedSelectionReady=true. TickCore's staged branch and DispatchStagedCommit lines797ff test selection/request ID but never stagedWaitingForOwnerReady. If a result is delivered before the Ready projection and a tick occurs between them, another request with a new ID is sent. PumpNotifications delivers queued results before owner listeners, so coalesced result+Ready in one pump normally closes the window. This is conditional extra request traffic while Ready is delayed/lost, not a duplicate award claim. The transaction AlreadyCreated guard was already audited.

A nonzero stagedRequestId has no timeout in this UI service. Missing result/owner delivery can retain the hold; a Ready owner snapshot still clears staging. Transport eventual delivery and state-request cadence are relevant next checks.

ClearAbandonedPendingPlayerUiAssignments reflects LocalPlayerUI.playerUIQueueForPendingEntity, destroys every queued non-primary UI GameObject, then clears the entire queue. It runs during start/shutdown/cancellation. Comments assert all queued entries are abandoned, but code has no world/attempt ownership filter. Native queue enqueue/dequeue lifecycle must be checked before claiming destruction of a valid UI. This is a concrete broad cleanup scope, not an established active failure.

The pre-spawn leave coroutine yields frames, then takes current GameManager.Instance and Disconnects. It has no captured world/session guard or cancellation token; ResetSession resets flags but does not explicitly cancel that coroutine. A new-session overlap is a hypothesis requiring native coroutine/lifecycle proof. No gameplay world was launched to manufacture it.

## Remaining exact dependencies

1. Native LocalPlayerUI.GetUIForPlayer/queue producers, EntityPlayerLocal.Awake, StartGame and Disconnect ordering: prove queue cleanup and outstanding coroutine ownership across sessions.
2. Native ToClient dispatch connection/session fencing and sender validation baseline: establish whether an old owner packet can ever reach ReceiveOwnerState after world reset.
3. Current owner-state codec/models full validation and authoritative revision-writer coverage, if not already parent-owned; this trace supplies no new codec full-read credit.
4. Actual UI creator/chooser/ProfileManager full bodies and XML/controller binding remain separate coverage; callers were located with `-uuu`, not counted as fully read.

No new shared finding identifier assigned. Parent should classify the conditional extra commit request as a UI/network reliability issue and keep lifecycle/native hypotheses qualified. No disabled feature was enabled.
