# NPC structural work remainder — 2026-10-15

Audit-only exact tranche reported before reads: StructuralAdmission80, StructuralWorkJournal99, ConstructionWorkPackage468, MiningWorkPackage540. Full hashes below. Every negative caller search used `rg -uuu` including Debug folders; source declarations/callers distinguished. No production/build/tests/game/Git/shared ledger/report changes. WorkReleaseGate remains false.

## Current release reachability

ConcreteWorkInfrastructure.Register210 returns before construction/mining definitions/adapters/executors when gatefalse; Tick likewise returns. Scripts -uuu searches find no external Plan/CreateAndStart call for either package. References outside these files are held Register/Tick/reset, production-resource diagnostic snapshots and DEBUG ConsoleCmdRebirthNpc reports. DEBUG command folders are excluded in non-Debug csproj configuration. Neither actual block RPC nor settlement debit/credit in these adapters is current ordinary gameplay reachability. No network handler directly creates these packages found. Do not classify dormant material/yield gaps below as active resource exploits.

StructuralAdmissionResult.CanAuthorizeStroke is always false and missing proof All; StructuralEvidence.IsQualifiedProof=false, Journal.RequiresReconciliation=true and CanAuthorizeStroke=false. The journal has no file/store/scheduler/installer or qualified producer. Its only references are its own definitions; identity text helper is reused by EmbodiedWorkIntent (not journal adoption). No mutation path consults this envelope to grant authority. This is intentionally inactive contracts, not a safety engine silently approving construction.

## Full immutable identity/evidence contracts

OperationIdentity binds64lowercasehex save fingerprint, stable NPC/profile/generation, assignment+revision, optional command, nonempty operationGUID and target. Canonical parsers and exact XML shape reject unsupported attrs/numeric representations. Evidence copies bytes <=64KiB, binds original operation, exact producer/schema/version and canonical base64. Journal caps5 unique evidence kinds and256KiB aggregate, sorts evidence, validates exact matching original identities and returns cloned arrays. Missing optional journal is readable but no proof. Phase Settled is only a producer claim and cannot release items/credit work. No original durable native witness or native structural scheduler exclusion is produced by these files.

## Construction package/adapter held limitations

Plan bounds4096input/512assignments, requires nonremote world/settlement/known material, deduplicates positions, filters placement/repair/upgrade and sorts y ascending then action/coordinate. It calls this support-sensitive order but performs no structural after-state closure, stability/scheduler exclusion, loaded native chunk proof, escape, land/claim access, tool action or definition-derived cost. Supplied material/quantity/target block are trusted internal request contracts. Undefined enum values flow through generic upgrade planning then Definition defaults placement; target parser Enum.TryParse likewise lacks IsDefined. This is not packet input today.

Create makes ordinary session assignments and process-local sequential package queue; StopOnFailure cancels later assignments but missing assignment rows are skipped. Package grouping/index not serialized here, so durable assignments alone do not prove cold sequential restoration. Adapter Begin validates coarse block state and supply availability; tick revalidates, reserves/commits settlement debit, then directly SetBlockRPC to repaired/placed/upgraded BlockValue. No native held-tool stroke or full item custody action is executed. On exception/failed retained postimage it refunds supply, even if a native side effect happened before throw or replacement occurred after placement. It does not force old world state over a later mutation, which preserves other actor state but requires original outcome custody to settle ambiguity. Postimage checks type/damage or type/rotation only, not full metadata/incarnation. Committed flag is process-only and per adapter instance; successful retry after reload needs controller/journal evidence elsewhere.

Repair clears all damage for request-declared cost; upgrade can replace any existing different type with request-declared target instead of deriving native upgrade path/materials. This is dormant policy gap, not ordinary owner permission bypass demonstrated. Settlement string is not encoded in construction target builder while block/item strings are colon escaped, so settlement IDs containing colon can parse differently. No actual invalid settlement ID demonstrated.

## Mining package/adapter held limitations

Plan validates expected block type/known output and positive quantity, clamps output100000/ticks600, orders priority then y descending/coordinates. It does not derive native harvest yield/cost/tool durability from target definition. Adapter Begin saves only BlockValue original, checks type; subsequent ticks validate same type, not incarnation/damage/metadata. At completion directly writes Air via SetBlockRPC, checks Air, then deposits declared settlement output. No block damage/tool hit/loot/native harvest call occurs in this implementation.

Output credit failure restores saved original only if current position remains Air; it avoids overwriting nonAir later ownership, but old saved BlockValue omits tile entity inventory/state and can be stale after same-type changes. Restoration checks type/rotation only and can restore old damage. A source-removal exception after native mutation reports failed with no exact original outcome receipt. Successful credit then exception before operation.Committed is also not durably journaled. These are future activation conservation obligations. Current held registration/no caller prevents an active item dup/loss claim.

Repeated call after in-memory Committed returns Completed with produced output dictionary again; outer executor must use its own committed/outcome identity rather than crediting dictionary afresh. Package service does not itself publish a reward, so don't infer a duplicate from this DTO alone. No disk format migration/native reconstruction provided by either package. Authority world checks reject remote world; no separate current save/embodiment binding survives process-only operations, and future dispatcher must supply those original bindings.

## Conclusion and next dependencies

Four remaining whole sources are now read; no new active gameplay finding promoted. Held block adapters remain unsuitable for release until native structural/action/access/custody/durable-outcome contracts are installed and proven. The inactive proof envelope correctly refuses authority but is not integrated into these legacy mutations. Parent-owned WorkController, assignment persistence, settlement endpoint and native SetBlockRPC/structural callbacks govern remaining audit dependencies. No runtime fault injection/native world test executed, and no feature enabled.

## Full-read manifest
- `Scripts/NPC/Work/RebirthNpcStructuralAdmission.cs` — 80 lines; SHA256 `156D0026D3BC0DB0C0A80ED6D04C69FBCD6FA94FBC25F4F691AAC838D0EC0E3A` (full read).
- `Scripts/NPC/Work/RebirthNpcStructuralWorkJournal.cs` — 99 lines; SHA256 `077BA3B30DA4A0409F14EDA86F2B778A2594E8595D2BB9341C18DF05E244FD3C` (full read).
- `Scripts/NPC/Work/RebirthNpcConstructionWorkPackageService.cs` — 468 lines; SHA256 `367C54423E64D35A2D89788157273599761B26C9F7CF73D9E4F35C9D873D5F6A` (full read).
- `Scripts/NPC/Work/RebirthNpcMiningWorkPackageService.cs` — 540 lines; SHA256 `092BD4EC930F271D1290859380DD679F61A164AB8ADCC8E0505637DDD77CA41A` (full read).
