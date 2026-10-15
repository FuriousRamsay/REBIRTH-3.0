# Fire multiplayer producer trace

Status: source investigation in progress; no gameplay executed. This extends MP-002 and does not certify the subsystem.

## Configured producers

Read `Config/_Fire/items.xml` and `entityclasses.xml` in full. Torch, flaming arrow and flaming crossbow bolt attach `AddFireDamage` to `onSelfDamagedBlock`. Zombie template attaches the same action under fire-element/AOE-buff requirements and a random roll. These are distinct producers: do not assume that a server-owned zombie event proves a remote player item event executes on the server.

The extinguisher uses `RangedRebirthExtinguisher, RebirthUtils` in Action0. Its selected powder magazine attaches `RemoveFire` to `onSelfPrimaryActionRayHit`, positionAOE, range 5. The preceding XML comment saying no custom ItemAction is needed is stale relative to the actual configured class.

Read the custom ranged action in full: it raycasts, sets entity or block context, and fires the primary/secondary ray-hit event. It does not independently send an authoritative fire mutation or create an action receipt. Whether native ranged replication invokes this override on the server for a remote shooter still requires exact-version native caller tracing. No matching native ItemActionRanged/NetPackageEntityAction source was located by the targeted search in the existing shared temporary extracts; this is a missing evidence item, not proof of absent native replication.

## Mutation admission

Exact-native continuation: directly decompiled installed Assembly-CSharp SHA256 `FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705`. Read ItemActionRanged ExecuteAction/TryExecuteAction shot invocation and complete ItemActionEffects body; GameManager ItemActionEffectsServer/Client; NetPackageItemActionEffects in full. The original firing path calls virtual fireShot, then sends action effects. The receiving package calls the effects service, which dispatches ItemActionEffects rather than ExecuteAction/fireShot. Ranged ItemActionEffects handles firing visuals/audio and onHoldingEntityFired, with no raycast or ray-hit min-event. Therefore this native replication route does NOT supply the missing server-side extinguisher ray-hit. This materially narrows MP-002: effects broadcast cannot be cited as the authoritative counterpart. Other patches/callers still need exclusion before claiming complete gameplay reproduction; runtime remains NOT RUN. Preserve native client-owned original action receipts when implementing the eventual correction.

`MinEventActionRebirthRemoveFire.Execute` resolves the hit position, fills its bounded cube, then calls ExtinguishMany directly on the server or sends Extinguish on the client. Cascade ignition has the equivalent direct-server/client-request split.

`NetPackageRebirthFireRequestBatch.ProcessPackage` authenticates source entity, handles snapshot reads, then unconditionally rejects Ignite, Extinguish and Remove. Distance validation and ApplyAuthoritativeRequest below that branch cannot admit these operations. The comment describes a planned server witness; a comment is not evidence that every configured producer has an equivalent native server event.

`ExplosionAttackBlocksPostfix` explicitly requires server authority and schedules ignition from changed block positions, or extinguishes for negative block damage. Thus explosion integration is a separate path and is not blocked by the rejected client request handler. It does not establish that remote torch/arrow/extinguisher events work.

## Next evidence and eventual test batch

Trace exact native ranged and block-damage event ownership before choosing the fix. Preserve native action/receipt trust architecture; do not blindly remove rejection or add duplicate server execution. In the later authorized gameplay batch, exercise remote extinguisher, remote torch, remote flaming projectile, server zombie ignition and explosion ignition separately, observing authoritative fire state plus both clients' visuals. Reconnect/snapshot and delta overflow still require review. All scenarios are currently NOT RUN.

## Snapshot and delta continuation

Persistence continuation: read RebirthFirePersistence completely and the service Restore body. Format v2 validates magic, version and bounded counts before calling Restore; backup is attempted for both absent and invalid primary. Restore keeps unloaded-chunk entries, enforces existing global/per-chunk limits, excludes trader positions and avoids an overdue catch-up burst. These are intentional existing policies, not newly proposed behavior. The shared RebirthDurableFileCommit was read completely and uses flushed caller temp + File.Replace or first-publication Move, without destructive fallback. Failed load still leaves autosave/shutdown writable: MP-021 records this separate recovery defect. No fault injection has run.

Read VisualManager initialization, delta/snapshot assembly and replay, maintenance, and ClearAll; FireSystem QueueDelta/FlushDeltas, ignition/extinguish/remove producers, and complete lifecycle handlers. These are section-level reads, not a claim that both large files are fully reviewed.

Normal QueueDelta flushes at MaxNetworkDeltaEntries (256), addressing the suspected normal-producer truncation issue in the packet Setup bound. Each mutation increments service revision, but FlushDeltas sends one final revision for the entire ordered list. While assembling a snapshot the client buffers each entry with this batch revision. Both successful snapshot completion and aborted assembly sort buffered entries using only Revision.CompareTo. Equal-revision entries therefore lack an ordering guarantee. An add followed by extinguish/remove for the same position within one batch must retain its source order; the replay representation currently cannot guarantee that. Track as MP-020, with runtime frequency unmeasured.

Assembly checks bounded chunk counts, aggregate counts, snapshot identity and revisions; duplicate chunk indices are ignored. A five-second assembly timeout requests resynchronization with a two-second request cooldown. Successful request submission clears the resync flag before a response arrives; absence of every response chunk does not start an assembly timeout. Verify native reliable delivery/disconnect handling before treating that as a separate defect.

GameStarting and shutdown clear visual projection, including epoch and pending snapshot identity. Server PlayerSpawned sends the initial snapshot; host initialization uses a direct local snapshot. Thus the source does contain session teardown and initial synchronization. Newer numeric epochs replace projection and older epochs are rejected within a session. Exact native network teardown remains relevant to delayed packets across sessions; do not claim old-world leakage solely from a reset epoch.
