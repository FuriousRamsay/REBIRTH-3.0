# Camper and recycling migration multiplayer review — 2026-10-14

Status: IMPLEMENTATION AND REVIEW IN PROGRESS. No gameplay or multiplayer execution in this continuation. User requests inclusion after migration; this ledger captures the scope now and must be updated with the final files, build and installation receipts.

Source: actual installed REBIRTH 2.6 custom scrapping and recipe-output definitions. Target: current cumulative REBIRTH project and exact installed game assemblies. User explicitly authorizes a considered migration, not an unchanged copy of legacy architecture.

## Required surfaces

- Physical camper targets: Camp-only mode, native vehicle lease, exact installed station/source identity, reach, solid geometry occlusion, stale admission, packing transitions, locked vehicle and disconnected actor.
- Rear door: server-authorized toggle, epoch/revision rejection, persistence, independent observers, late join, automatic deployment opening and packing closure; presentation uses provided hook.
- Four scrapping stations: Outfit Designer, Ammo Recycler, Recycling Station and Shredder. Ordered multiyield rules consume the source once; source quality/metadata and detached modifications survive cancellation/return.
- Native grouped queue rows: save/reload, chunk unload, camper pickup/unload, single completion clock, partial completion, output-full retry, broken/missing tools, tool wear and full owner inventory.
- Refunds: no source refund after any output completed; no duplicate refunds from sibling rows, repeated commands, reconnect or retry.
- Remote camper commands: exact retained lease and installation, acknowledged cursor/input state, owner checkpoint, operation identity, source match, queue snapshot and tools/output refresh.
- Placed stations: preserve trusted native client-owner editing and synchronization. Do not add a general server shadow-inventory rewrite. Verify local owner versus server processing handoff on open/close/disconnect.
- Regular recipe additional outputs: actual legacy nested output semantics, once per completion, capacity for all outputs, no rewards on cancellation, no double reward from UI plus tile completion or native retry.

## Evidence and explicit limits

`_Documentation/VehicleStudio/RECYCLER_MISSING_MIGRATION_TRACE_20261014.md` identifies the real missing backend. Full legacy Scrap.xml contains 924 general recycler, 233 outfit, 48 shredder and 29 ammo rules plus one unsupported stationless entry. These rule counts are configuration inventory, not gameplay coverage.

New code is being reviewed against source; final compilation and runtime status are pending. No independent client/server session has been run. Missing legacy item/feature dependencies must remain explicit and must not silently produce a reduced payout. Full-inventory attachment returns must refuse before consumption rather than lose or duplicate items.

## Batched validation after authorization

Prepare one disposable save containing the four placed stations plus an equipped camper. Exercise direct native inputs, drag/shift, each station's representative multiyield item, workbench crafting, ordinary recipe byproducts, full output, tool break, cancellation before and after first yield, pack transition and reload in the same session. Record exact inventory/queue/tool before-after images and screenshots. Then repeat conservation and conflict cases on independent host/client endpoints with a shared build manifest. Capture duplicate/stale requests, competing actors, disconnect and reconnect. Do not launch or control the game until the user authorizes it.

2026-10-14 integration update: cumulative build and matching camper/recycler asset installation performed; see VehicleStudio/CAMPER_RECYCLER_DELIVERY_20261014.md and installed hash receipt. No gameplay/multiplayer validation. Latest log review additionally retains NPC rollback checkpoint mismatch as unresolved recovery evidence. Deferred bridge scenario added, not run. Audit remains open.

## MP-027 — Placed recycler returns precede source settlement without recovery

Severity: High, source-confirmed exception/retry conservation gap; runtime triggering not demonstrated. Affected: local/listen placed stations and remote native-owned placed stations. Camper stations use their separate existing actor-payment journal and are not included in this finding.

Trace reviewed2026-10-14 after cumulative installation:
- RebirthRecyclerRuntime.TryQueue lines148–149 returns detached attachments through TryDeliverPlaced before assigning the consumed Input and grouped Queue.
- TryCancel lines201–202 returns original source to inventory before removing grouped queue rows.
- Process lines304–308 returns final bonus before publishing output and removing the final row.
- RebirthRecyclerOwnerDelivery.TryDeliverPlaced lines76–81 calls bag.SetSlot and inventory.SetItem sequentially, then checks the post-image. It throws if verification fails. Earlier successful setters are not rolled back or recorded as an uncertain operation; station input/queue remains unchanged in these cases.
- RebirthRecyclerTransport.Request and TryAdvanceOwned clear scopes in finally. Publish executes after the try/finally, so an exception skips both station/player publication. Runtime processing.Active is cleared in its finally, allowing subsequent processing attempts. A repeated cancellation or enqueue can therefore return already delivered items again if the prior partial write survives.

This is not an allegation that native trusted client editing itself is invalid. The introduced cross-inventory mutation requires an outcome record or reconciled compensating boundary. Existing admission/reservation checks before setters reduce expected refusals but cannot prove all callback/setter exceptions impossible. Remote packet ordering and separate player/chunk disk writes require further native lifecycle tracing; no crash-atomicity claim is made.

Implementation plan after audit/checkpoint: retain native owner action and publication. Introduce one scoped placed recycler operation with immutable player and station before/after images, durable identity, held uncertain outcome and idempotent settlement; reuse an existing compatible native-owner receipt mechanism if its lifecycle supports placed tiles. Do not simply reorder writes (that trades duplication for loss), swallow exceptions, or overwrite inventory after an unrelated edit. Couple enqueue attachment returns, cancellation and final bonus through the same mechanism. Confirm recovery across UI close, character/world replacement and tile unload.

Validation plan: offline fault injection after each changed player slot and each station setter; verify retries preserve exact item totals and operation identity. Then one batched disposable session tests all three operations, full bag, save/reload, close/reopen and independent remote owner disconnect. Gameplay is NOT RUN; no new implementation or build performed in this audit continuation.

## MP-028 — Additional-output owner completion uses incompatible capacity prediction and non-atomic writes

Severity: High. Source-confirmed prediction/mutation mismatch; actual duplication not executed. Applies to personal/native owner inventory delivery on local/listen/remote clients. Dedicated/headless station output-array path is different and not implicated by this precise failure.

Reachability: RebirthFreshInit.InitMod unconditionally calls RunStepOnce("recycling",RebirthRecyclerInstaller.Install), which installs RebirthRecipeOutputIntegration in both normal configurations. Only the audit console command is Debug-only. Integration patches XUiC_RecipeStack.outputStack, AddItemToInventory and Update; native station HandleRecipeQueue has an independent scoped array path.

Trace:
- BeginUi preflights primary plus extras before native giveExp, with a synthetic primary metadata reservation. PlanInventory delegates to RebirthRecyclerOwnerDelivery.Plan, distributing each item across remaining backpack then toolbelt capacities. It produces clones; no reservation/lease or receipt is acquired by this preflight.
- InventoryInsert recomputes that plan but discards its resulting bag/belt images. It sets the temporary scope.Inserted=true, calls native AddItemNoPartial for primary, then for each additional output. A false return throws. It has no rollback or durable completed-subset record.
- Current installed Assembly-CSharp.dll was freshly decompiled to MultiplayerAuditNative20261014/XUiM_PlayerInventory.cs: AddItemNoPartial lines77–84 first requires backpack.CanStack(item) OR toolbelt.CanStack(item). This differs from spreading one output across both inventories. Native AddItem also has placement and callback behavior absent from the clone planner.
- Counterexample boundary: primary fits; extra requires N units, backpack has capacityA and toolbeltB with A<N, B<N but A+B>=N. Detached planner admits combined capacity; native extra may refuse after primary delivery. A current-content reachable recipe/count fixture still must be selected; the algorithm mismatch itself is confirmed.
- Fresh native XUiC_RecipeStack.outputStack invokes giveExp before insertion. Update decrements recipeCount only after outputStack returns true. An exception bypasses decrement. Harmony Finalizer clears transient UiScope and returns the exception; it does not preserve a durable insertion outcome. Re-entry can therefore repeat a primary/reward already applied. Exact Harmony reward interplay still requires qualification; do not count repeated XP as runtime-proven.

Scope controls reviewed:
- NativeInsert only intercepts the exact active station.Output array and matching primary type/count, computes all outputs on clones, then publishes; ThreadStatic native scope is restored in Finalizer.
- Receipt stamping snapshots byproducts under the existing receipt prefix. ReadReceipt bounds encoded string length and item count, validates canonical reserialization, and treats earlier receipts without the field as primary-only.
- RebirthStationOutputDelta removes receipt markers and compares the entire predicted output array; PaidOutputPreflight accepts genuine full-output refusal. Neither helper is a transaction or durable completion proof by itself.
- UI grid path mutates primary slots before UpdateData augmentation. This needs its own exception/callback review, separate from the confirmed inventory mismatch.

Finding-linked implementation plan: align prediction with exact native per-output admission and ordering, and use a scoped owner completion receipt/outcome mechanism covering all outputs, primary rewards and queue decrement. Preserve native owner callbacks/publication, but hold uncertain partial outcomes and reconcile instead of rerunning them. Do not claim clone preflight is an inventory reservation. Reuse an existing compatible native owner completion mechanism after tracing its lifecycle; do not invent a global server inventory writer.

Validation plan after all code: pure/native-shaped capacity tests for split bag/belt, stack limits, locked slots and item placement restrictions; inject refusal/exception after primary and each extra; assert exact inventory+queue+reward deltas and retry identity. Then batch personal, open placed, closed placed, mobile and independent remote cases in one prepared disposable environment. Game NOT RUN.

Registry/lifecycle still pending: CaptureEffectiveRecipes rejects non-main-thread calls; native recipe loader caller/thread must be traced before asserting inline output capture works in all topologies. Fallback definitions are keyed by name+area; queued jobs and definition edits need explicit persistence review. AuditCurrentRecipes currently has no caller found (search evidence only); do not describe it as automatic startup validation.

## MP-029 — Regular byproducts are not bound to paid queue definition

Severity: Medium (saved-job output drift after definition changes); source-confirmed integration omission, not an allegation of arbitrary client reward authority.

Full source chain reviewed: RebirthRecipeOutputs.Register computes SourceId for the external/inline output definition and stores it in a registry keyed only by recipe.GetName()+craftingArea. TryPlan reads that current registry at completion. RebirthStationGridQueue.DefinitionKey hashes native Recipe.Write plus tool/modifier/tags/material/wildcard fields; it does not include the additional-output registry or its SourceId. Stamp stores that hash in the paid fragment. TryResolveDefinition checks the same native hash and restores native recipe fields. RebirthStationGridAdmission persists this binding and paid grid, not the byproduct definition. RebirthStationCompletionReceipt.TryCreate calls StampReceipt only at completion, so its extras snapshot protects a created receipt, not the earlier admitted queue.

Consequently, changing only Config/RebirthRecipeOutputs.xml between enqueue and reload can leave the native paid-job binding valid while changing the eventual extras. Removing an output definition changes HasOutputs to false and can allow primary-only completion. This differs from the scrapping rule path, which stamps rule.SourceId and holds mismatched saved jobs. Current recipe lookup also intentionally merges same-name/same-area definitions; ambiguous variants need deliberate semantics, not an assumption that output item name uniquely identifies a recipe.

Plan: bind the effective byproduct definition (including explicit absence) at queue admission, using a backward-compatible versioned payload and a documented policy for pre-migration jobs. Preserve existing paid fragments/refund identities; do not silently rewrite them. Carry that frozen output plan into preflight, physical publication and receipt verification. If an output item ceases to exist, retain custody for recovery rather than silently omitting the byproduct. Evaluate personal native queue persistence separately; the grid admission ledger does not cover every personal recipe.

Validation after coding: enqueue against A then reload with changed counts, removed definition, missing item and a same-name/area variant. Verify either the frozen paid result or explicit recoverable hold, exactly once. Test older receipts remain primary-only and older queues follow the documented migration policy. Offline codec/definition fixtures first; combine reload cases with the already planned multiplayer session. No runtime test performed.

Coverage increment: read entire RebirthRecipeOutputs and RebirthRecipeOutputIntegration; receipt creation/identity/admission matching, physical output delta and paid preflight; RebirthStationGridQueue definition stamping/resolution/restoration and admission construction. This is targeted function/path coverage, not completion of all lines in the much larger admission service. Remaining: native loader calling thread, personal native serialized recipe lookup and full terminal receipt authority/recovery lifecycle.

Native recipe loader continuation: read current installed RecipesFromXml.LoadRecipies body. It is an IEnumerator which parses ingredients/wildcard nodes and yields by frame budget; unrecognized output nodes are not themselves persisted in Recipe. This supports MP-029's external registry distinction. Caller thread/iterator invocation remains unproven; do not infer it from yielding alone. No additional confirmed defect assigned.
# Required-tool degradation follow-up (2026-10-14)

### Native queue-owner trace completed after priority camper delivery

Inspected current installed Assembly-CSharp decompilation: TileEntityWorkstation.UpdateTick computes elapsed ticks, caps fueled work by BurnTotalTimeLeft, handles fuel, then calls HandleRecipeQueue. HandleRecipeQueue (line 632 onward in retained export) returns while bUserAccessing, subtracts elapsed from the active row, and can carry negative remaining time into subsequent batches/recipes. It contains no tool durability write. A prefix that charges the original row's tool for the entire tick would therefore charge the wrong tool when catch-up spans recipes; a postfix cannot infer all processed rows from the surviving queue alone.

The complete native XUiC_RecipeStack.Update body (retained export lines 301–455) separately handles inventory-full publication, completion, count/recipe clearing and then time subtraction. It likewise contains no durability write. Its inventory-full branches return without productive time subtraction. Ordinary open-station wear must follow this owner path and must not run a parallel tile clock while bUserAccessing is true.

Current RebirthStationNativePublicationHoldPatch checks publication authority, then (only in Survivor mode) checks the active non-scrap row's required tool availability. RebirthStationToolAvailability and its open-queue patch are availability guards, not wear implementations. The mode-dependent gate must be preserved or explicitly reconciled with the intended migration; it is not evidence of equivalent wear in all modes.

Camper ownership differs: RebirthCamperStationState.TickMobile rejects remote worlds and held publication/custody, uses the vehicle clock, then invokes HandleRecipeQueue. RebirthCamperStationOpenQueueProcessing.Begin commits an admitted local UI before processing, preserves lastTickTime, temporarily clears bUserAccessing without releasing the lease, and restores that flag on Dispose. It publishes partially completed state back to the UI even on exceptions, marking failed projection held. RebirthCamperStationQueueProjection.Refresh sets every UI card IsCrafting=false: these cards are projections, not a second crafting clock. Remote server-lease processing returns from Begin without this local UI scope; the transport lease handoff remains a separate dependency to trace.

Design consequence: regular crafting wear needs exact productive per-row time accounting at the native processing boundary plus an owner-UI equivalent, not one blanket per-frame decrement. Review finalizer/partial-output ordering against existing receipt holds, and include mixed-recipe catch-up and output-full branches in the finding-linked tests. This is source tracing only; no production edit, build, save modification or gameplay test occurred during this audit continuation.

The migrated scrap coordinator explicitly resolves ScrapToolWearPerMinute, limits productive seconds to remaining tool durability, and publishes a cloned tool array with increased UseTimes through RebirthRecyclerBonuses.ApplyWear. RebirthRecyclerRuntime.Process ties those seconds to the grouped scrap row; completed rows waiting for output space have zero remaining processing time. Non-degradable tools are excluded from wear. This is source evidence, not a multiplayer/runtime pass; partial publication and custody findings elsewhere in this trace still apply.

Ordinary crafting wear is a separate migration requirement, not satisfied by that scrap path. In the 2.6 reference, Harmony_XUiC_RecipeStack.cs:3115–3172 gates required tools then calls DegradeRequiredTools(..., forCraft:true) before native processing. Scripts/Craft/TEW_CementMixerCraftToolWear.cs:265–365 separately selects recipe.craftingToolType, skips non-degradable tools and resolves CraftToolWearPerMinute with ScrapToolWearPerMinute fallback. These two legacy paths need overlap/reachability review before choosing a replacement; copying both risks duplicate wear.

The current RebirthUtils.csproj explicitly excludes Scripts/Craft/TEW_CementMixerCraftToolWear.cs. Current RebirthStationToolAvailability checks presence/durability and holds open queues but does not apply wear. RebirthRecyclerInstaller installs scrap/output integrations; this does not establish regular-crafting wear coverage. Current crafting-source searches found wear writes only in the new recycler helper. Treat ordinary crafting wear as outstanding until the native open/closed queue paths and compiled replacement coverage are traced, implemented and validated.

Implementation/test plan addition: integrate one wear calculation with each actual queue owner (placed closed, native open owner, camper), charge only productive elapsed work, clamp at breakage, preserve non-degradable tools, and commit queue progress plus tool images consistently. Resolve duplicate matching tools deterministically. Cover rates/fallback, zero rate, exhaustion mid-tick, output-full retry, queue replacement, disconnect/lease handoff, save/reload and elapsed-time catch-up. Do not execute tests until all coding/scenario work is finished and gameplay is authorized.

