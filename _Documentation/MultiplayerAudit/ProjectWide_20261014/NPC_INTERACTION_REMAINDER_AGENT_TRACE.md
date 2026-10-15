# NPC interaction/capability remainder — 2026-10-15

Audit only, scope reported before reads. All requested Interaction folder sources were already full-read in prior social/trade/active traces, so this tranche owns previously partial AdminSupport495, DEBUG IdentityAdmin100, ApiCompatibility30, BaseGameAdapters95, ThirdPartyCapabilities71, CompatibilityQualification152, LifecycleGameplay226 and DogDialogCompat134. Full-source hashes below; Config/dialogs.xml99–124, DogLifecycle1734–1800, lifecycle/coordinator/csproj selective callers only. No production/build/tests/game/Git/shared ledger changes.

## Confirmed local recovery limitations

AdminSupport.RecoverFromBackup198–243 reports 'Validated backup restored' after copying an allow-listed .bak to staging, validating, preserving current primary and DurableFileCommit. ValidateRecoveryCandidate245–266 only requires nonempty parseable XML with a root; aggregate additionally checks supported format integer but not root identity or records. It does not invoke each store's semantic validator. Therefore a nonempty wrong-root/semantically invalid .bak can replace its primary and be reported recovered. Local Repair permission confines this action to server console, so this is an administrative recovery defect, not an arbitrary remote write.

Additionally RecoverFromBackup neither resets/reloads service caches nor calls PersistenceFile.VerifiedRead. A previously loaded store keeps its pre-recovery authority; its next save can overwrite the recovered file from old memory. If the path is already write-blocked, a still-loaded store can remain blocked despite the success message. A not-loaded store may later validate/reload and clear the block, so do not claim recovery never works. No instruction to restart is included in the success text. Correct recovery must respect both disk validation and current loaded/cache state; no repair was executed here.

ValidateSave187–195 considers absent files valid and otherwise checks only positive length/hash existence, not XML, domain consistency or schema. PASS is file-readability diagnostics, not semantic save qualification. Report/export paths can initialize services/imports through called getters; they are not universally side-effect-free just because the command permission class is Inspect.

## DEBUG identity caller correction and permission boundary

Full ConsoleCmdRebirthNpcIdentityAdmin100 proves IdentityAuditLedger.Append resolve/forget callers under #if DEBUG. Earlier negative search in NPC_PERSISTENCE_REMAINDER_AGENT_TRACE omitted ignored Debug paths; an explicit correction addendum was appended to that unique trace. All subsequent negative caller searches here include -uuu where needed. Release csproj39–41 physically excludes Scripts/**/Debug/** and non-Debug configuration; DEBUG command availability remains conditional, not assumed in every shipped DLL.

Identity command IsExecuteOnClient=false; resolve/forget services require server and matching class/profile, with pending-only forget. This command has no custom Authorize Repair/local-only check; ResolveActor only records remote-entity identity. Native console permission dispatch must be traced before deciding remote admin access. Do not present the consolidated rbnpcadmin's local-only policy as applying to this separate DEBUG command. The existing audit unexpected-world cache omission remains source-confirmed regardless of Append because coordinator checkpoint is active.

Consolidated AdminSupport full read confirms Repair for recover/progressionimport/reset/migration26, Export for file exports and Inspect otherwise. RemoteClientInfo nonnull denies Repair/Export; native console gate remains outer policy. AdminAuditLedger is bounded512 process memory; snapshot copies the array but exposes mutable entry objects, so callers could alter diagnostics if internal API misused. No production mutating snapshot consumer found. It is distinct from durable IdentityAuditLedger.

## Actual dog XML interaction route

Config/dialogs.xml105–121 installs FuriousRamsayCompanion responses using the fully read DogDialogCompat classes: owner/lifecycle/pickup/order/inventory visibility predicates and local action dispatch. Follow/Stay/Guard/respawn/pickup use RebirthCompanionService.Request, so authenticated server command validation from previous companion trace remains authoritative. Inventory/stats/rename open companion UI. GuardHere is defined but omitted from start response list intentionally in comment, not a missing router accident.

DialogRequirementRebirthDogAdmin shows action for GodMode or DebugMenuEnabled, but TryAdminRemove authoritative service separately requires authoritative world and player.IsAdmin or GodMode plus distance. DialogAction adminremove calls that service directly locally, with no remote request. Hence a remote local debug UI can show Remove that returns false in a remote world; actual admin success is host/server-local only through this dialog. This is a route/visibility mismatch, not an ownership bypass. Service terminal removal mutates tombstone/owner/order/presence, removes native actor then saves; parent existing custody/lifecycle findings govern failure recovery. Do not duplicate a terminal-removal transaction claim without tracing remaining inventory disposition.

## Capability declarations and lifecycle helpers

ApiCompatibility is zero-count retained telemetry. CompatibilityQualification records seven string-named APIs as Resolved=true at initialization, always Informational, Audit(force) only refreshes timestamp. Its report honestly labels STRUCTURAL/runtimeResolved=NotRun; BaseGameAdapterRegistry descriptors derive IsAvailable from those declarations and are not actual native implementations or invocation proofs. No gameplay TryGet consumer found; do not equate these with installed adapters. ThirdPartyCapabilityRegistry copies strings/version/server-safe flag, requires major1/serverSafe, retains no callable provider behavior. No TryRegister/TryGet production user found. Whitespace normalization/duplicate edge cases are dormant API validation concerns, not multiplayer mutations.

LifecycleGameplay contains recall/vehicle/mission/contract/respawn methods but no world/economy/placement implementations or assignments and no external action callers/Tick caller found. Coordinator Load522/Save582 and lifecycle initialization/reset do run persistence, so serializer coverage is active independently. It exposes mutable Snapshot records, accepts weak schema/defaults/nonfinite vectors and has no authority guard internally. PendingReplay is added in memory but not durably saved before external effects despite comment; adapter false can cancel uncertain partial effects; mission outcome uses process string GetHashCode. Those are future activation/release obligations, not active economy duplication while adapters/action callers are absent. Existing companion recall/respawn uses separate installed services, not proof these helpers execute.

No new UI/native runtime test performed. Remaining native console authentication/permission policy, active dog inventory/removal custody and loaded-store recovery coordination require parent closure. Source-only findings/limitations must not be marked gameplay PASS.

## Full-read manifest
- `Scripts/NPC/Administration/RebirthNpcAdministrationSupport.cs` — 495 lines; SHA256 `4720B2B4021228E60BED2C1A3146A826FC9D73957C7CDF07686441A0D3F8BDCA` (full read).
- `Scripts/NPC/Debug/ConsoleCmdRebirthNpcIdentityAdmin.cs` — 100 lines; SHA256 `0E3A2E34A6194A817792B42F6F9EE5B6A143CC27C099384160D9D5E24DC62D36` (full read).
- `Scripts/NPC/Compatibility/RebirthNpcApiCompatibility.cs` — 30 lines; SHA256 `538203C0F96933AB30ED14C547E51ED9F3965993457C482EB18FFA2DA9F99B19` (full read).
- `Scripts/NPC/Compatibility/RebirthNpcBaseGameAdapters.cs` — 95 lines; SHA256 `C4E822B1783DA357B0AF6A3DC6C6156CCE663FE67329A471D447A75EA9C64787` (full read).
- `Scripts/NPC/Compatibility/RebirthNpcThirdPartyCapabilities.cs` — 71 lines; SHA256 `44357111E691F2C9D49B1B15F6DE73F65AAF59A30648A006BF4250F733E1FFFF` (full read).
- `Scripts/NPC/Compatibility/RebirthNpcCompatibilityQualification.cs` — 152 lines; SHA256 `A19EA4491ED3AB7E57D019C734AC6E92D3200839C4BB8DA7FF72FC225E5F85B6` (full read).
- `Scripts/NPC/LifecycleGameplay/RebirthNpcLifecycleGameplaySystems.cs` — 226 lines; SHA256 `A447F510839DC858C913C7919EB4160785208A4D15955343D48279C70CFF5EB1` (full read).
- `Scripts/NPC/Dog/RebirthDogDialogCompat.cs` — 134 lines; SHA256 `CAD757FC39045B500B8E72501227A7402D5B46DCEA2B5AC8B561B6F7B4668717` (full read).
