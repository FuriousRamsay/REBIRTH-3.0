# Debug farming snapshot service remainder

2026-10-15 current shared cumulative source; operating target REBIRTH3.0/b259. Audit-only; no profiling, command, build, game, tests, git or production/shared ledger/report changes. Prior FARMING_TRACE.md and DEBUG_PROFILER_AGENT_TRACE.md reviewed; previous first240 and sampling reset excerpts are now completed by reading every remaining source line, not duplicate unique coverage. Five full sources listed below; native hook/callee excerpts remain selective only.

## Snapshot service authority and lifecycle

AdvancedFarmingPerfSnapshotService.cs1645 full read. Debug-only via #if DEBUG and csproj Debug-directory exclusion. Release facade is prior full FARMING evidence, not newly counted. Service is process-static counters, bounded8 name buckets and value-struct SampleSnapshot; no persistent IDs, custody, save/write, packet or gameplay operation authored. Sampling defaults false. SetSampling(true) unconditionally clears counters/start time then enables; false only disables; ResetSampling resets counter values but does not change samplingEnabled. Existing scene/frame/stutter/automation commands share this singleton, without ownership token/world generation. Confirmed sampler interference/crossworld report defects remain DEBUG_PROFILER_AGENT_TRACE.md, not new duplicate findings.

Farming ModApi reset invokes ResetSampling and automation reset, with stutter-stop conditional; reset alone retaining enabled flag is exact service behavior, not proof a departed sampler lease survives every lifecycle caller. Existing scene runner lacks lifecycle retirement. No service event subscription/timer/Harmony installer of its own. Server farming processing produces server counters; client scene diagnostics can observe different/zero farming activity while local native lighting produces local counters. A client report is not dedicated/listen parity proof and no counter is replicated.

## Recorder and snapshot semantics

All Record* entrypoints gate samplingEnabled. Numeric methods increment sums/maxima and hit/miss fields; snapshots copy scalars into value struct without sharing mutable backing lists. No locks/Interlocked/volatile/generation are used for recorder/reset/read operations. Exact native lighting thread confinement was not newly decompiled here, so inconsistent concurrent snapshots/lost increments remain conditional on actual multi-thread invocation, not a proven gameplay race. Counter resets during sampling concretely produce discontinuous/negative raw delta reports because BuildSamplingDeltaReport simply subtracts snapshots without epoch guard; nonpositive denominators suppress some averages but not raw negative fields.

Name summary keeps8 approximate heavy hitters by replacing lowest count with1, not exact global top8; case-sensitive names. RecordMissingPlantTileEntityWakeSkip also records anchor-no-plant, intentionally overlapping counts. RecordAreaProcess total time includes maintenance/active processing and its maintenance subtotal is whole-area duration when maintenance ran; separate RecordAreaMaintenancePhase tracks narrower phase, so not additive independent costs. Managed-memory deltas split positive/negative GC.GetTotalMemory samples; these are process heap changes rather than measured allocations per method, and nested phase sums can overlap. Diagnostic long input/public API does not reject negative ticks, but observed production callers use Stopwatch differences; no malicious network input path found under -uuu.

GetSnapshot is sequential field copy, returns current Stopwatch timestamp, not atomic capture. The missing-plant wake-skip field exists only in direct anchor report, not SampleSnapshot/delta; this is report scope limitation. BuildSamplingReport reads statics repeatedly, elapsed wallclock continues after sampling stops because no stop timestamp is captured, so callsPerSecond can decrease simply waiting after stop. A stopped report is not a frozen interval. BuildSamplingDeltaReport rates use passed timestamps, preserving their interval if no reset; formatting uses current culture, not stable interchange schema. No wire format or durable data depends on these strings.

## Report scans and actual gameplay callee separation

BuildReport clamps radius to1–64 with default24 for <1, scans10 vertical layers and (2r+1)^2 tile positions (max166410). Reads plant/plot/tank counts, current water, growth ticks and classification; does not directly subtract water, advance growth, write tile state or network. EvaluatePlantConditions invokes actual LightService.Evaluate, TemperatureService.Evaluate and RebirthUtilities.CheckForWater -> WaterProviderRegistry query. These can fill caches/observe registry and sampling counters; report is not universally pure just because it is a diagnostic. Existing farming whole service/custody trace remains authoritative; no claim of exact no-side-effects for every callee from this source alone. Current selective CheckForWater query ends in RecordWaterRegistryQuery and returns best; consumption provider is a distinct method.

BuildReport mixes supplied WorldBase with GameManager current world for biome/daytime, so arbitrary old-world direct caller could produce mixed context; current command supplies live world, no active mismatched-world caller proved. Near-complete comparison stageRate-20 assumes effective seconds>=1 before multiply; runtime policy clamps must be used before asserting underflow. Descriptive fixed 'activeLoadedPlantPath' strings and legacy worst-case estimates are authored claims, not native timing proof.

## True hook and command callers (selective reads)

- CatchupService297–319 records area/managed delta only when sampling captured true; light/thermal/maintenance recorders likewise called around real processing (rg -uuu callers). Service does not cause that processing.
- Native LightProcessor bindings: RefreshSunlightAtLocalPos, RefreshLightAtLocalPos, recursive SpreadLight/UnspreadLight forward explicit prefixes into dynamic-opacity implementation. PatchInstaller.InstallLightProcessorPatch enumerates7 light/door/block binding types and tries each class; bootstrap farming registration already in FARMING_TRACE.md. First125 installer and explicit bindings40–115 selectively read, no new whole-file credit.
- LightProcessor patch sampling branch adds Stopwatch/Record around existing active-area farming light work; it does not alter gameplay control by sampling itself. BypassBlockLightPatch is a separate explicit diagnostic switch; this snapshot service never sets it.
- rbfarming perfsample start/stop/reset/report, automation and frame/stutter/scene profilers consume same global counters (-uuu). Native console authority and their larger gameplay commands remain prior trace dependencies, not new permission bypass claims.

## Four additional Debug wrappers full read

DiagGates75, RuntimeHealth85, Manifest93, ScenarioPerf88 are all #if DEBUG in Scripts/Debug; client-executed, inherited default native permission0, no override bypass. Each switch invokes only named diagnostic/registry report methods and logs; no direct state toggle, profiler start, file/export, network/save or actual test execution. ScenarioPerf 'metrics' returns declared performance-contract rows, not real captured metrics; Manifest 'measurable' similarly declaration metadata. RuntimeHealth summary/full reports architectural state, not gameplay acceptance. Called registries are prior module/feature evidence or separate dependencies; no unearned full-read credit from a wrapper call. No commands run.

## Remaining qualification

No new active multiplayer state/custody defect established. Counter reset ownership, stopped-report denominator and unqualified cross-thread snapshots constrain evidence validity. Actual native LightProcessor worker context and callee query cache effects remain explicit selective dependencies; no broad light system rewrite authorized or performed. Remaining Debug wrappers still need full reads in separate coherent tranche; this closes actual snapshot body and named four sources only.

## Full-read manifest
- FULL Scripts/AdvancedFarming/Debug/AdvancedFarmingPerfSnapshotService.cs SHA256 936E4E5BBD28747ACBFCA7944E27E04912CD0D9F9D8740D915E8A793CCA61F06 (1645 lines; full read).
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthDiagGatesFresh.cs SHA256 38479527BC7A20AD64F7E7F3C017AB93FE0AE8CE4265FF381238C2A2D158C459 (75 lines; full read).
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthRuntimeHealthFresh.cs SHA256 100436B14481D50E38ED7D3F6B17ECE650FD32EF12F8EF2A0F87EBE435B2C803 (85 lines; full read).
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthManifestFresh.cs SHA256 4BFEAE84B869830745BBFC2B649D8A0CE5A570041B6394B48B7B9C312DD2000C (93 lines; full read).
- FULL Scripts/Debug/Commands/ConsoleCmdRebirthScenarioPerfFresh.cs SHA256 2B02FB36AF16F73903686BBE1D382BCF973E727F37E9C7FE637D8710FB78CFD9 (88 lines; full read).

REBIRTH FEATURE COMPLETION CONFIRMATION: assigned static tranche COMPLETE. Directives/evidence/architecture/documentation PASS; production code/XML/UI/localization/assets/persistence/network modifications N/A; packaging N/A. Compilation Not performed; in-game validation Not performed. No performance acceptance or implementation fixes claimed.
