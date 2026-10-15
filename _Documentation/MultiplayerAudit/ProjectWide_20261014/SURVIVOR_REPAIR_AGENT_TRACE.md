# Survivor repair signatures, wear and native owner persistence

2026-10-15 audit only. No production edits/build/tests/game/gameplay commands/git/shared report or ledger writes. All4 current Repair sources fully read; prior NPC_TRADE upload method inspection was partial and is not counted as full coverage. Other agent Background/Workmanship/Medical/Resource services untouched apart from named provenance helper dependency.

## Full-read manifest

| Current source | Lines | SHA256 |
|---|---:|---|
|Scripts/Survivor/Repair/RebirthRepairSignatureService.cs|476|9829E3486504DC7B35D69C83F3E5E1AD5D6AFA2769BAAD8620D205CC3A9E105F|
|Scripts/Survivor/Repair/RebirthItemConditionStatAdapter.cs|274|3FD93224ACF4A5252C77F9723C68400F46037C337462C4640888B50EF7FCCBB3|
|Scripts/Survivor/Repair/RebirthRepairSignatureNetPackages.cs|28|A5CB07130F055BE8C31C0F18A691B070702B7C54AB5AE0463099FAB8EE064550|
|Scripts/Survivor/Repair/RebirthRepairSignatureVectorHarness.cs|22|AB50BC6E9575E269B9F20217700EF7A6989F15B42DF8EA377DB221FA50BBE7C3|

4 new unique C#800 lines. Service1–170/171–335/336–476 with450–476 reread after output truncation. Adapter1–150/151–274. Native/package bodies below are exact separate evidence; containing native ItemValue/RecipeStack and ItemProvenanceAdapter not claimed fully read.

Read-only existing inspector re-extracted current installed NetPackagePlayerInventory into E:/_Haven/shared/REBIRTHTemp/SurvivorRepairNative20261015/NetPackagePlayerInventory.cs, SHA25660538E7F7A33B7FEB5B1DEF4356E199978973B477D5449ECE40A9CAD7EB6E958, fully read. This confirms the prior DescriptionBindings extract was current. Installed assemblyFCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705. No build. Existing native XUiC_RecipeStack outputStack/SetRepairRecipe and ItemValue Stats/Clone/MaxUseTimes named bodies selectively read; their broader custody traces remain prior native/Phase89 evidence.

## Confirmed remote durability signature adapter cannot read installed upload shape

ProgressionInstaller58 calls RepairSignatureService.Install and120/121 explicitly installs queue/completion patches. Install dynamically patches NetPackagePlayerInventory.ProcessPackage prefix by real current type/method, so this is active production integration.

NativeInventoryCommitPrefix180+ reads incoming `toolbelt` and `bag` members cast to ItemStack[]. Current exact native NetPackagePlayerInventory has ONLY inventoryData/bagData/equipmentData MemoryStream fields and dragAndDropItem. It serializes native inventory persistency blobs, and ProcessPackage assigns those blobs to Sender.latestPlayerData and marks modified. It exposes no incoming toolbelt/bag arrays. Reflection ReadMember therefore returns null for both arrays; FindItem always returns null and every pending repair is skipped. No remote signature correction is enqueued by this installed native upload path.

This is source/native-confirmed remote functional failure for Maintenance loss mitigation/Gunsmith/Tailor restoration, not a speculative stale inventory race. The begin request also searches server live replica grids, whose freshness is separately weak, but even a successfully captured token cannot complete through the blob/array mismatch. Respect native owner inventory custody when correcting later; this audit does not decode/rewrite a second authoritative owner inventory system.

## Native original repair and deferred output

Queue Prefix stamps original durability cap on the source item and calls Begin before native SetRepairRecipe. Native method may return false when busy/existing original; no __result acceptance check cancels that token. Begin packet contains player/type/seed/quality only and authenticates sender ID. Server selects matching owned grid item with positive wear, captures prewear/max/cap and an AcceptedRevision hash; it is not evidence of paid native repair admission by itself. It does not debit repair kits or mutate repair wear.

Native outputStack performs degradation/repair on a clone, clamps wear to zero, then clears AmountToRepair before delivery; existing Phase89 trace proves full-inventory false-return retry loses positive-amount observer state. RepairSignatureCompletion Prefix has the SAME AmountToRepair>0 condition, Postfix requires true; therefore local deferred successful output also misses this durability signature, an MP-015 family extension. No new identifier here. It does not mean the native repaired item/payment is lost.

For immediate local success, prefix retains pre-repair identity, CompleteLocalRepair checks accepted revision of that identity, locates the delivered matching grid stack and requires decreased wear before applying bonus and removing token. Failed adapter retains token but no independent local retry pump calls CompleteLocalRepair afterward; only later matching native completion could revisit it. This is retained permission without guaranteed liveness, not durable settlement.

## Cancellation and runtime custody

`rg -uuu` across production Scripts finds RebirthRepairSignatureCancelPatch only at its declaration445, while explicit installer includes Queue/Completion and never Cancel. No assembly PatchAll in existing architecture. Thus HandleOnPress cancel observer is dormant. The public CancelPendingRepair has only that dormant caller. Cancellation does not immediately retire the server pending token through this code; tokens expire15 wall-clock minutes or world/game shutdown.

Pending state keyed by player entity ID plus type/seed/quality, with accepted32-bit revision of type/seed/quality/wear/max/original cap. No world/creation/stable owner/native queue transaction ID captured. Multiple same-key begins replace prior token. Accepted revision is not full item original: modifications/other metadata/source slot/physical identity are omitted. No malicious collision or server entity-ID reuse chain established; do not label hashes exact authenticated item identity. Lists and correction queue lack a global admission bound beyond lifetime/owned grid search. Source operates normal main-thread native callbacks, but does not explicitly assert main thread.

Shutdown clears Pending/Corrections outright; no durable original signature ledger, acknowledgment/retry or reconnect recovery. Existing metadata on physical items travels native saves; pending permission does not. NextCleanupUtc process timer is not cleared by ClearRuntime, normally delaying cleanup at most prior1s. No failed-snapshot retention guarantee from this runtime dictionary.

## Authoritative max adjustment and original cap

Maintenance reduces only this repair's newly lost max with ceil rounding; no loss means no restoration. Gunsmith/Tailor restores configured fraction of current deficit toward original cap only for classified firearm/wearable. Original cap helper uses crafted provenance if present, otherwise typed durability metadata; Force helper respects existing crafted cap. Service calculates cap at least pre/post max and never allows adapter target above it. It does not restore unrelated wear or redesign native death/repair degradation.

Death prefix stamps tool/bag original caps before native OnEntityDeath (dynamic EntityAlive declared method), not a new damage model. Equipment outside those arrays is not scanned here; native equipped armor custody depends on its own durability/provenance path. No assertion every item already has baseline solely from this prefix.

Provenance named helpers161–198 validate positive stored baseline, encode metadata and refuse overwriting valid crafted provenance; full provenance service belongs other coverage. Current Native ItemValue.Clone allocates/copies Stats array and clones metadata/mod arrays. MaxUseTimes derives from DegradationMax then native durability modifier. Stats are serialized by native inventory persistence, not a new custom save for each signature.

## Condition stat adapter safeguards and limits

Adapter discovers existing Stats array, verifies stat type DegradationMax and numeric raw field; it never creates/clears unrelated magnitude entries. It probes on clones, explicitly isolates shared value-type arrays or refuses reference-shared mutable elements, estimates local slope, evaluates bounded fine/coarse candidate values, excludes any result below current/above cap and writes only a safe upward result. Post-write mismatch attempts restoration of prior raw field and reports failure. This is materially stronger than blind max rewriting.

Reflection caches are by runtime type/index but cached index is revalidated for actual DegradationMax. Native shape absent/zero response fails closed with one warning. Native raw encodings wider than supported int ranges are conservatively constrained. Final live write cannot guarantee rollback if reflective restoration itself fails; no actual such failure observed. Probe work can allocate many clones; this is infrequent repair work, no performance measurement performed. Getter/reflection/effect exceptions are caught rather than treated as permission.

## Corrections and replay boundaries (prepared remote path)

If upload shape were fixed, server currently mutates the incoming item before native ProcessPackage and removes pending immediately after adapter success, then queues a primitive targetMax/cap correction. GameUpdate dequeues before SendPackage, no exception retention or owner acknowledgment. A transient unavailable player, ownership item movement/collision, reconnect or lost application could leave only server-side uploaded copy corrected; a later owner upload can overwrite it. These are conditional future-path custody issues because the current incoming-array mismatch prevents normal correction creation.

Client correction authenticates direction via native dispatcher, then requires primary player entity ID and first matching type/seed/quality. It does not bind expected preimage/revision/current creation/world or exact slot. It forces baseline then asks upward-only adapter; no lowering/reset/native item mint. Native reliable delivery alone would not prove successful exact owner item application. No actual cross-owner grant/replay exploit claimed. The correction packet carries no result/receipt; ordinary custom ToServer request cannot directly choose targetMax.

Vector harness only tests arithmetic/caps; it does not validate native package arrays, installed patches, paid repair or output retry and was not run. Main confirmed findings: remote upload member mismatch and dormant cancel observer; deferred local output loss integrates MP-015 family. Parent assigns severity/IDs and integrates full manifests. No runtime qualification or production fix delivered.
