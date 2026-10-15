# Survivor domain final models and presentation store

2026-10-15 source audit of current REBIRTH3.0. No production/build/test/game/commands/git/shared ledger/report edits. Project directives and accepted server creation/native owner architecture preserved. Only this evidence document added. Searches include rg -uuu.

## Full source manifest

| File | Lines | SHA256 |
|---|---:|---|
| Scripts/Survivor/Persistence/RebirthSurvivorProfileModels.cs |200|BF12E25BC9CB64CD59487F1A8A88174550E614FAC38B7FB645E6D07705CB94A2|
| Scripts/Survivor/HudTracking/RebirthHudTrackingModels.cs |262|D65A38FB34E49461671D0B25D022D87B6C9F9E1CB4DCC1AD24DD0E2DAD79AFD4|
| Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs |29|6271DB1D3BB17C852EB69199083DCB5AC3ADE0D7716C9557DD5C578B75D8BEF7|
| Scripts/Survivor/HudTracking/RebirthHudTrackingPreferenceStore.cs |261|89F879CC761C0B379BC4412300513D9F185B26C35204E0B4EFAE2927C6B99338|

Four complete bodies752 lines physically read. CANONICAL CREDIT CORRECTION: parent assigned PreferenceStore261 to tradeagent concurrently; store read here is repeated integration evidence only and NOT new canonical coverage. This tranche contributes only three files491 lines (ProfileModels200, HudModels262, RequestScope29). Prior UI_SANDBOX_REMAINDER/UI_HUD_NAV explicitly PARTIAL store; tradeagent now owns final full credit/qualification. No prior model trace manifest hit. RequestScope extensively referenced in prior traces but no explicit full-read credit hit; parent reconcile. ProfileStore only compatibility194–244 targeted; HUDPreferenceService180–end targeted. No whole-file credit for those.

Reused existing full manifests: DefinitionModels289 and DefinitionVersion94 from SURVIVOR_DEFINITIONS_AGENT_TRACE; PersistenceLifecycle115 from METABOLISM/PARTY_IDENTITY/LITERATURE traces (SHA75B6657953A510A56023B7B5EE466261AD5301CA43FA5B0792D1EBF993E74840); StablePlayerIdentity147 PARTY_IDENTITY trace (D80E82E2CAF081B1EB0B4DBA50D52F9E5383FD18772CBCEB9B010C245B53A3F3). DefinitionVersion and lifecycle were renewed reads, zero new credit. No unreviewed clock helper was inferred from naming.

## Profile value ownership

Profile schema1 is local reusable starting template; no earned progress values. Constructor copies traits and choices into read-only collections, normalizes profileID/hash lowercase, trims names/IDs, UTC dates. New/Duplicate mint GUID N IDs; Rename preserves original ID/authored definition/schema/created time and refreshes modified UTC. WithSelections upgrades to current schema/hash and current selection, preserves ID/created UTC. Clone preserves values with detached collection copies. No public mutable backing arrays exposed. Null inputs become empty; validity is not model permission.

Compatibility result copies errors and exposes Ready/NeedsReview/MissingDefinition/InvalidCombination/UnsupportedSchema. CanBeReviewed excludes unsupported schema; IsUsableWithoutReview only Ready. Actual ProfileStore.EvaluateCompatibility requires exact current schema and reruns current ValidateForCommit rather than trusting authored hash. Hash change alone with valid selections remains Ready, editing naturally refreshes hash. Missing definitions and changed point balance classified separately. Server creation revalidates its own request/result from prior Creation/Network trace; local profile model cannot restore authoritative world progress. Full ProfileStore disk publication/repair is another agent's scope, not proven by model reads.

## Creation identity helper

RequestScope accepts nonempty parsed GUID normalized N or exact legacy- plus64 lowercase hex (71chars). Matches GUID forms by parsed value; legacy identity only complete ordinal string. Empty GUID/malformed/uppercase legacy hash rejected. It never truncates old71char identity into GUID. Existing owner scalar/medical/gear/teaching callers use exact character identity comparison. It is identity syntax/equality, not ownership authorization by itself; caller must still bind sender/world/player/record. No migration-origin mismatch demonstrated.

## HUD models and actual persisted context

HUD identities are type+case-insensitive stableID, trim only; values are presentation IDs, not progression values. Typed tokens Skill/Attribute/Knowledge/Summary explicit; unknown tokens retained in distinct unknown-row model. Clones deep-copy mutable rows/lists. Add refuses empty/duplicate typed identity; Remove and Move normalize ordering. Combined ordering deterministically sorts numeric order then canonical key, retains unknown entries and compacts sequential integers. GridTarget restricts first15 entries to3columns×5rows; MoveGrid swaps numeric row order then normalizes. Unknown rows participate global order but not rendered known grid. No authoritative mutation from preference changes.

Integer overflow is possible in exposed Add nextOrder+1 or Move index+arbitrary delta for hand-constructed values; normal file read NormalizeOrder compacts and actual UI caller uses bounded grid deltas. No active overflow path proved. Unbounded row counts/string lengths in local XML can allocate/produce quadratic duplicate checks; this is local preference data, no network mutation/remote payload source found. Do not promote as server packet DoS.

PreferenceStore resolves stable local platform storageID and hashes normalized native GetSaveGameDir path for WorldKey; output is userData/RebirthData/HudTracking/worldKey/playerStorageKey.xml. Dedicated does not own these presentation choices. Native world path lowercased/slash normalized. No server persistent GUID is present, so cross-server uniqueness depends on actual native remote save path; no exact native two-server collision proved. Context is not save-owned gameplay authority.

Load absent final+backup -> fresh defaults. AtomicXml loader final/backup supports syntax recovery; semantic corruption separately attempts backup. Newer schema deliberately refuses older backup downgrade. TryParse accepts historical/missing/nonpositive schema with current defaults, rejects only newer, recognizes local root (case insensitive localname) and direct entry nodes, skips empties, ignores duplicate known rows, retains unknown rows. NormalizeOrder before publication. Save clones before normalization, writes schema1 through atomic XML helper. Public Serialize normalizes its supplied object, so it is not a pure nonmutating formatter; no harmful concurrent caller proved.

TryLoad returns defaults+false after unrecoverable/newer schema. Store TrySave alone does not enforce prior refusal: actual PreferenceService targeted180–end binds current world/player/platform, invalidates unavailable owner, loads context and sets writable=ok; refused disk stays nonwritable. Pending prior writes restore only exact Context.DebugKey after successful load, preserving failed-departure newer preferences. Earlier UI traces own remaining debounce/publication/runtime integration. Do not claim failed load automatically overwrites this store from low-level API alone.

## Result and remaining work

No new confirmed active gameplay/owner authority defect in this tranche. Full model/store semantics closed with scope qualifications; no runtime qualification. Canonical project-wide Survivor full-read manifest remains parent integration task because multiple agents own concurrent source manifests; this trace supplies exact unique rows rather than counting prior partial/hash references. ProfileStore/HUDPreferenceService whole bodies and native remote save-path uniqueness remain explicit external dependencies unless their owning agents have completed current hashes.
