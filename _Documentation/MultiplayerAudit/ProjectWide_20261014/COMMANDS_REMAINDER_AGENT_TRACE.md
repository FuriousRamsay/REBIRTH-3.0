# Commands remainder and selected Debug wrappers

2026-10-15, current cumulative shared source; target operating REBIRTH3.0/b259. Audit-only; no production/build/tests/game/command execution/git/shared ledger/report edits. Existing COMMANDS_AGENT_TRACE.md and FEATURES_AGENT_TRACE.md were consulted. Sys previously partial is now full; no repeat full-source count for already covered NetPersist or HarvestSystems Debug command. All negative caller searches use rg -uuu.

## Full source and permission boundaries

Sys1614 and RadialIconExport558 fully read in bounded chunks. Five Debug wrappers fully read: CraftingUi51, CropHarvest138, Perf106, Build91, RuntimeScenarioPolicy77. Additional full Release harvest installer facade22 read. Existing exact native ConsoleCmdAbstract defaults and parent console-dispatch evidence reused: Sys IsExecuteOnClient=true/defaultpermission0; radial IsExecuteOnClient=true/permission1000/AllowedInMainMenu=false. Five Debug wrappers defaultpermission0 and client execution. These are native console surfaces, not independent unauthenticated RPCs; native permission configuration remains outer gate. No commands were invoked. Dedicated invocation lacks local UI for radial export; remote client executes on own client, listen host local state may also be authority state.

## Confirmed diagnostic contract mismatch, qualified by build

Sys description/help says read-only and explicitly says no patches installed/no scheduler callbacks. Most branches log registry/pipeline/planning reports. Some intentionally mutate diagnostic memory: buildselect/reset replace in-memory selection (selective implementation verified), shadow state changes/reset, runtime/harvest shell resets. GetDryRunReport only builds plan/report with shadow-state reads, not feature toggle. These do not change compiler symbols/project/build outputs.

Sys tail is not uniformly passive. harvestsystemsinstallall/installcrop, harvestsystemsenablestumpcontext, harvestsystemsenableattackobserve/bonus and rollback invoke RebirthHarvestSystemsManualPatchInstaller. Debug implementation explicitly installs attack/stump Harmony class processors; rollback UnpatchSelf clears manual policy flags/counters/stump context. Existing FEATURES_AGENT_TRACE.md owns whole installer/patch/replacement semantics; do not infer all named test modes actually deliver gameplay rewards. InstallCropPatch explicitly skips missing old native crop target and disables crop policy. Release facade (#if !DEBUG) returns disabled for every install/uninstall with no mutation. Thus blanket no-install text is inaccurate for actual documented Debug build, but release does not install those manual test hooks.

Sys crop enabletest writes production policy enabled/runtime/hotpath/manual override flags via EnableManualTest; values are clamped, reset clears flags. rg -uuu CanUseInPatch/PreviewAdjustedCount consumers do not establish an active native crop patch: located use is preview/report and absent-old-target installer, not ordinary harvest actuation. Do not present flag enablement as confirmed active client crop duplication. Ordinary shared advanced-farming gameplay is separate evidence.

Sys bootstrap report literal 'No Harmony patches installed' is stale text and does not inspect actual PatchClass count; current bootstrap explicitly installs feature hooks. Source report/registry strings are not native release or runtime acceptance proofs. No new unauthorized remote mutation found in this wrapper.

## Radial exporter actual file/GPU lifecycle

Catalog is static action/context metadata; entries named spawn/repair/pickup are labels and sprite references, not executable actions. list only logs. export/exportall require active World, primary EntityPlayerLocal, PlayerUI/XUi. Output root is GameIO.GetUserGameDataDir()/RebirthRadialIconExport, independent of save directory; timestamp yyyyMMdd-HHmmss produces folder and zip. Paths use fixed folders and sanitized catalog/runtime sprite names, no command-supplied path. Writes ByAction, UniqueSprites, SourceAtlases, optional AllUiGameSymbols PNGs, TSV and README then zip; existing same-second zip is deleted and folder reused. This can overwrite same-second prior export and retain partial output on exception; bounded local diagnostic issue, not world-save loss or arbitrary path traversal. No recursive deletion and no game save/inventory/world actor writes.

Atlas replacement follows max8 links, resolves exact sprite/material via reflected property/field and refuses missing atlas instead of substitution. Crop uses GPU blit/readback; temporary RenderTexture released and prior active RT restored in finally, returned texture destroyed by caller finally; failure destroys partial output then propagates to outer logging. Whole atlases exported once per instanceID and unique sprites per atlas/name; exportall walks stock sprite list. Asset dimensions/list size are not explicitly resource-capped here; trusted installed atlas input, explicit user diagnostic can be expensive. No export was executed or GPU work initiated by audit. Existing assets are not removed. No lifecycle handler/coroutine retained by exporter.

## Selected Debug source closure

All five wrappers are under Scripts/Debug plus #if DEBUG; Release csproj physically removes Debug directories. rbcraftui guides changes developer LayoutDebugEnabled and requests active UI layout audit; status requires open personal crafting. It does not enqueue craft/award inventory or write save. guides recognizes on/1/true else false, explicit developer action.

rbcropharvestfresh parallels Sys policy/reset/preview and crop installer; old crop hook unavailable, so help's install expectation is stale. No test executed. rbperffresh baseline/dryrun/sweep calls planning reports; selective GetDryRunReport confirms no toggle application. rbbuildfresh select/reset changes diagnostic selected profile only, not actual build. rbruntimepolicyfresh reset changes in-memory explicit-default policy cache, not game options/save/XML. Other service bodies remain parent feature/registry traces or selective dependencies; these wrapper reads do not grant all called services whole-file coverage.

## Remaining exact scope

Scripts/Commands is now full-read across COMMANDS_AGENT_TRACE.md, current trace, and hash-matched NetPersist prior trace. Scripts/Debug/Commands still contains many unreviewed wrappers plus SceneContentProfiler1171; only named five receive new full credit here. Earlier HarvestSystems363/BuildVerify90 prior FULL evidence not counted again. Scene profiler must receive full lifecycle/export/memory/render authority review, not scan-only credit.

No build/test/command/game run; no native asset rendering, file export or save inspected. No production fixes implemented. New confirmed finding is misleading read-only/install status text, conditional on Debug/native installer availability; it is not a newly proved multiplayer exploit.

## Full-read manifest
- FULL Scripts/Commands/ConsoleCmdRebirthSys.cs SHA256 5C7FC67C278C63A5813026D48494239381FE46B3B15B14ED836945CF6E93930F (1614 lines; full read).
- FULL Scripts/Commands/ConsoleCmdRebirthRadialIconExport.cs SHA256 695881C4956F7B2F81BE21E17B875C4A0718CB5C2B7C4EF5B506E0E0D75C20A5 (558 lines; full read).
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthCraftingUi.cs SHA256 115F233B6FED437DE732969F770995D5AB810F440E35F64456253440E05AD23B (51 lines; full read).
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthCropHarvestFresh.cs SHA256 47799F00051A426F7C2FC93016049D3F07799F4BDA7A1A9EE19C7FCEDE213021 (138 lines; full read).
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthPerfFresh.cs SHA256 5DB1302F6B47D808A4311DDC7F823571449E6400C17D32A8F1A46B55CE350858 (106 lines; full read).
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthBuildFresh.cs SHA256 44628A09C6F614802B14F7B2093492694F00869C639C59BF0E584CB088136F21 (91 lines; full read).
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthRuntimeScenarioPolicyFresh.cs SHA256 95DE2B945C5F3263E6C9B22F5DD6409DD8B5CA371048B21A5285D1C26B22DC10 (77 lines; full read).
- FULL Scripts/Features/Harvest/Patching/RebirthHarvestSystemsManualPatchInstaller.Release.cs SHA256 0C6132756CC2E6801BF2F18F7001ACC4A8111CFD1C1DDA7982132EB7CB0AF1CE (22 lines; full read).

REBIRTH FEATURE COMPLETION CONFIRMATION: assigned static tranche COMPLETE. Directives/evidence/accepted architecture/documentation PASS; production code/XML/UI/localization/assets/persistence/network changes N/A; packaging N/A. Compilation Not performed; in-game validation Not performed. Whole Debug subsystem still pending named remaining sources; no qualification/fix claimed.
