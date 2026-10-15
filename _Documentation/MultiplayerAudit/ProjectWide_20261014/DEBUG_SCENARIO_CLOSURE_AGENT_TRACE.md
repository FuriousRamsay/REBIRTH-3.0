# Scenario closure and Debug report wrappers

2026-10-15 audit-only. Twelve literal whole-source reads, 1,984 lines. No build, game, tests, endpoint, profiling, script launch, production edit or shared report/ledger change. Reconciled existing COMMANDS_REMAINDER, DEBUG_PROFILER, DEBUG_PERF_SNAPSHOT, FEATURES and FEATURES_REGISTRIES manifests and preceding DIAGNOSTIC_SCENARIO_REMAINDER trace before scope selection.

## Literal full-read manifest

All paths relative to mod root; SHA-256 current at read/check time. Each source body, including declaration strings, comments and branches, was displayed without eliding initializer tables.

| Source | Lines | SHA-256 |
|---|---:|---|
| Scripts/Scenarios/RebirthScenarioSurfaceLedger.cs | 584 | 9B3173E4D616046420F2D30B5DA9800190F37C3597E33EF6ACB35C9E6436AA1C |
| Scripts/Scenarios/RebirthScenarioXmlOwnershipRegistry.cs | 530 | C17A1FD53CD61A1A217646AB0F077DA078C20BA2821B9FA7867782D3F1045F72 |
| Scripts/Debug/Commands/ConsoleCmdRebirthBuildProfileCompareFresh.cs | 83 | 130C3EEB51466F2C398B9621F2693165931FCC39324E26A6D79208D53F0B75D6 |
| Scripts/Debug/Commands/ConsoleCmdRebirthBuildProfileFresh.cs | 83 | 2A174405AC24B3AC414166EFDC1B5EDC1EFE9647F0F8C8C7662B3F9023D765A9 |
| Scripts/Debug/Commands/ConsoleCmdRebirthBuildScriptFilesFresh.cs | 71 | EB5C936125B87C637D15631D7D1811765F85ED2F4555673780FA6CECB809C002 |
| Scripts/Debug/Commands/ConsoleCmdRebirthBuildScriptFresh.cs | 86 | D846FC82CCE634D246AE870240CB56B54BF291EA78BEABE0D6E53EC50FD01B18 |
| Scripts/Debug/Commands/ConsoleCmdRebirthScenarioBindingsFresh.cs | 98 | 797E697B698824D9B1DC0DF0C703D5CC85C04573B53E9E77E64EE15B06572C3D |
| Scripts/Debug/Commands/ConsoleCmdRebirthScenarioLedgerFresh.cs | 93 | 2E155F9F3A570D776EB75A3E30F08CC1CA4AEC511227601AB5DF4F6D652BB008 |
| Scripts/Debug/Commands/ConsoleCmdRebirthScenarioManagersFresh.cs | 98 | 07A223E68F538BA7A51ADEE55FA9C8DF0D7CE4D71503F127677188831CF2605C |
| Scripts/Debug/Commands/ConsoleCmdRebirthScenarioPolicyFresh.cs | 92 | 7608BE8626D7F6BBE6AC0AA786A7B44C2911430AE4785731D49A38CD072B164B |
| Scripts/Debug/Commands/ConsoleCmdRebirthSourceAuditWorkspaceFresh.cs | 88 | BCA1B1ED0389355DCEEE06715EE241977F9988A3D9EC8EACA39254FBD601EF09 |
| Scripts/Debug/Commands/ConsoleCmdRebirthSourceIntakeFresh.cs | 78 | 442BA31240EA41170F49E992A7BAE523BEB99C41E06A8AB4532BF92104F193C3 |

## Registry behavior versus printed contracts

SurfaceLedger contains historical 2.6 surfaces and migration advice. All executable methods enumerate that static table, filter case-insensitively, count classifications and assemble strings. No source/XML inspection, scenario activation, native patch, scheduler registration, network dispatch, inventory or save mutation occurs. Printed SourcePath references to Harmony/Manager/XUIC are historical descriptors; csproj excludes those legacy directories. Reading this table is not full-read credit for those paths and does not prove the historical authority labels against current b259 native code.

GetSnapshot copies only the array: public mutable RebirthScenarioSurfaceDecl objects remain shared. No external GetSnapshot caller was found by `rg -uuu` across Scripts. This is an exposed-metadata integrity limitation, not a proved live multiplayer permission defect. Reports call table objects directly and do not mutate them. Exceptions from future callers modifying those objects were not injected or inferred as an existing production occurrence.

XmlOwnershipRegistry similarly contains only fixed records and report/filter/clone code. GetSnapshot deep-clones all declaration fields. Scenario and layer filters are substring matches, not exact authority selection. It neither reads XML nor applies/removes patches, ownership, cleanup, files or generator output. Strings such as server-authoritative spawning, final cleanup, manual review and C# should not generate XUi describe intended/historical contracts. They do not establish current gameplay behavior or impose a new user directive. Survivor token disambiguation is correctly represented as classification work rather than automatic migration. No durable mutation, receipt or reconnect state is authored by either registry.

## Actual callers, compilation and authority

`rg -uuu` caller search includes ignored Debug. Surface reports are called by Debug ScenarioLedger wrapper :54–83 and production ConsoleCmdRebirthSys :721–739. XML reports are called by production ConsoleCmdRebirthScenarioXmlFresh :53–88 and Sys :805–831. Both registry classes therefore have Release report reachability. No external snapshot consumer was found for either class. The production command bodies were already covered in COMMANDS_AGENT/COMMANDS_REMAINDER and receive no new full credit.

All ten newly read wrappers have #if DEBUG around the entire class, Preserve and IsExecuteOnClient=true. RebirthUtils.csproj:39–41 additionally physically excludes Scripts/**/Debug/** from non-Debug compilation. In the ordinary Debug configuration SDK DEBUG plus project REBIRTH_DEBUG are distinct symbols; these classes depend on DEBUG. Their native console registration/permissions remain the already qualified native ConsoleCmdAbstract/dispatcher boundary; they do not add a custom sender check or custom network packet. IsExecuteOnClient establishes client console routing, not server authority or certification of dedicated-server state. No wrapper accepts player/item/world mutation arguments.

Wrappers normalize the first argument, default to summary, validate required scenario/module operands and log report strings. Unknown arguments log help. Ledger/Bindings/Managers/Policy use the previously fully read registries; their authority/lifecycle/guardrails/blocked commands print planned constraints and do not execute them. No automatic Unity callback or retained per-world runner is installed. There is no new gameplay session state to persist/reset here.

SourceAuditWorkspace and SourceIntake call the existing FEATURES_REGISTRIES trace's **SEMANTIC-FULL** dependencies. That earlier trace explicitly elides repeated literal formatting while inspecting executable bodies and literal field values; this document does not relabel those as literal complete reads. Workspace does not inspect the named files; intake markdown logs a blank template, not a saved file or evidence acquisition. Counts and statuses are registry state, not results of newly executed audits.

BuildProfile/Comparison/Script/ScriptFiles wrappers call report APIs. Targeted dependency body reads (not whole-file credit) cover Scripts/BuildProfiles/RebirthBuildScriptFileRegistry:62–end, ScriptTemplateRegistry:194–296, ProfileComparisonReport:150–240 and ProfileRegistry:166–end. These assemble static strings/rows. Profile template can consult selected diagnostic profile and append GetDryRunManifestReport, but never changes selection or compiler symbols itself. Patch compatibility reads adapter declarations and maps policy enums into textual guidance; it does not install them. BuildScriptFiles prints declared filenames and flags and does not verify disk existence or launch files. No Process/File/Directory execution path was present in these checked bodies. Remaining initializer/filter bodies and selected-profile implementation retain their own prior/pending scope; no full BuildProfiles subsystem claim is made.

## Findings and limits

Confirmed: literal whole-source closure of the two scenario report registries and ten wrappers; SurfaceLedger shallow mutable snapshot versus XML clone; report-only behavior and Debug-only wrappers with separate Release production report callers. No new live item custody, server authority, replication, save corruption or performance defect established. No report PASS, NotRun marker, historical path, generated filename or future contract is counted as runtime/native verification.

Pending top-level Debug wrappers after the named prior full manifests and these reads: Consolidation78, FeatureDeps91, Features103, FeatureTests96, FirstSliceAudit78, FirstSlice84, FirstSliceNoGo78, HarmonyStrategy94, HarvestAdapter84, HarvestSalvageGate95, HarvestSourceAudit87, MigrationOrder92, MigrationReady84, Milestone78, PatchAdapter86, PatchEvidence83, SmokeTests83, XmlCleanupPlan79. Names are ConsoleCmdRebirth + listed suffix + Fresh.cs under Scripts/Debug/Commands. Exact pending list is a manifest reconciliation result, not source-read credit. Source files outside this selected scope are not silently promoted from scans.
