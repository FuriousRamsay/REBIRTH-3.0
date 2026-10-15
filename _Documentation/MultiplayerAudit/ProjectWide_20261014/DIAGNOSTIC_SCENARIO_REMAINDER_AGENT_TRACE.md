# Performance and scenario source remainder audit

Audit-only source review, 2026-10-15. No game, endpoint, profiling, command execution, build or tests. No production or shared ledger/report changes. This tranche fully read the following 12 files, 3,410 lines, including declaration tables and report bodies. Hashes were checked again after reading. Native methods discussed below were not newly decompiled; native compatibility questions are explicitly left open.

## Full-read manifest

Paths relative to the mod root. SHA-256, current file content:

| Source | Lines | SHA-256 |
|---|---:|---|
| Scripts/Performance/RebirthFastTagsUnionPatch.cs | 125 | 0CEEEA16C980B550913E2CE62205B0B4CF0C4842A2212D36525665E8C1BDE094 |
| Scripts/Performance/RebirthGameSenseDedupePatch.cs | 91 | 65F559AB912A628741DC8E203D27BF0FD4E15B7E927BD14A62088DDA94C840AA |
| Scripts/Performance/RebirthReflectionRateAtSpeedPatch.cs | 54 | 54BF1F5CD0DE75F65E7D6110465199A42D5ECE433AB9886853FA3AB7771A03D0 |
| Scripts/Performance/RebirthSpawnSelectionScratchLease.cs | 61 | 5F75AE422983482FB661B8B5DD7762CDA50B9481970C0FB99A844BFD645C2BAE |
| Scripts/Performance/RebirthThemeScratchLease.cs | 61 | 64FCBDF853450A27F312B282268AD8E4843316733AA24EAA55EA6D2AD6728CD0 |
| Scripts/Performance/Architecture/RebirthFrameScheduler.cs | 213 | EE76F4BF94DA030BEAE2851E70AF873D75DAECC9F9012E1F7B7939119CECE264 |
| Scripts/Performance/Architecture/RebirthPerformanceArchitecture.cs | 646 | 18E0C777029FD777874D38717140CCB4491045157622DB44A1E1B47AFCEE0E9B |
| Scripts/Scenarios/RebirthScenarioContractRegistry.cs | 358 | F2D8DADAE7F1210A644B21102EC2E0C26F5BB25546FFDDE9F3F3BB79A74558DE |
| Scripts/Scenarios/RebirthScenarioManagerBoundaryRegistry.cs | 454 | 286EBCD052C12E42C61E4722F47F48424AE10E56B5B8F7B18AA0242831AFA7A3 |
| Scripts/Scenarios/RebirthScenarioModuleBindingRegistry.cs | 487 | CF8BEC9EC010E3272D8F5F63CCF373465F4089EB6500E7E262B2087A2F5DF110 |
| Scripts/Scenarios/RebirthScenarioPerformanceContractRegistry.cs | 502 | 536216A9287A55CD49007492E26218789CE0EA51734B44DA9F2B698E311C61E3 |
| Scripts/Scenarios/RebirthScenarioRuntimePolicyCacheRegistry.cs | 358 | 4FE9ED947A777FEFC788E8879502D6854D5135C1A3A5EBFE78647BEC74D7AF78 |

## Prior coverage and caller scope

Reconciled the existing DEBUG_PROFILER_AGENT_TRACE, DEBUG_PERF_SNAPSHOT_AGENT_TRACE, COMMANDS_REMAINDER_AGENT_TRACE, MODULES_AGENT_TRACE and NATIVE_TRANSPORT_TRACE. PerformanceArchitecture previously had partial coverage; this is its full-body credit. DryRunPlanner Debug257/Release49 and RuntimeScenarioPolicyCache181 were previously full reads and receive no new credit here. Bootstrap, console command and spawn composition references below are targeted caller checks, not new full-file credit.

`rg -uuu` over Scripts includes ignored Debug callers. RebirthFresh_Init.cs:60 explicitly installs FastTags, :62 reflection and :64 GameSense without a Debug conditional around these steps. The 12 selected files are outside Debug directories and not among project compile removals. RebirthUtils.csproj:39–41 excludes feature Debug directories from non-Debug builds; standalone ConsoleCmdRebirthSys remains a report caller in Release. This establishes source installation intent, not proof the current on-disk DLL contains these source versions or that a native patch successfully applies.

## Active performance behavior

**FastTags:** the prefix returns an existing operand when containment proves its union bits already cover the other operand. Null words are treated as empty; the single-bit path and multiword path are both handled. Interlocked sampling checks the first 4,000 and subsequent periodic unions against native bit tests, disables the optimization and logs once upon mismatch. Equality checks do not establish backing-array immutability. No exact current native FastTags body was available in this tranche; aliasing safety remains a native dependency, not a confirmed defect. No performance measurement was made.

**GameSense:** reflection selects SendEvent overloads with 2–3 arguments, string event name and primitive/string value; a non-null third context bypasses deduplication. A process-global locked dictionary keys only by event name and compares value.ToString() for five seconds using unscaled time. It records the attempt before the original send runs. Thus a failed/throwing send can suppress a same-value retry until the window expires; there is no successful-send receipt. Cross-instance collision additionally requires multiple native GSClient instances, which was not established. Installation catches failure and returns a status string logged by bootstrap. This is peripheral telemetry behavior, not player inventory/network authority. Dictionary state is not world-scoped and has no lifecycle reset.

**Reflection:** FrameUpdate uses static prior position/time/speed and mutates ReflectionManager.optionsSelected.rateScale according to speed, preserving a detected external base scale. A movement of 60 or more resets speed; smaller discontinuities can be treated as movement. State is not scoped to player, manager or world, so a new player/manager within the accepted delta can inherit a temporary visual throttle. The exact native options copy/reference behavior was not read, so a stronger global-quality persistence claim is not established. No gameplay custody or authority mutation is present.

**Scratch leases:** each service retains only thread-static double lists, with a capacity ceiling of 256. Nested or oversized requests allocate transient storage. Generation and in-use checks prevent a copied old struct Dispose from clearing a newer lease; disposal on another thread throws. Targeted SpawnCompositionService callers at :254 and :467 use scoped leases. Getter access after disposal is not fenced, but no escaping active caller was established. These lists retain no Unity objects. No allocation reduction was measured.

## Dormant scheduler and diagnostic accounting

`rg -uuu` found no external RebirthFrameScheduler.Register or ResetForLifecycle calls in Scripts. TickPhase is reached through its own GameUpdate subscription only after registration. Do not classify this as a live gameplay scheduler or a tested optimization. It supports one phase, interval scheduling without catch-up bursts and eight-strike callback disablement. Budget values are descriptors; execution is not preempted or budget-enforced.

Latent implementation concerns: subscription occurs before the owner-thread registration check; Unregister lacks the registration thread check; reset clears IDs back to one without changing owner-thread state, permitting an old handle to collide with a new entry after reset; reset during a pump can invalidate its captured iteration count. Without registration/reset callers these remain dormant hazards, not demonstrated production failures.

PerformanceArchitecture declares default module descriptions, many explicitly legacy 2.6 references. GetModulesSnapshot copies the array but shares mutable manifest objects; TryGet returns the same objects. Actual snapshot consumers found are bootstrap status :130, Sys :344 and CostReport :618. No mutating consumer was found. Stats are locked. ShouldSampleThisFrame counts invocations, not guaranteed frames, and arbitrary masks do not uniformly mean one in mask+1 except appropriate bit masks. Default mask31 has the intended pattern. No external RecordTicks/EndSample producer was found beyond the dormant scheduler. Zero/unmeasured reports cannot establish a runtime performance result. Cost report labels per-sample measurements and unmeasured entries explicitly.

## Scenario declarations do not implement multiplayer enforcement

ContractRegistry declares six scenario contracts. ManagerBoundaryRegistry declares manager boundaries. ModuleBindingRegistry declares owns/uses/register policy/forbid relationships. RuntimePolicyCacheRegistry declares six planned cache contracts, authority and invalidation descriptions. None creates managers, installs ticks, changes a scenario, routes network messages, persists gameplay state or enforces the authority strings it prints. The separate previously reviewed RuntimeScenarioPolicyCache is a default/report snapshot and is not proof these planned contracts are enacted. A printed survivor blocked description is report metadata, not a newly discovered project instruction or gameplay gate.

ContractRegistry.GetSnapshot is shallow and exposes mutable declaration objects; ModuleBindingRegistry and RuntimePolicyCacheRegistry explicitly deep-clone declarations. No mutation caller was found. This is a qualified integrity concern for future consumers, not a demonstrated current permission bypass. Manager boundaries have no snapshot-returning runtime integration.

PerformanceContractRegistry has eight declared scenario sets and eleven metric contracts. Its matrix lists Hive+Survivor, and the enum includes that combination, but its set table omits the standalone HiveSurvivor declaration. Set/detail/blocked reports therefore lack that matrix case. This is confirmed diagnostic completeness only. The source explicitly marks measurements NotRun; no latency, FPS or multiplayer pass result can be inferred.

Caller evidence: ConsoleCmdRebirthScenarioFresh:53–85 prints ContractRegistry reports. ConsoleCmdRebirthSys:746–907 prints contract, binding, runtime-policy, performance and manager reports. Ignored Debug command wrappers also call those report methods (ScenarioPolicyFresh:54–82, PerfFresh:54–78, BindingsFresh:54–88, ManagersFresh:55–88). These callers do not convert the declarations into runtime authority or lifecycle enforcement. Their bodies receive no new full-read credit here.

## Remaining dependencies

Next genuine pending scope is the scenario Surface/XML registries and uncovered Debug command/report wrappers after manifest reconciliation. Exact native FastTags backing-array behavior, reflection options semantics and GSClient instance/send-failure behavior remain qualified dependencies if these local presentation optimizations warrant deeper verification. This tranche establishes no new player-item loss, server-owner bypass, scenario replication implementation, or measured performance regression.
