# Survivor remaining persistence/domain records — 2026-10-15

Audit only. Three FULL literal current C# source bodies, 789 lines. No production changes/build/tests/game/Git. Original pending CSV now has no further Domain/Persistence candidates after these three and the prior18 station records. This statement concerns that candidate list, not files added later or another agent's pending state.

## Local journal original data persistence

RebirthLocalJournalOriginalPersistence333 is a partial WorldCharacterRepository implementation. Its four witness-facing wrapper APIs have no callers outside their own file in rg -uuu Scripts. Therefore the exact-original local journal witness protocol is infrastructure, not an active native owner inventory settlement path. Its shared gate/selection/write helpers ARE production dependencies: Repository GetWriteLock1099, write/recovery operations812/1014, SaveIfDirty/SaveAll1045/1058 and selected save1075–1085. Reset44 replaces RepositoryEpoch while retaining stable path gates. No installer is needed for static partial methods.

Gates are keyed by canonical FINAL path, with Windows-insensitive/platform-sensitive comparison, excluding cross-world player-key alias. Each selection freezes epoch/generation/gate, original record/support and cloned snapshot. Same-thread reentry invalidates generation and refuses operation; other threads serialize on gate. An uncertain journal blocks unrelated gate operations. Live verification requires current authority/cache/original/support, unchanged complete serialized record and callbacks rechecked around detached comparator execution. TryWriteSelected changes generation under Sync before atomic write of a cloned private document. Existing repository callers and native gates are other full traces; they are not new full coverage here.

Witness Commit distinguishes denial before attempted write from uncertainty afterward. It retains exact snapshot before I/O; Retry can only reuse that same original and cannot turn stale refusal into proof the prior attempt did not happen. Verify reads final-only XML, requires exact document equality, validated nonmigrated owner/schema record, comparator nonmutation and trailing raw equality after callbacks, then marks persisted and clears pending witness only on success. These are DATA publication proofs, never native inventory authority or entitlement. No behavior is inferred for an uncalled API.

Retained stale-epoch inspection is read-only and nonblocking Monitor.TryEnter. It explicitly requires epoch mismatch with unchanged gate/generation/canonical path and retained witness. It distinguishes missing/different/invalid/blocked/faulted final files, double-reads exact XML, returns detached support only, and never clears pending custody or grants I3/native recovery. Raw XDocument load has no independent size budget in this method; no active caller is present, so no live remote-input vulnerability is claimed. Stable gate lifetime and reset revocation are explicit; no test execution or crash durability certification.

## Migration policy

SkillMigrationPolicy300 defines48 current IDs, sixteen explicit legacy mappings and signed[-50,100] runtime bounds. Broad bladed/blunt/handgun/rifle skills fan out to explicit destinations; unarmed additionally copies knuckles. Axes and unrelated new skills initialize neutral. Current explicit destination values override broad legacy mappings deterministically. Returned arrays are cloned; mapping objects expose private setters and read-only destinations.

TryBuildMigratedSkills consumes a skills XElement and produces a new node; source is not changed. It rejects blank/unknown/case-insensitive duplicate IDs, missing/nonfinite/out-of-range values, invalid progress outside[0,1). Progress defaults zero when absent, and is omitted entirely for origin mode. Output is complete deterministic CurrentIds order with invariant roundtrip float strings; missing skills become zero, and audit counts/mappings record what happened. It selects Elements("skill") rather than independently rejecting every other node/attribute; complete parent document schema validation remains the migration caller's responsibility, not an inferred guarantee of this helper.

Actual WorldCharacterMigration136/138 builds both origin and progression translations first, returns failure on either, requires registry ready, then replaces both nodes. Thus this helper does not partially mutate origin on bad progression. Other full migration/repository trace remains authority for repair/backup/save and later schema stages. Policy name schema4->5 describes that stage, not latest schema. Debug harness calls only inspect policy output; no vectors executed here.

## IDs and mode lifecycle

RebirthSurvivorIds156 includes enums, stable constants and SurvivorMode. Constants have no grants/network/save side effects; historical comments saying Rage planned are stale commentary, not runtime disable flags. Mode defaults Rebirth, reads configured int, state-specific IsEnabled uses supplied SandboxState, and ApplyFromSandbox coerces invalid enum to BaseGame then calls ForgeCraftingMode.Apply. Actual callers: Options/RebirthSandboxOptionManager844 and SurvivorInstaller87; this source does not independently infer server authority or broadcast changed sandbox state. Those callers' ownership remains prior evidence. No new gameplay defect established.

## FULL literal source hashes

All three bodies read completely. The initial combined output truncated; policy was fully reread as1–160/161–300, IDs all156, and journal omitted middle/tail recovered with111–193 and191–333. No source coverage from hashes alone.

```csv
"file","sha256","coverageMethod","lines"
"Scripts/Survivor/Persistence/RebirthLocalJournalOriginalPersistence.cs","E896581026ABC6686D33E699FE8557410B88EA7AE5D35398E35B7F3E6970403B","FULL literal source body read; no execution","333"
"Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs","E79FB04A92B15928EE0C25BBF0AE25E2DE72BB0D7E8CC48305721EFD9F047805","FULL literal source body read; no execution","300"
"Scripts/Survivor/Domain/RebirthSurvivorIds.cs","DEF87E5F6B8C0C7B824A1B99ABCCA22912E5FBD816821C1B86BFD3143813B20E","FULL literal source body read; no execution","156"
```
