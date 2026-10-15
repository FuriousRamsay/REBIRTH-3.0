# Current project inventory reconciliation and eight small source reads

2026-10-15 audit only. No build, test, game, endpoint, profiler, native command or production edit. SeatPoseEditor external work was read without modification. Only the requested reconciliation CSV and this unique trace were written; main coverage ledger/report unchanged.

## Inventory method and limits

PROJECT_REMAINING_SOURCE_RECONCILIATION.csv records 1,805 current Scripts C# source files after unconditional RebirthUtils.csproj Compile Remove patterns. SDK defaults include sources; no Compile Include or explicit import/EnableDefaultCompileItems override was found. Current Debug source inventory is the union before evaluating file-local preprocessor branches; it is not a compiler output or DLL inventory. Release Debug-directory removal is separately labeled. Non-Debug conditional declarations, .Release.cs files and feature symbols still require their individual branch evidence. This static glob selection did not evaluate MSBuild or execute a build.

Compared the current SHA-256 against COMPILED_SOURCE_COVERAGE and canonical NPC/Survivor/Crafting/Purge/UI source manifests plus GameBridge manifest. Parsed explicit full-read/hash entries in all *TRACE.md and manually qualified compressed/full-current-body tables. Filename/hash matching identifies candidate manifest evidence; where a manifest abbreviates directory prefixes, exact current hash plus subsystem context supplies the disambiguation. Hash mention alone and plain source reference are kept separate, never credited as full reads. Old prose evidence without current hash is retained for manual qualification rather than called fresh untouched source. These inventory classifications do not certify any runtime test, successful install or native behavior.

Final classifications in this snapshot:

| Status | Count | Meaning |
|---|---:|---|
| CANONICAL_FULL_READ | 1011 | Current hash matches canonical source manifest; inherit its source/runtime limits. |
| TRACE_FULL_READ | 490 | Current hash and explicit full-read manifest/table match, including manually qualified formats. |
| LEDGER_FULL_READ | 111 | Current hash matches existing full-source ledger claim; not a new re-read. |
| NEW_LITERAL_FULL_READ | 8 | Current tranche read every source line, hash unchanged on recheck. |
| SEMANTIC_FULL_ONLY | 17 | Grouped registry semantic evidence; not relabeled literal complete display. |
| HASH_CHANGED_REVIEW_REQUIRED | 9 | Current source differs from ledger; previous coverage does not certify new body. |
| HASH_REFERENCE_SCOPE_REVIEW_REQUIRED | 7 | Hash/source referenced but recognized whole-read scope unclosed; parent-owned energy evidence. |
| TRACE_REFERENCE_SCOPE_REVIEW_REQUIRED | 8 | Older prose/partial reference requires exact scope/hash qualification. |
| NO_EXPLICIT_FULL_READ_FOUND | 144 | No qualifying evidence recognized; select for manual reconciliation/read, not an assertion nobody ever inspected it. |

Nine changed snapshots are camper EnergyAccess/DeviceAdapter/DrivingInput/Runtime/SnapshotWire/WorldLighting/XUiC_Energy and StationAccess/StationNativeActorPayment. Parent owns these; no duplicate reads or edits attempted. Seven hash-reference rows are camper energy helpers in NATIVE_TRANSPORT. Eight source-reference rows include AdvancedFarming console, Fire console, Healing ItemAction, Trees and four UI dependencies; older traces already describe source/native work, so do not treat these as proven virgin files. CSV retains precise reason/evidence and current/ledger hashes for parent verification.

Semantic registry count excludes the explicit FULL BuildVerify and NetPersist callers in FEATURES_REGISTRIES. That trace's grouped table review applies to its semantic registry entries, not every file it mentions. Earlier literal FirstSliceNoGo full evidence takes priority over its repeated semantic entry. QuickStack smaller-source 'Full current bodies' table and Medical 'all eight fully read' table were manually resolved as actual full-read evidence, preventing duplicate coverage work.

## New genuine source scope and runtime entry

Eight previously unmanifested small sources read in full: seven SeatPoseEditor sources (732 lines) plus obsolete compatibility resolver marker14, total746. Seat editor has no #if DEBUG in these files but its entire Scripts/Vehicles/Debug directory is physically omitted from Release. It is not auto-installed: console rbseatpose creates the session; `rg -uuu` found no external SeatPoseSession/ProfileStore/Rig/Panel/Camera/Bones references outside its directory. No new native method bodies were read, so native attachment/pose/sync effects remain qualified native API boundaries.

Console entry explicitly refuses dedicated server, RemoteClientInfo sender, remote World, absent/dead local primary player, invalid/dead vehicle, moving vehicle (>0.01 squared speed), >10m distance and locked unauthorized access. Permission uses native lock/access/owner identifiers. Seat index is bounded, occupied other-player seats refused and attachment uses native calls. It then SendSyncData(1) and creates local authoring component. Unlike previous read-only report wrappers, it really changes local attachment/pose, camera, input, animation speed and rig state. It is opt-in Debug authoring with genuine native effects, not a no-op or production vehicle-definition writer. The command does not override IsExecuteOnClient; native default routing must be preserved/qualified separately. No custom network authority/receipt or remotely editable JSON protocol exists.

## Session lifetime and restoration

Open captures input, cursor, first-person, model transform, native pose and affected overlay/animation state. Generic rig constraints and four limb mappings are validated; duplicate/missing native bone mappings can reject setup. Setup exceptions call Close; each saved rig/bone/camera restore has per-action cleanup logging. Session validates player/vehicle liveness, same attachment+slot and current World==player.world on Update/LateUpdate, closing when invalid. It removes additive offsets before native evaluation and reapplies after; previous applied quaternion products are tracked and inverted. Dispose restores constraint data/weights, rebuilds native rig, restores native IK enabled bool and destroys temporary targets. Camera restores original enabled bool and destroys the preview object. OnDestroy calls idempotent Close.

Close restores model transform and pose only if player still occupies the captured vehicle slot; it does not force a disconnected/detached player back into an old seat. Input/first-person/cursor and overlays restore captured values. This is session-local restoration, not a persistent-world rollback/receipt. Exceptions are logged and cleanup continues, but swallowed native rebuild failures are not proof of successful restoration. Native rig/camera setter failure timing was not injected.

Initial parked/access checks exist only in the command before Open. Session.Valid checks world, health and attachment, not speed/current permission; seat-change closure invokes Open directly after its occupied check rather than rerunning original command admission. Thus the authoring session can outlive initial parked/access conditions if another actor/system changes them. It writes pose each LateUpdate while valid. This is a qualified Debug authoring admission limit, not a demonstrated remote exploit; no concurrent driver or native behavior was executed. Public authoring DTOs are not a network permission system.

## Profile storage and validation

Schema1 and vehicle-model-local frame are required. Validate rejects missing identity/arrays, duplicate target/bone paths, nonfinite vectors, weights outside0..1 and finger controls outside-1..1. Finite anchor offsets and basePose have no upper bound; native pose IDs are passed through without native supported-ID lookup. UI numeric input explicitly excludes NaN/Infinity; preview Change serializes before mutation and attempts to restore/reapply previous profile on exception. Undo history stores JSON snapshots capped100. Each rig retains temporary targets for profile names already seen; local authoring profiles are trusted, no network exposure claimed.

Save validates name against Windows invalid filename characters and dot/dotdot, validates/serializes profile before touching existing file, writes unique temporary JSON, parses readback, then File.Replace with .bak or File.Move. Finally deletes surviving temp. Local persistentDataPath/REBIRTH/SeatPoseProfiles (or Exports) is the command-selected destination. No production XML/vehicle definition is written. Load reads selected JSON, validates schema and checks vehicle/rig/seat identity before Apply. It has no backup/temp recovery selection, byte/count quota or automatic migration; an invalid local authoring file reports error and leaves prior profile through Change handling. Backup is for file preservation, not proof of successful future load. No native/world save custody is attached to this JSON store.

Compatibility/RebirthBlockActivationMethodResolver is an empty obsolete(error=true) static marker retained for overlay replacement of old reflection source. No executable behavior and no external symbol references were found under `rg -uuu`. It neither discovers activation methods nor installs patches. No gameplay defect established.

## Next dependencies

CSV selection is inventory evidence only. Parent should reconcile current camper/native source additions and changed hashes with concurrent external work before scheduling reads; reserve genuinely uncovered large bodies rather than reread older full traces on a filename-only parser miss. The eight new full reads do not make the remaining144 automatically accurate whole-project pending obligations. No multiplayer runtime acceptance, clean build, profile export or restoration test is claimed.

## Literal full-read manifest

| Source | Lines | SHA-256 |
|---|---:|---|
| Scripts/Compatibility/RebirthBlockActivationMethodResolver.cs | 14 | 1C80EBD602B41ECB18C299D89AAC6A070338AD864EFBC9EFBF379C1035AB0B99 |
| Scripts/Vehicles/Debug/SeatPoseEditor/ConsoleCmdSeatPoseEditor.cs | 64 | 433DF9CB581B0CCE69D5164EEA9C981CA45468245C65B1906C7138757006B6DF |
| Scripts/Vehicles/Debug/SeatPoseEditor/SeatPoseBones.cs | 84 | FCD9A453EC8477901B981CCAF45BE95C6AB41D9A73AE69E8E1A343F00817B2D1 |
| Scripts/Vehicles/Debug/SeatPoseEditor/SeatPoseCamera.cs | 65 | 5FBBC738FDBC77786B0122BF7365849758AF1DA68CED77C2D2B2BBF1F37B0579 |
| Scripts/Vehicles/Debug/SeatPoseEditor/SeatPosePanel.cs | 175 | BDA8E2D117B79ADE464BED03AA9479A38BADD56BF38766475386CF8911F220DE |
| Scripts/Vehicles/Debug/SeatPoseEditor/SeatPoseProfile.cs | 122 | A1407B599CD1D1CB63CC678BCEC99A373B88546F0C8EB200C23FD37D389686FD |
| Scripts/Vehicles/Debug/SeatPoseEditor/SeatPoseRig.cs | 84 | 82159977FCE529CBE310423A8959C1C667FD14839D202CEE895E9D22891988F1 |
| Scripts/Vehicles/Debug/SeatPoseEditor/SeatPoseSession.cs | 138 | 3BBCCC66348053337F4972F6EE711A50B8822440C26F3EDC796EC0CA456E0394 |
