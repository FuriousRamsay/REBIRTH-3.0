# Final top-level Debug wrapper tranche

2026-10-15 audit only. All eighteen selected source files were read literally in full, 1,553 lines. No tests, profiling, game, endpoint, console command, build or production edit. Prior FEATURES_AGENT, FEATURES_REGISTRIES, COMMANDS_REMAINDER, DEBUG_PROFILER, DEBUG_PERF_SNAPSHOT and DEBUG_SCENARIO_CLOSURE evidence reconciled before selecting files. Shared ledger/report unchanged.

## Actual execution, compilation and permissions

All eighteen files are Preserve ConsoleCmdAbstract classes enclosed in #if DEBUG, with IsExecuteOnClient=true. RebirthUtils.csproj:39–41 additionally removes Scripts/**/Debug/** from non-Debug compilation. These commands therefore have Debug source reachability and are omitted from ordinary Release builds. Their registry dependencies may still be reachable through production Sys report subcommands; excluding a wrapper does not exclude all underlying report classes. Neither this nor previous source review proves current DLL freshness or a successful native command discovery run.

Native console discovery/authorization remains the previously traced native dispatcher/ConsoleCmdAbstract boundary. None of these wrappers supplies a custom authorization or server-routing layer; none authors a packet or accepts a target player/world/save. A local client prints its process's reports. IsExecuteOnClient is not a claim of server-side validation. `rg -uuu` across Scripts found no external direct use of the eighteen command class symbols; native console discovery remains their intended entry. Searches included ignored Debug directories. No commands were executed to validate registration or permission behavior.

Every Execute branch normalizes the first string, defaults to summary, then logs help or a returned report. Unknown subcommands print help. MigrationOrder wave checks a required second operand; list filters use only the next argument. No branch sets a gameplay policy, starts a coroutine, writes a file/save, installs Harmony or issues another console command. No player resource, replay receipt, pending request, world epoch or durable recovery state is created by the wrappers themselves. The caller objects retain no session/world state requiring lifecycle cleanup.

## Per-command behavior and evidence limits

| Wrapper suffix (ConsoleCmdRebirth…Fresh) | Runtime branch behavior |
|---|---|
| Consolidation | Prints consolidation plan/target shape; no project/file movement. |
| FeatureDeps | Dependency/upstream/guardrail/blocked reports; does not enforce graph order. |
| Features | Domain/hot/network/persistence/scenario coverage reports; no native census or authority implementation. |
| FeatureTests | Test/critical/hot/stateful/blocked matrix; executes no tests. |
| FirstSliceAudit | Audit checklist/blockers; reads no source files. |
| FirstSlice | Slice contract/recommendation/blocked reports; does not select or migrate gameplay. |
| FirstSliceNoGo | Static gate/reason reports; no patch install or actual target selection. |
| HarmonyStrategy | Strategy/reflection/patch-type reports; performs no reflection or Harmony installation. |
| HarvestAdapter | Parses optional integer (default10 on parse failure) and prints no-op preview; does not grant items. |
| HarvestSalvageGate | Critical/server/performance/readiness declarations; does not execute parity or performance tests. |
| HarvestSourceAudit | Candidate/selected/rejected/next records; no new native extraction or source acquisition. |
| MigrationOrder | Order/wave/next planning; no file, build or migration mutations. |
| MigrationReady | Recommended/blocked slice records; does not enable a feature. |
| Milestone | Static cadence/next plan; no scheduling or messages. |
| PatchAdapter | Excluded and measurement candidate reports; measure is not a profiler/install command. |
| PatchEvidence | Target-evidence/required/blocked records; no live native signature verification. |
| SmokeTests | Command matrix/critical/order report; does not execute listed commands. |
| XmlCleanupPlan | Cleanup/rootinclude plan; neither parses XML nor edits includes. |

Most dependency registry bodies already have FEATURES_REGISTRIES **SEMANTIC-FULL** evidence: executable methods and grouped literal fields were inspected, while repeated formatting/comments were elided. That remains semantic coverage; this tranche does not rename it literal whole-file coverage or infer runtime PASS from static statuses. HarvestSalvageImplementationAdapter, TestGateRegistry and ActualSourceAuditRegistry have earlier explicit FULL evidence in FEATURES_AGENT. Targeted recheck of adapter :30–84 confirms PreviewHarvestAmount/PreviewSalvageAmount return BuildNoOpResult and GetPreviewReport formats those results; no grant or replacement operation is called. Negative or extreme parsed integers affect diagnostic preview data only in this no-op implementation.

PatchAdapterPlanRegistry was only a targeted dependency read here, :221–end including every called report method and Matches, **not** new full-file credit. Its GetReport/GetExcludedReport/GetMeasurementReport/GetSafetyReport/GetSummaryReport enumerate declarations and construct strings; they do not inspect live Harmony or install/uninstall patches. Its shallow GetSnapshot does not make wrapper output mutate records. Initializer table/schema remains outside this tranche's whole-file claim. A method name containing measurement is insufficient evidence of a profiler action.

No new live multiplayer defect was established. Existing experimental manual harvest installer/replacement findings remain in FEATURES_AGENT and are not transferred to these no-op/report wrappers. The old report text's gate, blocked, readiness, source-audited or recommended classifications are authored diagnostic metadata, not new project operating instructions or verified current b259 results.

## Full-read manifest

All paths below have prefix `Scripts/Debug/Commands/`; SHA-256 checked after full reads.

| File | Lines | SHA-256 |
|---|---:|---|
| ConsoleCmdRebirthConsolidationFresh.cs | 78 | F12BDC1354D5E2467C82CAD213D015988816ADBC420CE73704B49A8B9527FDE2 |
| ConsoleCmdRebirthFeatureDepsFresh.cs | 91 | F26D97573B2176EEA80A3CDE66D728B1CDB91982FD59F185EC33B345ED9EFEBF |
| ConsoleCmdRebirthFeaturesFresh.cs | 103 | A4B43481AD4F72D69D14EA11626F2DD48C8347CC143B1972187278ECE07A51EB |
| ConsoleCmdRebirthFeatureTestsFresh.cs | 96 | E77E8CD1F4C84BE1A5289891DE7544228E3076273A962F15FA459D705BAE6A07 |
| ConsoleCmdRebirthFirstSliceAuditFresh.cs | 78 | 87226A0C8D65733301CABD3A268A163F9FCAB1A16BC095D2F9836A89F7CCBB3B |
| ConsoleCmdRebirthFirstSliceFresh.cs | 84 | E2DD6888D1E5F9CC04FA567B862C1B60B611E40EFA462D86D660F5CE6418C7D6 |
| ConsoleCmdRebirthFirstSliceNoGoFresh.cs | 78 | 431277DF00BFEF983B056D5166E2877566F5EBA60F6F4B2D3C182F8FF277C3BA |
| ConsoleCmdRebirthHarmonyStrategyFresh.cs | 94 | BEA33D1E51DD3EA046FB97F70504165D6F7A371908717F6BAC457FB377D17851 |
| ConsoleCmdRebirthHarvestAdapterFresh.cs | 84 | C9764794ABDDFC9C9F75564521F06A9B3002D848832C6DFCFA10DACB86C1E883 |
| ConsoleCmdRebirthHarvestSalvageGateFresh.cs | 95 | 9829A1521D8CC775F89F7351B5E2BF7E1443EDD75998A0E2782AA892828B1CBB |
| ConsoleCmdRebirthHarvestSourceAuditFresh.cs | 87 | EE4E1153E965AD9409949254E28A3900CE5BC0741340B986C3C889FC110F6748 |
| ConsoleCmdRebirthMigrationOrderFresh.cs | 92 | 054CE3C31C3582DB3AC676DED2777791A80D5EF7E6E8D42FE5EBECAE4ECFAA19 |
| ConsoleCmdRebirthMigrationReadyFresh.cs | 84 | F4DD5FCF0E2D2C29580429CCE84BF689AE6040C47CF066F1378BBCDC537BD7F4 |
| ConsoleCmdRebirthMilestoneFresh.cs | 78 | C686A4B4D718ABEEB18C84F32E4D01662BA16B97209A4E28E92C088C240056E9 |
| ConsoleCmdRebirthPatchAdapterFresh.cs | 86 | F5D12332257274DE50567DF16FD304E2635FE4C64F654A72F03857085C0488B5 |
| ConsoleCmdRebirthPatchEvidenceFresh.cs | 83 | 22B151CF787F12AB24F39B6F52B2DE795E57BAC2B5111C664ADC691D286E0F72 |
| ConsoleCmdRebirthSmokeTestsFresh.cs | 83 | 725FBB247479AB7E0A7112012264130B6698550DD0C622E358AD41A854A2316F |
| ConsoleCmdRebirthXmlCleanupPlanFresh.cs | 79 | 88FE2ABCBF5E5E3BDF9DC47F0375E9A504BEE8985C1DF29536EA32F5FEC4B90F |

Current top-level Scripts/Debug/Commands has40 files: these18 + closure10 + prior named12 (COMMANDS_REMAINDER5, DEBUG_PERF_SNAPSHOT4, DEBUG_PROFILER1, FEATURES_AGENT HarvestSystems1 and FEATURES_REGISTRIES BuildVerify1). This closes their actual wrapper-source obligation without promoting called service tables. Native console dispatch was reused, not freshly decompiled; no build/test acceptance is claimed.
