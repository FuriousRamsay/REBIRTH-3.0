# Survivor local profile store audit

2026-10-15 audit-only current REBIRTH3.0. No production/build/tests/game/commands/git/shared report/ledger edits. Only this unique trace written. Operating directives and native/server creation architecture preserved; templates are user-local provenance, not authoritative world progress.

## Full read manifest

| File | Lines | SHA256 |
|---|---:|---|
| Scripts/Survivor/Persistence/RebirthSurvivorProfileStore.cs |532|AC822C63CF987C227204EA42684C71FE4284ADE2CD241B59D008A42745968798|
| Scripts/Survivor/Persistence/RebirthSurvivorProfileMigration.cs |73|54D8D21CFB21D7A6C5760D23D000E1C781523E9F8272BB85D6C8D49217AC187B|

Two newly full files605 lines. AtomicXml205 fully renewed read, already full SURVIVOR_REPOSITORY_AGENT_TRACE matching SHA256 4BFE0DA94812BC548B2AE56F7917146ACB468D005D749DEC4122271304DAB5BE: zero new credit. ProfileModels200 full DOMAIN_FINAL trace; current creator bodies other agent, targeted CreatorViewModel655–694 and caller searches here only. All negative searches rg -uuu, including ignored diagnostics.

## Active path and authority

Store.Initialize/Refresh force local catalogue load; userData/RebirthData/SurvivorProfiles/GUID-N.xml, no user profile name in path. ID parsing exactN prevents traversal. Profile catalogue local process lock; Get snapshot/TryGet return clones. UI current calls: CreatorViewModel.SaveLocalProfile calls TryCreate or fetches existing then WithSelections/TrySaveEdited, returns clone only on success. ChooseSurvivor/FirstEntry also TryCreate; manager1273 duplicate,1318 delete; actual refresh callers creator/profile/spawn UI. Ignored Debug console only reads named profile. No network request directly invokes local disk edit. Dedicated does not receive an owner-supplied disk path. Server creation still recomputes selections independent of profile authored values.

TryCreate validates name and current ValidateForCommit(require mode=false); TrySaveEdited independently reruns mechanical validator. That flag bypasses only current-world mode because menu reusable templates must work before a world. Rename/Duplicate preserve possibly mechanically outdated selection with structural checks; compatibility reevaluates before use. Valid current selections under changed semantic hash remain Ready; unknown definitions/point imbalance require review. Invalid template cannot directly overwrite earned world record.

## Save, cache and delete

All save/delete/refresh serialize through same Sync gate. Save ValidateStructure and requireExisting check happen before atomic writer; cache publishes clone only after writer success. Failed write retains prior catalogue value. New profiles get fresh GUID; existing update requires loaded ID; no public arbitrary GUID insertion method except Create-generated ID. Context root lookup on each operation uses current native user-data directory, not player-world identity; profiles intentionally reusable across saves.

Refresh snapshots previous catalogue/issues/loaded flag, clears and enumerates top-directory *.xml plus *.xml.bak base paths, ignores tmp as authority. Whole enumeration exception restores previous state; individual bad file records issue and other files continue. .bak-only is considered. Filename mustGUID and match internal profileID before cache publication. Duplicate IDs refused. If all recovery paths fail, failed file remains physically untouched and absent catalogue; user can still create unrelated profiles.

Delete explicitly deletes final,backup,tmp in sequence then removes cache. A later delete exception can leave earlier deletion committed while cache remains. No transactional all-or-none delete guarantee; next refresh reflects remaining source. This is user-command local template deletion, no authority item loss. No physical execution performed.

Atomic writer: per-normalized-path locks; same-directory temp, full XML write+Flush(true), reopens exact temp for structural parse, File.Move if no final, File.Replace(temp,final,backup) preferred. Unsupported/IO fallback deletes old backup, copies current final to backup, deletes final, moves temp; attempts restore on move failure. Candidate is syntax-validated but primitive does not know semantic source validity or supported format. Existing repository trace owns broader failure-preservation findings. No new FS atomicity/disk durability claims beyond these operations.

## Confirmed format preservation defect: future primary downgraded from older backup

ProfileMigration.TryMigrateToCurrent rejects primary schema>current1 with explicit newer-than-supported reason. ProfileStore.LoadOne does not distinguish this from corruption/missing file. If current-schema .bak validates, it publishes that old profile to cache and automatically rewrites final via Serialize(profile) because usedBackup=true. Therefore an intact newer-format primary can be replaced by old-schema backup simply by Refresh/load in this build. Trigger requires newer schema file plus valid older backup matching GUID; source-confirmed, runtime not run.

Preferred File.Replace initially retains the displaced future primary as .bak, so do not claim immediate irrecoverable erasure. A subsequent ordinary successful save/rename/update of now-loaded schema1 record replaces that backup, allowing future-only data to disappear from both files. Portable writer also replaces backup with displaced current primary before publishing old record; same eventual sequence. This is user-local reusable template compatibility risk, not world-character migration or network ownership exploit. Existing HUD preference store separately refuses newer schema without backup downgrade, establishing that this distinction is already implemented elsewhere. No redesign or production fix proposed in this audit.

Other backup repair custody limitation: validated backup published in memory before automatic repair writer succeeds; failure records issue but profile remains available. Subsequent edits retry as ordinary save. A corrupt existing final can replace validated backup through shared writer before eventual publication failure; shared MP036/AtomicXml family should be integrated by parent, not new duplicate finding. No claim that every failed recovery overwrites files: when both fail, LoadOne returns before writing.

## XML shape and migration

Root exact rebirthSurvivorProfile and integer schema. Registry steps explicit, unique fromVersion>=1 registrations; future rejects; old version needs exact registered step; each step must advance schema. Current1 has no old step required. No registration callers found in current rg -uuu source; unsupported old0 fails. Delegate throwing is not caught inside registry but caller load validation context may surface through broader refresh exception restoring prior catalogue. Migration document is detached disk load; no live profile mutated.

Deserialize reads first traits/creationChoices child; trait IDs preserved to structural duplicate/empty check; choice dictionary case-insensitive last duplicate key wins. Unknown attrs/children ignored. Dates parse roundtrip invariant and normalizeUTC through immutable model. ValidateStructure exactschema1, GUIDN/name1..64 without controls, nonempty hash/version/background/diet, distinctnonempty traits, choices<=32/key<=96/value<=256. Trait count/ID length and definition strings not capped at file parse; native local XML size/AtomicXml load has no explicit document bound. Local malformed-input resource risk only; no remote file write demonstrated. Choice duplicate normalization is accepted last-wins semantic, no earned grant.

Serialize fixed schema attribute order, sorted choices, preserved traits, UTC O dates; XML escaping via XAttribute. No native item payload/health/XP/skill values stored. Name/definition fields cannot inject XML structure through this writer. Current model constructor normalizes ID/hash/name while store enforces ID-filename equality. Unsupported future policy can be weakened only by backup fallback above, not by migration registry accepting future.

## Remaining scope

No other active ownership defect confirmed. SOURCE read qualification only, no build/runtime/gameplay tests. SURVIVOR_DOMAIN_PENDING_CANDIDATES.csv is candidate inventory only (135 rows at generation; pending ledger plus no filename occurrence in TRACE documents), not canonical unreviewed proof. Concurrent manifest integration may already cover candidates. Largest outstanding non-UI candidates at scan: Console1412/Diagnostics694/TestHarness567, StatisticsService544/Models340/Repository298/Network168, WildAffinity526, ResourceField447, TraitGameplay424, RecipeCatalogue391; parent must reconcile owners/current hashes. This tranche closes ProfileStore532 and Migration73 and excludes all repeated files from new coverage count.
