# Survivor teaching, consent, time and durable outcome trace

2026-10-15 audit only. No production changes/build/tests/game/gameplay commands/git/shared report or ledger writes. Prior manifests/coverage read before choosing scope. TheorySolo completion/retirement and StudySettlement remain other agent coverage; no duplicate claim for those internals. All negative caller searches used rg -uuu, including Debug.

## Full-read manifest

| Current file | Lines | SHA256 | Unique coverage |
|---|---:|---|---|
|Scripts/Survivor/Progression/RebirthTeachingService.cs|908|65BDCAA4392DD847807550700CCDA6A67F00926A40A8FBE74EA447EA950A10EB|new full|
|Scripts/Survivor/Progression/RebirthTeachingNetPackages.cs|66|271BD06157A0CA3AD94359C279C576D7681386BDE122A1D17DFFD859F5C86AAB|new full|
|Scripts/Survivor/Progression/RebirthTeachingVectorHarness.cs|25|D6D07F2C3CBC95002319209E4B0233E04096DF567B010D473222431377DC83A8|new full|
|Scripts/Survivor/Progression/RebirthTeachingOutcomeStore.cs|361|DE57543DC864E17B86E31163DF34747E8C4E2C58B834B7716F5B3AC9E9638D2C|repeated full, already ledger full|
|Scripts/Survivor/UI/XUiC_RebirthTeaching.cs|62|D66325CD5F1180E8141E419BE4F6C21A23F9C31FCD74E830DE6E5DAF50693347|repeated full, already ledger source-read|

New unique: 3 C# files999 lines. Service read bounded1–235,236–460,461–690,691–800,801–908; ending reread after broad output truncation. Store read1–200/201–361. Specialist114 current hash A9B06778C7DDD3004318916A935E7B5C70FDF06804BDFDA94AF674869E15590F already fully manifested in SURVIVOR_LITERATURE_AGENT_TRACE.md and reused without new full read. Native timing/dispatch relies on preceding native lifecycle/metabolism traces, no new decompilation.

## Active native/UI and multiplayer entry

ProgressionInstaller65 calls TeachingService.Install. It registers GameUpdate/world shutdown/game shutdown. Actual Players UI OpenTeaching at XUiC_RebirthPlayers545 invokes TeachingUiService.Open; Config/XUi_InGame/xui.xml122–126 defines both teaching groups and windows.xml14467/14729 binds RebirthTeaching/RebirthTeachingOffer controllers. These were targeted caller/XML nodes only, not entire-file read claims. NPC Interaction TeachHandler244 is the only -uuu caller of TryTeachOwnedNpc.

Subjects/offer requests are ToServer and reject null/remote World and invalid instructor sender entity. OfferResponse authenticates student sender entity. The native direction/auth enforcement and ValidEntityIdForSender implementation are established in SURVIVOR_NATIVE_LIFECYCLE_AGENT_TRACE; ToClient offers cannot be injected through the authenticated server client-packet path merely because their ProcessPackage has no local guard. Subject response bounds skills64; client panel displays48. Strings use bounded existing codec. No client directly supplies numeric knowledge, reward amount, history timestamps or outcome journal flags.

Listen owner calls the same service directly; remote sends packets and receives only targeted owner UI feedback. Dedicated operates server records and no local window. UI subject response matches both request ID and target entity. Student acceptance references a server-owned pending OfferId and must match its StudentId. Expiry is enforced while consuming the pending offer under Gate; cleanup timing is not the consent boundary. Closing the offer window sends decline if not already answered.

## Session, curriculum and time contract

Players must be distinct, alive, within6m, not creation-held, have resolved stable identities/complete records and normalized creation IDs. Subject must exist, be nonadvanced and not Teaching itself. Instructor practical Value must be finite and >=10; Theory >=20 and gap>=5. Consent revalidates these plus captured instructor/student creation IDs and no active participant session. Pending offers capture both creation IDs and a random outcome ID. Active sessions recheck loaded entities/death/mode/hold/proximity/creation each update; completion revalidates competence/gap/cooldown. Entity-ID reuse alone cannot bypass captured creation checks.

Session duration is20 seconds measured Time.realtimeSinceStartup, offer lifetime30 seconds. This is server process real time, not world time/day acceleration. Proximity is sampled each GameUpdate; the code cannot establish uninterrupted conditions during a frame stall. No exploit is inferred from sampling. Pair+subject cooldown uses persisted UTC timestamps/1800 wall-clock seconds; it advances offline. A future timestamp remains on cooldown until clock catches up. Invalid out-of-range timestamp parsing is a prior repository robustness issue, not authenticated client control.

Transfer is continuous Theory1.5..3 scaled by Teaching, limited to half the gap and below instructor. No recipe, discipline, initiation, binary ownership or item custody transfers. Teaching LBD is a separate discrete award only after durable student/instructor stages. Lasting Lessons is a finite time multiplier1..3 using authored background tuning, not a new skill level.

## Persistence, retry and reconnect

CompleteSession freezes a single PreparedOutcome before uncertain journal storage. If TryReserveOriginal fails it keeps that same Active object/outcome, rather than assigning a replacement transaction ID. It removes Active only after durable reservation. Store checks immutable SameIntent and original ordinal; dirty uncertain writes must be saved/exactly reread before Snapshot or original evidence is usable. Outcome fields reject nonfinite/negative values. SaveLocked compares the exact final XML root against its intended snapshot, beyond merely trusting atomic-write return. Binding is both owner storage keys AND both creation IDs.

Application is staged: student Theory max-to-target plus lasting lesson and receipt -> save -> journal ACK; instructor cooldown/history plus receipt -> save -> ACK; TheorySolo evidence delegate; teacher skill/insight -> reward ACK. If journal says applied but a restored character lacks that receipt, service holds rather than replaying consumed timers/additive effects or silently retiring. A publication failure after saving/acknowledging is caught and queued for owner publication without re-awarding. Unknown ACK saves retain uncertainty; store protects pending and backup outcome receipts from pruning. Final removal waits all stages including Solo evidence. Existing MP-015/outcome-retention analysis is reused, no duplicate finding ID.

Recovery every second scans saved pending outcomes, resolves each participant by stable storage key among current world players, then rechecks creation and original-record/live-world scope. It requires BOTH participants online even if only one participant stage remains; it does not open offline records and cannot finish until they overlap online. This is a liveness limitation with retained custody, not evidence of discarded progression. Recovery intentionally does not reimpose lesson proximity/death after the outcome is already durably reserved: it is finishing an original completed event, not authorizing another lesson.

Shutdown clears pending offers/Active/lesson caches and resets the outcome store's in-memory selection. Saved outcomes reload by current save-directory path. A failed pre-reservation write retained only in memory can be lost on reset (same broad failed-final-write family as MP-036), but no student/reward mutation occurs before reserve succeeds. A completed durable original remains recoverable. No duplicate award or lost applied lesson demonstrated here.

## Lasting lesson timer and offline behavior

Loaded players with saved lessons are rediscovered every5 seconds. RemainingActiveSeconds decrements by clamped unscaledDeltaTime0..0.25 per GameUpdate; absence from world pauses the timer. It does not use DateTime elapsed to charge offline time. Unlike active session eligibility, timer ticking has no IsDead or creation-hold check, so a loaded dead/held player continues consuming the remaining lesson. This is source-confirmed timing scope; native loaded-dead duration remains qualification, consistent with Condition trace.

Timer state touches revision at most once/sec and flushes dirty known players every30 seconds. DirtyLessonPlayers is cleared before FlushPlayer and the return is ignored; underlying record.Dirty remains the repository retry custody, but this scheduler drops its specific retry marker. If a player disappears before flush it skips that ID; general lifecycle/disconnect persistence is the remaining save path. No new independent loss finding beyond existing repository reset handling is asserted.

The previously audited character reader admits nonfinite remaining lesson/multiplier values; +Infinity remaining never expires under finite dt. Service's normal authoring path clamps duration60..7200 and store ValidOutcome rejects nonfinite originals, so malformed-save admission is not a remote lesson-request numerical exploit. Practical instructor Value explicitly finite, whereas Theory/tuning comparisons rely on authoritative valid records; store catches nonfinite final player outcomes.

## Owned NPC route and permission boundary

TryTeachOwnedNpc is a separate immediate solo-parity route, not the20-second player consent flow. It receives a stable NPC key, stores synthetic companion Theory in instructor TeachingHistory under npc:<key>|subject, chooses largest valid gap, updates cooldown/count/target, and awards instructor with a completion receipt. When award returns false but receipt exists it retries the same dirty snapshot; without receipt it restores history. The completed lesson is not repeated just to settle insight.

The service itself checks server/living instructor/eligible progression but does not resolve or authenticate NPC ownership from the string key. Sole production caller is NPC interaction handler, whose command context/permission route supplies context.NpcId; presentation permission provider grants Teach in the owned interaction branch. Existing NPC interaction audit owns the full dispatcher checks. This tranche does not claim arbitrary NPC keys are reachable remotely by omission of a second internal ownership check. NPC histories are deliberately excluded from ordinary history pruning because they are the companion Theory store; no separate NPC profile Theory mutation occurs here.

## Diagnostic and completion limits

VectorHarness is pure arithmetic/constants checks; it cannot establish native consent, persistence, reconnect, proximity, or live installed patches and was not executed. No external curriculum service was discovered beyond current registry skill eligibility and the already-covered specialist route. No new bypass/duplicate-credit exploit was confirmed. Exact remaining qualifications are both-player reconnect liveness, uncertain journal/player-save recovery, loaded-dead lesson time and source-to-native packet behavior in actual multiplayer; no disabled feature was enabled.
