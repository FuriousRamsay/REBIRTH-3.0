# Sink recovery trace — 2026-10-15

Scope: current compiled REBIRTH 3.0 source, not runtime validation. Extends MP-031; does not restart the project-wide audit.

## Reachability and trust boundary

RebirthCamperModulePatchInstaller invokes RebirthCamperSink.Install. It registers native Inventory.Execute interception, prompt/mutation/owner-custody patches and GameUpdate/WorldShuttingDown callbacks. Sink request V1 declares ToServer and checks ValidEntityIdForSender before Receive. Receive additionally requires the current authoritative world GUID, authenticated ClientInfo/entity association, current character creation and Admitted vehicle access. Local authority requires EntityPlayerLocal. Admitted checks current entity/world, life, unattached player, distance, sink target validation and native lock permissions. This is a reachable feature, not a prototype.

Primary captures the exact held slot and before-image in a process-local LocalHold, publishes native player data, then sends the request. Prepare witnesses the held slot using local inventory or the saved remote owner image, validates the conversion/capacity, and durably writes the immutable frame before delivery. The frame binds world, stable vehicle, owner, character creation, operation, revision, slot and exact conversion. Read validates a canonical round trip and recomputes conversion; the client does not supply an arbitrary tank delta.

## Settlement and interruption windows

1. Before journal preparation: no tank mutation. A failure reply normally releases local hold; connection loss removes process-local intent.
2. Prepared, before owner receipt2: journal excludes subsequent operations. Reconnect Recover authenticates stable owner/creation and rebinds the player, but Apply with no LocalHold rejects receipt0. This is the confirmed MP-031 indefinite pending state.
3. Receipt2 published, before conversion: Apply can reconstruct a hold on reconnect, provided other custody systems are idle. The slot must still equal the frame before- or after-image. An unrelated slot change causes retention rather than guessing a payment; recovery currently offers no explicit resolution.
4. Native slot setter attempted: the code reads back the exact after-image before publishing receipt1. Setter refusal does not acknowledge successful conversion.
5. Receipt1 published: repeated Apply republishes proof without converting again. Tick waits for saved receipt evidence before advancing Prepared to Uncertain and changing reservoir metadata. Ordinary subsequent item moves intentionally do not revoke paid proof.
6. Reservoir updated: revision and amount are compared with exact before/after states. Vehicle save acknowledgment is required before journal Applied and release. Exceptions retain the record. Crash replay must be tested around both journal advances and vehicle publication; source ordering alone is not proof of durable atomicity.

Shutdown clears memory, not journal custody. Recover obtains pending records by stable vehicle identity and matches world/owner/character. Frame.Actor is not used as a reconnect identity requirement, allowing replacement entity IDs; authentication must continue using the current sender. Reply snapshots are revision-monotonic while completion can release the exact pending local operation even if its amount projection is older.

## Finding-linked design constraints

MP-031 needs durable owner intent or an authenticated explicit resume/cancel handshake. A missing receipt is not proof that no mutation occurred, so elapsed time must not cancel an uncertain operation. A new click must not silently substitute a new vessel/slot for the original transaction. Resume must bind the exact original operation, world, vehicle, owner and character; cancellation must have positive evidence of an unattempted conversion. Existing receipt2/receipt1 journals need compatible handling. Keep native client-owned item mutation and verified server reservoir settlement.

## Validation matrix to implement before execution

Inject interruption at each numbered boundary, including both sides of native player and vehicle save publication. Assert jar/bucket item counts plus tank millilitres conserve water, no duplicate conversion, and a resolved operation permits the next action. Include current/replacement player entity IDs, different owner, different character creation, another world, changed belt slot, death, competing custody hold, vehicle unload/reload, duplicate and delayed replies, malformed frame and capacity rejection. Batch reconnect/save/reload with other camper persistence cases. Host-only Bridge results cannot prove independent remote or dedicated behavior; report those topologies NOT RUN until tested with separate identities/endpoints.

## Save dependency follow-through

Read RebirthCamperSavedCursor.VerifyNative and HasPublishedReceipt in full. The witness opens the authenticated owner's native .ttp file, validates signature/version, parses PlayerDataFile to end-of-stream, checks the current session/directory again, and requires one exact operation receipt with value1 in saved buff data. Duplicate receipt names or malformed/trailing buff bytes fail. Published-receipt mode intentionally does not demand the old slot/cursor image, because legitimate later owner moves must remain allowed. This relies on native owner publication trust, not a server proof that an untrusted client executed every instruction. No replacement owner inventory is applied by the verifier.

Read RebirthCamperSavedVehicle Capture, RequestSave and Verify in full. Capture serializes complete native bag/entity bytes. RequestSave schedules the native single writer and expedites its countdown; Verify waits for the writer to terminate, reads vehicles.dat, checks signature/version/bounded record count, exact entity ID/class and complete bag/entity equality, and rejects duplicate records/trailing data. A successful scheduling call alone is not acknowledged as persistence.

Additional unresolved availability risk: sink Tick captures operation.Save once, then retries verification of that fixed complete snapshot indefinitely. EnergyRuntime.Tick independently persists changed energy state into the same vehicle ItemValue; sink's owner inventory guards do not acquire EnergyRuntime.Hold. If unrelated vehicle state changes between Capture and the native writer's snapshot, the exact complete expected bytes may never reach disk even though the sink reservoir does. Source establishes the fixed witness and independent mutation paths; the scheduling interleaving and reproduction still require validation. Do not weaken verification to mere save completion. Evaluate a semantic sink-specific persisted witness (stable identity, exact tank revision/amount plus operation evidence) or coordinated publication that cannot freeze an unreachable whole-entity image. Include active generator consumption, damage and concurrent authorized vehicle actions in the save matrix.

The sink callback passed to VerifyPublishedReceipt checks current vehicle and save directory, rather than current character creation. Original creation is authenticated at Prepare/Recover and in Apply. Review character replacement and stale saved receipts together before declaring this sufficient; an operation GUID is distinct from a complete character-lifecycle proof.

No production recovery change or tests executed in this trace. Save dependencies were source-reviewed; filesystem crash durability and scheduling behavior remain unverified.
