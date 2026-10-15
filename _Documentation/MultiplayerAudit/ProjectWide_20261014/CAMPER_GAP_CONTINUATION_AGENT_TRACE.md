# Camper gap continuation: owner payment and cancellation

Audit-only2026-10-15. Twelve current full literal source bodies listed below; prior narrative reconciliation found caller references but no explicit full body evidence for these exact files. No production/build/game/tests/Git/shared ledger. Uses earlier station continuation/client/recovery/native actor payment traces for their independently scoped dependencies. Supplemental caller excerpts are not full-file credit.

## Production callers and topology

ModulePatchInstaller installs PublicationWatch and CancellationWatch in production non-diagnostics branch; OpenQueueCancellationHold intercepts native XUiC_RecipeStack cancel, verifies exact admitted window/card/job then local QueuedAuthority binding→CancellationWatch; remote routes Client.Cancel→server RemoteCancellationWatch (separate prior full scope). Managed cooking takes existing CookingQueueAdmission path and is not authorized by this watcher. Current StationPreparationService excerpt persists attempted admission and consumes original native attempt before local PlayerPayment.TryApplyLocal or remote original callback; claimQueue one-shot guards paid publication. PublicationWatch.TryPlan remote uses PlayerSavedSource, and debit payload server→RemoteOwnerEffects Debit→PlayerPayment owner client; RemoteOwnerApplication watcher uses DebitPayload.VerifySaved rather than a receipt-only acknowledgment. No direct remote server-replica bag setter is substituted.

## Exact owner images and native debit

PlayerSavedSource reads authenticated final native owner file, detached bag/belt, and native bagData format2 ItemStackGrid lock bits with exact EOF/capacity. It grants no custody alone. PlayerPaymentImage freezes owner IDs/owned belt capacity, before/after full slot images, locks, stationinput and paid fragments. Strict canonical shape/attributes, native item roundtrip, <=169bag/20belt/9allocations,2MiB aggregate text budget. Parsing recomputes after images from positive bounded allocations and compares every slot; callers separately authenticate recipe plan and locked resources. Repeated allocations consume successively, cannot exceed source count. Parsing by itself does not establish current access.

Payment.Operation derives deterministic GUID from SHA256 canonical attempted player-payment binding. Current native owner checks mainthread/exact currentworld entity/alive/creation and library holds/no pending remote resource transaction/current mobile window/address/payment source/exact stationinput/stable local identity/projected creation. Before debit rechecks all bag+belt cells, locks and owned capacity. Local writes phase2 receipt, saves native player, verifies phase2+before grids in SAME final file, then ApplyExact uses native Bag.SetSlot/Inventory.SetItem only for changed slots, revalidating complete expected custody after each setter/callback. Stops on first mismatch/exception retaining phase2; no refund/replay. Sets phase1 and saves/verifies same final-file exact after grids before returning paid. Remote TryApply requires original phase2 and same current owner/session images; generic owner application supplies its own durable phase lifecycle.

FileWitness parses exact native EntityBuffs.Version/buff list/CVar list/EOF, unique canonical rbCamperOwner_GUID at phase2(before) or1(after), and bag+belt from same native primary file. Uploaded cache/backups/receipt-only do not qualify initial paid publication. HasSavedReceipt intentionally allows later inventory change only after confirmed admission, for historical queued-owner correlation; stable persistent identity maps queued entity actor before reading historical file. CurrentCreation/projection dependencies retain prior upstream native-mode helper limitations.

DebitPayload <=4MiB DTD-prohibited XML binds actor/stable owner/canonical ID/attempted mobile admission and deterministic op; server TryCreate requires exact authoritative actor/creation. Owner dispatch mode-gates native non-Survivor payload versus Survivor admission, checks primary remote owner then payment's current window guards. Server VerifySaved rechecks original current callback/creation and exact after file. Packet/delivery authentication remains prior RemoteOwnerApplication trace; parsing payload alone not sufficient.

## Cancellation before effects and after persistence

Checkpoint requires saved original queued publication, current owner/lease/exact station snapshots and no completion/training/save/completion hold. Computes remaining one unique marked job and creates immutable terminal choice/refund/attempt/before/after queue/terminal/digest. QueuePlan synthesizes queued-owner comparison choice only for native exact row removal, never stores that synthetic actor as requester's real intent. CounterRefund clones full native input, only adds exact molten unit payload to uniquely matching material counter, checked int bound; no live mutation.

CancellationSave retains operation before staging three owner records, caps64 each, marks dirty then requires FlushPlayer and primary saved exact attempt. Failure retains original cached obligation, grants no removal. Removal requires saved operation reference and unchanged stagedbefore, holds station BEFORE replacing input+queue, setsModified and starts native retirement save. It retains out operation even if a subsequent callback throws. Confirmation requires original hold/current owner/unchanged planned queue/digest and saved vehicle proof. CancellationRecord binds19 strict attributes including parent admission/queued/intent/refund/attempt hashes, native vehicle/class/saveScope/station/recovery and actors; record cap64/dependency validation/read-write repository chain. CurrentCancellation correlation permits unrelated later station work but forbids original job/completion receipt; it does not reconstruct a native effect.

Only after native removal proof and saved owner cancellation publication can destination be planned. Local exact bag reference/locks checked; remote planning uses saved owner bag, not replica. RefundDelivery/RemoteRefundDelivery must then supply original saved application permit/receipt before TryReleaseSettled removes station hold. Dispose deliberately does NOT release custody; no timeout authorizes another credit. Archive is separate prior reviewed save-before-retirement chain. Repository1324/1574 contexts write/read dependency-validated cancellation records; whole repository not reread.

## Confirmed lifecycle issue and limits

GAP-CANCEL-A: local CancellationWatch entries are static cap64 and only removed when Advance returns true. Install registers GameUpdate only, no shutdown reset. Lease handoff branch83-103 runs ONLY after Released. A user can cancel, then close/leave/die/change world before saved checkpoint/removal/refund settles; saved/current binding refuses further advance, but unreleased entry remains forever in process. Future ticks retry/once warn, keep strong original player/world/checkpoint and count against64; same job IsWatching remains true. After64 such unresolved original entries new local cancellation TryWatch rejects globally. This extends prior remote refund watcher lifecycle family to the actual local original watcher; do not treat as extra item-loss/duplication defect. Durable obligation must survive appropriate handoff; simply clearing entries/holds without recovery would be unsafe. Native world teardown cannot clear this ordinary static dictionary itself. No repeated gameplay reproduction executed.

Failures deliberately retain exact custody; source audit does not prove every pending transaction recoverable immediately. Existing repo flush uncertainty/save witness qualifications remain. No production fix or authority redesign proposed. Partial multi-file caller output truncation affected only supporting excerpts, not the twelve manifest full reads.

## Current full-read manifest

| Source | Lines | SHA256 |
|---|---:|---|
| Scripts/Vehicles/Camper/RebirthCamperStationPlayerDebitPayload.cs | 78 | 2F4DD9A17F907D7B9772DB0B3EA77481E2A4F3190A90424709B603E29F13F121 |
| Scripts/Vehicles/Camper/RebirthCamperStationPlayerFileWitness.cs | 53 | AE28E912176A30C455CEB4D68F0E572BB38DDB7ED8E4754CB1B75DD089F674B1 |
| Scripts/Vehicles/Camper/RebirthCamperStationPlayerPayment.cs | 157 | A46564C56F9E7A24303917E568E99880466F97928F72D66714CD5A9CD866B7C9 |
| Scripts/Vehicles/Camper/RebirthCamperStationPlayerPaymentImage.cs | 150 | 6A6E19C729C5EC3BF30D6E7272FC845D3C3296BE7ED10835C5938EAAB5F52DC2 |
| Scripts/Vehicles/Camper/RebirthCamperStationPlayerSavedSource.cs | 35 | ED85756BD00DD85975701B52289DC54E831ECB3CF7A71A302092989DB42BAB39 |
| Scripts/Vehicles/Camper/RebirthCamperStationCancellationCheckpoint.cs | 116 | E35301F3C332BC307AB14D8A04BFBFEA4827E7216ED4D8AE491DB97215E87BDF |
| Scripts/Vehicles/Camper/RebirthCamperStationCancellationQueuePlan.cs | 20 | 67308CDDBA6EBF77C8ACCEE20FB6B0B53128FDC306E5AADE6FE4AE3CE2A19C9A |
| Scripts/Vehicles/Camper/RebirthCamperStationCancellationRecord.cs | 146 | 6988797EC8A7826BB2A8F5F862AFA10D7EF2D63988CA880BF6C07924D727217B |
| Scripts/Vehicles/Camper/RebirthCamperStationCancellationRemoval.cs | 190 | 704589B68C63D7982A63DFD254EF46D7B039E660B9344BA96763907184F97672 |
| Scripts/Vehicles/Camper/RebirthCamperStationCancellationSave.cs | 65 | FE9E86982B40106FCA8620A1F901A237A62A8F960E1179C47D19BFCC549C1AAC |
| Scripts/Vehicles/Camper/RebirthCamperStationCancellationWatch.cs | 143 | 93C732678852C09054B0391C884BDB8916BCEB623547B8D0C28EB1E81A79AD4B |
| Scripts/Vehicles/Camper/RebirthCamperStationCounterRefund.cs | 36 | D7EADBAC5DDB47CA1368C397A4C2A85902B52CF8BE479F71DAF0FF2DD60D4AAF |

Full manifest total 1189 lines. Hashes current at completion; no runtime or deployed DLL parity claim.

